using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CraneTactics.Rules;
using UnityEngine;

namespace CraneTactics
{
    // A reusable route is data, not a second board. Only BoardState commits outcomes.
    public sealed class ReturnRoute
    {
        public readonly Vector3[] Points;
        public ReturnRoute(Vector3 start, Vector3 chute)
        {
            Points=new[]{start,new Vector3(chute.x,start.y,start.z),new Vector3(chute.x,start.y,chute.z)};
        }
        public float Length => Vector3.Distance(Points[0],Points[1])+Vector3.Distance(Points[1],Points[2]);
    }
    public static class GripProfile
    {
        public static float Quality(Kind kind, float offset)
        {
            float radius=kind==Kind.Heavy?.38f:kind==Kind.Slimy?.48f:.55f;
            return Mathf.Clamp01(1-offset/radius);
        }
        public static float Seconds(Kind kind,float quality) =>
            (kind==Kind.Heavy?2f:kind==Kind.Slimy?5f:3.8f)*Mathf.Lerp(.65f,1.6f,Mathf.Clamp01(quality));
    }
    public sealed class DropContact : MonoBehaviour
    {
        public OneNightGame Game;
        void OnCollisionEnter(Collision collision) {Game?.ObserveImpact(GetComponent<UnitView>().Unit,collision.collider.GetComponent<UnitView>()?.Unit);}
    }
    public sealed partial class OneNightGame
    {
        public bool AutoReturn {get;private set;}
        public string PickHint => pickAxis==0?"1  HOLD: MOVE RIGHT":"2  HOLD: MOVE INTO CABINET";
        public float GripRemaining => held==null?0:Mathf.Max(0,HoldLimit-carryTime);
        public Vector3 ClawPosition => crane.Head;
        public float HoldLimit => held==null?float.PositiveInfinity:AutoReturn?gripLimit:held.HoldSeconds;
        public IReadOnlyList<string> ExperimentLog => experimentLog;
        readonly List<string> experimentLog=new List<string>();
        ReturnRoute returnRoute;
        LineRenderer routeLine;
        Transform shadow;
        int pickAxis, routeIndex;
        bool axisMoving, arrived;
        float gripLimit, grabQuality, arrivalTime;
        Unit impactUnit;
        Vector3 grabPoint;
        int experimentCase;
        public void LoadExperimentCase(int value) {experimentCase=Mathf.Clamp(value,0,3); AutoReturn=true; LoadStage(0); RecordExperiment("case-reset");}
        void ApplyExperimentCase()
        {
            if(stage!=0 || experimentCase==0) return;
            board.Units.Clear(); board.Pit=new Cell(5,2); board.Add(Kind.Knight,1,4,3);
            if(experimentCase==1) {board.Add(Kind.Heavy,1,2,3).Hostile=true; board.Add(Kind.Guard,4,2,3);}
            else if(experimentCase==2) board.Add(Kind.Guard,4,1,2);
            else {board.Add(Kind.Scout,4,2,1); board.Add(Kind.Guard,4,0,2);}
        }

