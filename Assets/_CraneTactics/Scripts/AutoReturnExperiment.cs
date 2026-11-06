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
            float radius=kind==Kind.Koromochi?.24f:kind==Kind.Heavy?.38f:kind==Kind.Slimy?.48f:.55f;
            return Mathf.Clamp01(1-offset/radius);
        }
        public static float Seconds(Kind kind,float quality) =>
            (kind==Kind.Koromochi?1.2f:kind==Kind.Heavy?2f:kind==Kind.Slimy?5f:3.8f)*Mathf.Lerp(.65f,1.6f,Mathf.Clamp01(quality));
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
        public event Action<ClawEvent,Unit> ClawActivity;
        CabinetProfile cabinetProfile=CabinetProfile.Classic;
        PhysicsMaterial floorMaterial;
        PhysicsMaterial softMaterial;
        readonly List<Transform> fingers=new List<Transform>();
        readonly List<Vector3> fingerPositions=new List<Vector3>();
        float clawOpen;
        readonly List<string> experimentLog=new List<string>();
        ReturnRoute returnRoute;
        LineRenderer routeLine;
        Transform shadow;
        int pickAxis, routeIndex;
        bool axisMoving, arrived, waitForRelease;
        float gripLimit, grabQuality, arrivalTime, contactAge;
        Unit impactUnit;
        Vector3 grabPoint;
        int experimentCase;
        public void LoadExperimentCase(int value) {experimentCase=Mathf.Clamp(value,0,4); AutoReturn=true; LoadStage(0); RecordExperiment("case-reset");}
        void ApplyExperimentCase()
        {
            if(stage!=0 || experimentCase==0) return;
            board.Units.Clear(); board.Pit=new Cell(5,2); board.Add(Kind.Knight,1,4,3);
            if(experimentCase==1) {board.Units[0].Cell=new Cell(4,3); board.Units[0].Hp=2; board.Add(Kind.Heavy,1,2,1).Hostile=true; board.Add(Kind.Guard,4,2,3);}
            else if(experimentCase==2) board.Add(Kind.Guard,4,1,2);
            else if(experimentCase==3) {board.Add(Kind.Scout,4,2,1); board.Add(Kind.Guard,4,0,2);}
            else {board.Add(Kind.Koromochi,3,2,2); board.Add(Kind.Guard,4,2,2);}
        }

        public void SetAutoReturn(bool enabled)
        {
            experimentCase=0; AutoReturn=enabled; LoadStage(0); RecordExperiment("mode-reset");
        }
        void ResetExperiment()
        {
            foreach(var u in board.Units) displayedHp[u]=u.Hp;
            routeLine=new GameObject("Return route").AddComponent<LineRenderer>(); routeLine.transform.SetParent(world);
            routeLine.widthMultiplier=.065f;
            var routeMaterial=Resources.Load<Material>("RouteGuide");
            routeLine.sharedMaterial=routeMaterial!=null?routeMaterial:Mat(new Color(.2f,.8f,.75f)); routeLine.positionCount=3;
            shadow=Shape("Claw ground shadow",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.4f,.008f,.4f),teal,world).transform;
            foreach(var actor in actors.Values) actor.AddComponent<DropContact>().Game=this;
            fingers.Clear(); fingerPositions.Clear();
            foreach(var finger in head.GetComponentsInChildren<Transform>()) if(finger.name.Contains("finger")) {fingers.Add(finger); fingerPositions.Add(finger.localPosition);}
            clawOpen=1;
            if(floorMaterial!=null) Destroy(floorMaterial);
            if(softMaterial!=null) Destroy(softMaterial);
            floorMaterial=new PhysicsMaterial(cabinetProfile.Name) {dynamicFriction=cabinetProfile.FloorFriction,staticFriction=cabinetProfile.FloorFriction,bounciness=cabinetProfile.Bounce,frictionCombine=PhysicsMaterialCombine.Minimum};
            softMaterial=new PhysicsMaterial("Soft toy") {dynamicFriction=.25f,staticFriction=.25f,bounciness=.45f,bounceCombine=PhysicsMaterialCombine.Maximum};
            foreach(var col in world.GetComponentsInChildren<Collider>()) if(col.name.StartsWith("Cell ")) col.sharedMaterial=floorMaterial;
            if(AutoReturn) foreach(var pair in actors) if(pair.Key.Kind==Kind.Slimy || pair.Key.Kind==Kind.Koromochi) pair.Value.GetComponent<Collider>().sharedMaterial=softMaterial;
            ResetAutoPick();
        }
        void ResetAutoPick()
        {
            pickAxis=0; axisMoving=false; arrived=false; arrivalTime=0; impactUnit=null; contactAge=0; waitForRelease=true;
            crane.Speed=3.2f;
            if(AutoReturn && motion==Motion.Selecting) target=new Vector3(0,3.5f,0);
        }
        // Used by both physical input and automation; an axis may only advance once.
        public void PickInput(bool down,float dt)
        {
            if(!AutoReturn || paused || motion!=Motion.Selecting) return;
            if(down) {if(!axisMoving) RecordExperiment("axis-start"); axisMoving=true; if(pickAxis==0) target.x=Mathf.Min(board.Size-1,target.x+2*Mathf.Clamp(dt,0,.05f)); else target.z=Mathf.Min(board.Size-1,target.z+2*Mathf.Clamp(dt,0,.05f));}
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
            returnRoute=new ReturnRoute(grabPoint+Vector3.up*3.5f,Position(board.Pit,3.5f));
            RecordExperiment("approach");
        }
        bool ResolveAutoGrab()
        {
            if(held==null || grabQuality<.3f)
            {
                var direction=held==null?new Cell(1,0):BoardState.Push(held.Cell.X-grabPoint.x,held.Cell.Y-grabPoint.z,new Cell(0,0),new Cell(1,0),.01f);
                int before=board.Captured.Count;
                board.ResolveMiss(board.CommitRelease(),held,direction);
                RecordExperiment(held==null?"empty-miss":"contact-push");
                toast=held==null?"Empty claw. Try a different line.":"SLIP! The contact changed the board.";
                if(board.Captured.Count>before) toast="PRIZE GET!  A missed grab pushed it into the chute.";
                PlayTone(180,.12f); BeginPresentation(); return false;
            }
            pickup=held.Cell; gripLimit=GripProfile.Seconds(held.Kind,grabQuality);
            returnRoute=new ReturnRoute(new Vector3(grabPoint.x,3.5f,grabPoint.z),Position(board.Pit,3.5f)); routeIndex=1;
            RecordExperiment("grab-success"); PlayTone(480,.09f); return true;
        }
        void TickExperiment(float dt)
        {
            routeLine.enabled=AutoReturn && motion!=Motion.Result && motion!=Motion.Presenting;
            clawOpen=Mathf.MoveTowards(clawOpen,motion==Motion.Lifting || motion==Motion.Carrying?0:1,dt*4);
            for(int i=0;i<fingers.Count;i++) {var p=fingerPositions[i]; fingers[i].localPosition=new Vector3(p.x*Mathf.Lerp(.75f,1.3f,clawOpen),p.y,p.z*Mathf.Lerp(.75f,1.3f,clawOpen));}
            shadow.gameObject.SetActive(motion!=Motion.Result); shadow.position=new Vector3(crane.Head.x,.016f,crane.Head.z);
            if(!AutoReturn) return;
            if(!controls.Held) waitForRelease=false;
            if(motion==Motion.Selecting && !Automation && !waitForRelease) PickInput(controls.Held && (controls.KeyboardAction || axisMoving || !OverHud()),dt);
            var preview=motion==Motion.Selecting?new ReturnRoute(target,Position(board.Pit,3.5f)):returnRoute;
            if(preview!=null) for(int i=0;i<3;i++) routeLine.SetPosition(i,new Vector3(preview.Points[i].x,.045f,preview.Points[i].z));
            if(motion!=Motion.Carrying) {head.localRotation=Quaternion.identity; return;}
            crane.Speed=cabinetProfile.ReturnSpeed;
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
            impactUnit=contacted; contactAge=0; RecordExperiment("impact");
        }
        [Serializable] sealed class InputRecord
        {
            public string mode,action,unit,contact,phase,outcome,boardState; public float time,x,z,grip; public int stage,turn,revision;
        }
        void RecordExperiment(string action)
        {
            var row=new InputRecord {mode=AutoReturn?"B":"A",action=action,unit=held?.Id??"",phase=motion.ToString(),time=Time.time,x=crane.Head.x,z=crane.Head.z,grip=GripRemaining,stage=stage,turn=board.Turns,revision=board.Revision};
            row.outcome=board.Outcome.ToString(); row.boardState=string.Join(";",board.Units.Select(u=>$"{u.Id}:{u.Kind}:{u.Cell}:{u.Hp}"));
            row.contact=impactUnit?.Id??"";
            if(experimentLog.Count>=4096) experimentLog.RemoveAt(0);
            experimentLog.Add(JsonUtility.ToJson(row));
            switch(action)
            {
                case "approach": ClawActivity?.Invoke(ClawEvent.Approach,held); break;
                case "grab-success": ClawActivity?.Invoke(ClawEvent.GrabSuccess,held); break;
                case "empty-miss": case "contact-push": ClawActivity?.Invoke(ClawEvent.GrabFailure,held); break;
                case "release": ClawActivity?.Invoke(ClawEvent.Release,held); break;
                case "grip-break": ClawActivity?.Invoke(ClawEvent.GripBreak,held); break;
                case "impact": ClawActivity?.Invoke(ClawEvent.Impact,held); break;
                case "capture": ClawActivity?.Invoke(ClawEvent.Capture,held); break;
                case "settled": ClawActivity?.Invoke(ClawEvent.Settled,held); break;
            }
        }
        public void ExportExperiment(string path) {File.WriteAllLines(path,experimentLog);}
        void DrawExperiment()
        {
            GUI.Label(new Rect(26,80,360,24),AutoReturn?"B  AUTO RETURN  /  "+cabinetProfile.Name:"A  FREE CARRY  /  Esc: experiments",small);
            if(paused)
            {
                if(GUI.Button(new Rect(24,110,245,32),AutoReturn?"Experiment B → A (restart)":"Experiment A → B (restart)")) SetAutoReturn(!AutoReturn);
                if(GUI.Button(new Rect(24,150,245,32),"Save input / result log")) ExportExperiment(Path.Combine(Application.persistentDataPath,"crane-experiment.jsonl"));
                if(GUI.Button(new Rect(24,190,245,32),"CASE A: Heavy → enemy → chute")) LoadExperimentCase(1);
                if(GUI.Button(new Rect(24,230,245,32),"CASE B: Hold to capture")) LoadExperimentCase(2);
                if(GUI.Button(new Rect(24,270,245,32),"CASE C: Offset contact push")) LoadExperimentCase(3);
                if(GUI.Button(new Rect(24,310,245,32),"Koromochi: slippery round toy")) LoadExperimentCase(4);
                if(GUI.Button(new Rect(24,350,245,32),cabinetProfile.Name+" floor (switch + restart)")) {cabinetProfile=cabinetProfile==CabinetProfile.Classic?CabinetProfile.Sweets:CabinetProfile.Classic; LoadStage(stage);}
            }
            if(AutoReturn && !paused && motion!=Motion.Result) DrawReturnHud();
        }
    }
}