        public void SetAutoReturn(bool enabled)
        {
            experimentCase=0; AutoReturn=enabled; LoadStage(0); RecordExperiment("mode-reset");
        }
        void ResetExperiment()
        {
            routeLine=new GameObject("Return route").AddComponent<LineRenderer>(); routeLine.transform.SetParent(world);
            routeLine.widthMultiplier=.045f; routeLine.sharedMaterial=Mat(new Color(.2f,.8f,.75f)); routeLine.positionCount=3;
            shadow=Shape("Claw ground shadow",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.4f,.008f,.4f),teal,world).transform;
            foreach(var actor in actors.Values) actor.AddComponent<DropContact>().Game=this;
            ResetAutoPick();
        }
        void ResetAutoPick()
        {
            pickAxis=0; axisMoving=false; arrived=false; arrivalTime=0; impactUnit=null;
            crane.Speed=3.2f;
            if(AutoReturn && motion==Motion.Selecting) {target=new Vector3(0,3.5f,0); crane.Reset(target);}
        }
        // Used by both physical input and automation; an axis may only advance once.
        public void PickInput(bool down,float dt)
        {
            if(!AutoReturn || paused || motion!=Motion.Selecting) return;
            if(down) {axisMoving=true; if(pickAxis==0) target.x=Mathf.Min(board.Size-1,target.x+2*Mathf.Clamp(dt,0,.05f)); else target.z=Mathf.Min(board.Size-1,target.z+2*Mathf.Clamp(dt,0,.05f));}
            else if(axisMoving) {axisMoving=false; RecordExperiment("axis-locked"); if(pickAxis==0) pickAxis=1; else BeginAutoGrab(target);}
        }
        public void BeginAutoGrab(Vector3 point)
        {
            if(!AutoReturn || paused || motion!=Motion.Selecting) return;
            grabPoint=new Vector3(Mathf.Clamp(point.x,0,board.Size-1),0,Mathf.Clamp(point.z,0,board.Size-1));
            held=board.Units.Where(u=>u.Alive).OrderBy(u=>Vector2.Distance(new Vector2(u.Cell.X,u.Cell.Y),new Vector2(grabPoint.x,grabPoint.z))).ThenBy(u=>u.Id,StringComparer.Ordinal).FirstOrDefault();
            float distance=held==null?float.PositiveInfinity:Vector2.Distance(new Vector2(held.Cell.X,held.Cell.Y),new Vector2(grabPoint.x,grabPoint.z));
            if(distance>.85f) held=null;
            grabQuality=held==null?0:GripProfile.Quality(held.Kind,distance);
            target=grabPoint+Vector3.up*1.2f; motion=Motion.Approaching; elapsed=0;
            RecordExperiment("approach");
        }
        bool ResolveAutoGrab()
        {
            if(held==null || grabQuality<.3f)
            {
                var direction=held==null?new Cell(1,0):BoardState.Push(held.Cell.X-grabPoint.x,held.Cell.Y-grabPoint.z,new Cell(0,0),new Cell(1,0),.01f);
                board.ResolveMiss(board.CommitRelease(),held,direction);
                RecordExperiment(held==null?"empty-miss":"contact-push");
                foreach(var pair in actors) {pair.Value.SetActive(pair.Key.Alive); if(pair.Key.Alive) {pair.Value.transform.position=Position(pair.Key.Cell); if(pair.Key==held) pair.Value.transform.Rotate(0,35,0);} }
                toast=held==null?"Empty claw. Try a different line.":"SLIP! The contact changed the board.";
                PlayTone(180,.12f); held=null; motion=board.Outcome==Outcome.Playing?Motion.Selecting:Motion.Result; elapsed=resultTime=0; ResetAutoPick(); return false;
            }
            pickup=held.Cell; gripLimit=GripProfile.Seconds(held.Kind,grabQuality);
            returnRoute=new ReturnRoute(new Vector3(grabPoint.x,3.5f,grabPoint.z),Position(board.Pit,3.5f)); routeIndex=1;
            RecordExperiment("grab-success"); PlayTone(480,.09f); return true;
        }
        void TickExperiment(float dt)
        {
            routeLine.enabled=AutoReturn && motion!=Motion.Result;
            shadow.gameObject.SetActive(motion!=Motion.Result); shadow.position=new Vector3(crane.Head.x,.016f,crane.Head.z);
            if(!AutoReturn) return;
            if(motion==Motion.Selecting && !Automation) PickInput(controls.Held && (controls.KeyboardAction || axisMoving || !OverHud()),dt);
            var preview=motion==Motion.Selecting?new ReturnRoute(target,Position(board.Pit,3.5f)):returnRoute;
            if(preview!=null) for(int i=0;i<3;i++) routeLine.SetPosition(i,new Vector3(preview.Points[i].x,.045f,preview.Points[i].z));
            if(motion!=Motion.Carrying) return;
            crane.Speed=1.7f;
            if(!arrived)
            {
                target=returnRoute.Points[routeIndex];
                if(Vector3.Distance(crane.Head,target)<.04f) {if(routeIndex==1) routeIndex=2; else arrived=true;}
            }
            else {arrivalTime+=dt; if(arrivalTime>.65f && crane.LoadVelocity.magnitude<.5f) {RecordExperiment("chute-arrival"); Release();}}
            var fraction=GripRemaining/gripLimit;
            head.localRotation=Quaternion.Euler(0,0,fraction<.25f?Mathf.Sin(Time.time*28)*8:0);
        }
        public void ObserveImpact(Unit falling,Unit contacted)
        {
            if(!AutoReturn || motion!=Motion.Falling || held!=falling || contacted==null || contacted==held || impactUnit!=null) return;
            impactUnit=contacted; RecordExperiment("impact");
        }
        [Serializable] sealed class InputRecord
        {
            public string mode,action,unit,phase; public float time,x,z,grip; public int stage,turn,revision;
        }
        void RecordExperiment(string action)
        {
            var row=new InputRecord {mode=AutoReturn?"B":"A",action=action,unit=held?.Id??"",phase=motion.ToString(),time=Time.time,x=crane.Head.x,z=crane.Head.z,grip=GripRemaining,stage=stage,turn=board.Turns,revision=board.Revision};
            if(experimentLog.Count>=4096) experimentLog.RemoveAt(0);
            experimentLog.Add(JsonUtility.ToJson(row));
        }
        public void ExportExperiment(string path) {File.WriteAllLines(path,experimentLog);}
        void DrawExperiment()
        {
            if(paused)
            {
                if(GUI.Button(new Rect(24,110,245,32),AutoReturn?"Experiment B → A (restart)":"Experiment A → B (restart)")) SetAutoReturn(!AutoReturn);
                if(GUI.Button(new Rect(24,150,245,32),"Save input / result log")) ExportExperiment(Path.Combine(Application.persistentDataPath,"crane-experiment.jsonl"));
                if(GUI.Button(new Rect(24,190,245,32),"CASE A: Heavy → enemy → chute")) LoadExperimentCase(1);
                if(GUI.Button(new Rect(24,230,245,32),"CASE B: Hold to capture")) LoadExperimentCase(2);
                if(GUI.Button(new Rect(24,270,245,32),"CASE C: Offset contact push")) LoadExperimentCase(3);
            }
            if(AutoReturn && held!=null && (motion==Motion.Carrying || motion==Motion.Lifting))
            {
                GUI.color=Color.black; GUI.DrawTexture(new Rect(Screen.width/2-100,Screen.height-65,200,10),Texture2D.whiteTexture);
                GUI.color=GripRemaining/gripLimit<.25f?new Color(1,.3f,.15f):gold;
                GUI.DrawTexture(new Rect(Screen.width/2-100,Screen.height-65,200*Mathf.Clamp01(GripRemaining/gripLimit),10),Texture2D.whiteTexture); GUI.color=Color.white;
            }
        }
    }
}
