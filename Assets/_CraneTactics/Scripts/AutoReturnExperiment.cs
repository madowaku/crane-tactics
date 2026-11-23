using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CraneTactics.Rules;
using UnityEngine;

namespace CraneTactics
{
    public sealed class DropContact : MonoBehaviour
    {
        public OneNightGame Game;
        void OnCollisionEnter(Collision collision) {Game?.ObserveImpact(GetComponent<UnitView>().Unit,collision.collider.GetComponent<UnitView>()?.Unit);}
    }
    // Button cabinet play (mode B, historically "auto return"): two lit buttons aim the claw, then the
    // player carries the toy anywhere with the direction buttons until DROP or until the grip gives out.
    // Only BoardState commits outcomes.
    public sealed partial class OneNightGame
    {
        public bool AutoReturn {get;private set;}
        public string PickHint => pickAxis==0?"1  HOLD: MOVE RIGHT":"2  HOLD: MOVE INTO CABINET";
        public float GripRemaining => held==null?0:Mathf.Max(0,HoldLimit-carryTime);
        public Vector3 ClawPosition => crane.Head;
        public float HoldLimit => held==null?float.PositiveInfinity:AutoReturn?gripLimit:held.HoldSeconds;
        public IReadOnlyList<string> ExperimentLog => experimentLog;
        public event Action<ClawEvent,Unit> ClawActivity;
        CabinetProfile cabinetProfile=CabinetProfile.Classic, cabinetOverride;
        PhysicsMaterial floorMaterial;
        PhysicsMaterial softMaterial;
        readonly List<Transform> fingers=new List<Transform>();
        readonly List<Vector3> fingerPositions=new List<Vector3>();
        float clawOpen;
        readonly List<string> experimentLog=new List<string>();
        readonly Dictionary<Cell,Transform> reachMarkers=new Dictionary<Cell,Transform>();
        Transform shadow;
        int pickAxis;
        bool axisMoving, waitForRelease;
        Vector3? steerGoal;
        public Vector2Int SteerDirection {get;private set;}
        float gripLimit, grabQuality, contactAge;
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
            experimentCase=0; cabinetOverride=null; AutoReturn=enabled; LoadStage(0); RecordExperiment("mode-reset");
        }
        void ResetExperiment()
        {
            foreach(var u in board.Units) displayedHp[u]=u.Hp;
            reachMarkers.Clear();
            var reachMaterial=Unlit(new Color(1,.78f,.3f));
            for(int y=0;y<board.Size;y++) for(int x=0;x<board.Size;x++)
            {
                var marker=Shape("Grip reach",PrimitiveType.Cylinder,Position(new Cell(x,y),.03f),new Vector3(.2f,.006f,.2f),gold,world).transform;
                marker.GetComponent<Renderer>().sharedMaterial=reachMaterial; marker.gameObject.SetActive(false); reachMarkers[new Cell(x,y)]=marker;
            }
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
            if(AutoReturn) foreach(var pair in actors) if(pair.Key.Kind==Kind.Slimy || pair.Key.Kind==Kind.Koromochi || pair.Key.Kind==Kind.Petapetan) pair.Value.GetComponent<Collider>().sharedMaterial=softMaterial;
            ResetAutoPick();
            DressCabinet();
        }
        void ResetAutoPick()
        {
            pickAxis=0; axisMoving=false; steerGoal=null; SteerDirection=Vector2Int.zero; impactUnit=null; contactAge=0; waitForRelease=true;
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
            pickup=held.Cell; gripLimit=GripSeconds(held,grabQuality);
            RecordExperiment("grab-success"); PlayTone(480,.09f); return true;
        }
        // Grid steps the claw can still carry the held toy before the grip gives out.
        public float ReachRemaining => held==null?0:GripRemaining*cabinetProfile.ReturnSpeed;
        // One axis at a time, like the cabinet's direction buttons: keys first, then a held lit dome.
        Vector2Int PlayerSteer()
        {
            var m=controls.Movement;
            if(m.sqrMagnitude>0) return Mathf.Abs(m.x)>=Mathf.Abs(m.y)?new Vector2Int(m.x>0?1:-1,0):new Vector2Int(0,m.y>0?1:-1);
            if(!controls.MouseHeld) return Vector2Int.zero;
            var point=new Vector2(controls.Pointer.x,Screen.height-controls.Pointer.y);
            foreach(var button in SteerButtons) if(DeckHit(DeckButtonRect(button,Screen.width,Screen.height),point)) return SteerVector(button);
            return Vector2Int.zero;
        }
        // Automation drives the same claw by giving it a point to head for, one axis at a time.
        public void SteerTo(Vector3 point) {steerGoal=new Vector3(Mathf.Clamp(point.x,0,board.Size-1),3.5f,Mathf.Clamp(point.z,0,board.Size-1));}
        void TickExperiment(float dt)
        {
            clawOpen=Mathf.MoveTowards(clawOpen,motion==Motion.Lifting || motion==Motion.Carrying?0:1,dt*4);
            for(int i=0;i<fingers.Count;i++) {var p=fingerPositions[i]; fingers[i].localPosition=new Vector3(p.x*Mathf.Lerp(.75f,1.3f,clawOpen),p.y,p.z*Mathf.Lerp(.75f,1.3f,clawOpen));}
            shadow.gameObject.SetActive(motion!=Motion.Result); shadow.position=new Vector3(crane.Head.x,.016f,crane.Head.z);
            if(!AutoReturn) return;
            if(!controls.Held) waitForRelease=false;
            if(motion==Motion.Selecting && !Automation && !waitForRelease) PickInput(controls.SpaceHeld || controls.MouseHeld && (axisMoving || PointerOnActiveDeckButton),dt);
            bool carrying=motion==Motion.Carrying;
            float reach=ReachRemaining;
            foreach(var pair in reachMarkers)
            {
                bool show=carrying && Mathf.Abs(pair.Key.X-crane.Head.x)+Mathf.Abs(pair.Key.Y-crane.Head.z)<=reach;
                if(pair.Value.gameObject.activeSelf!=show) pair.Value.gameObject.SetActive(show);
            }
            if(!carrying) {SteerDirection=Vector2Int.zero; head.localRotation=Quaternion.identity; return;}
            crane.Speed=cabinetProfile.ReturnSpeed;
            SteerDirection=Automation?Vector2Int.zero:PlayerSteer();
            if(Automation && steerGoal.HasValue) target=steerGoal.Value;
            else if(SteerDirection!=Vector2Int.zero) target=new Vector3(Mathf.Clamp(crane.Head.x+SteerDirection.x,0,board.Size-1),3.5f,Mathf.Clamp(crane.Head.z+SteerDirection.y,0,board.Size-1));
            else target=new Vector3(crane.Head.x,3.5f,crane.Head.z);
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
        // Plays one planned move like a careful player: carry one axis at a time, then either stop and
        // let go once the swing settles, or let go on the move when the ring shows the planned cell and heading.
        public IEnumerator PlayMove(CraneMove move,float timeout=20)
        {
            float end=Time.time+timeout;
            var unit=board.Units[move.Unit];
            if(move.Contact)
            {
                BeginAutoGrab(new Vector3(unit.Cell.X-move.Direction.X*BoardSolver.NudgeOffset,0,unit.Cell.Y-move.Direction.Y*BoardSolver.NudgeOffset));
                while(!IsSelecting && !IsResult && Time.time<end) yield return null;
                yield break;
            }
            Grab(unit);
            while(!IsCarrying && Time.time<end) yield return null;
            if(!IsCarrying) yield break;
            var d=move.Direction; bool still=d.Equals(BoardSolver.StillDirection(pickup,move.Landing));
            var stop=still?move.Landing:new Cell(move.Landing.X-d.X,move.Landing.Y-d.Y);
            // Cross the axis that is not the final heading first, so the last leg runs along it.
            var corner=d.X!=0?new Cell(pickup.X,stop.Y):new Cell(stop.X,pickup.Y);
            foreach(var waypoint in new[]{corner,stop})
            {
                SteerTo(Position(waypoint,3.5f));
                while(IsCarrying && Vector2.Distance(new Vector2(crane.Head.x,crane.Head.z),new Vector2(waypoint.X,waypoint.Y))>.02f && Time.time<end) yield return null;
            }
            bool Ready() => ForecastClear && ForecastCell.Equals(move.Landing) && ForecastDirection.Equals(move.Direction);
            if(still)
            {
                while(IsCarrying && !(Ready() && crane.LoadVelocity.magnitude<NearRestThreshold*.8f) && GripRemaining>.12f && Time.time<end) yield return null;
            }
            else
            {
                // Let the swing from the earlier leg die down first, or it skews the throw.
                while(IsCarrying && crane.LoadVelocity.magnitude>.3f && GripRemaining>1.2f && Time.time<end) yield return null;
                var past=new Cell(move.Landing.X+d.X,move.Landing.Y+d.Y);
                SteerTo(Position(board.Inside(past)?past:move.Landing,3.5f));
                while(IsCarrying && !Ready() && Time.time<end) yield return null;
            }
            if(IsCarrying) Drop();
            steerGoal=null;
            while(!IsSelecting && !IsResult && Time.time<end) yield return null;
        }
        public IEnumerator CarryHome(Unit unit)
        {
            yield return PlayMove(new CraneMove {Unit=board.Units.IndexOf(unit),Landing=board.Pit,Direction=BoardSolver.StillDirection(unit.Cell,board.Pit)});
        }
        public void ExportExperiment(string path) {File.WriteAllLines(path,experimentLog);}
        public float GripSeconds(Unit unit,float quality) => GripProfile.Seconds(unit.Kind,quality)*unit.GripScale*(board.IsStuck(unit)?.6f:1);
        void DrawLab(float x,float y)
        {
            float w=300,h=40,g=6; int row=0;
            Rect Next() => new Rect(x,y+(h+g)*row++,w,h);
            if(ArcadePill(Next(),AutoReturn?"Lab: switch to mouse free carry":"Lab: switch to cabinet buttons",amberPill)) SetAutoReturn(!AutoReturn);
            if(ArcadePill(Next(),"Lab: save input log",amberPill)) ExportExperiment(Path.Combine(Application.persistentDataPath,"crane-experiment.jsonl"));
            if(ArcadePill(Next(),"Case A: Heavy > enemy > chute",amberPill)) LoadExperimentCase(1);
            if(ArcadePill(Next(),"Case B: hold to capture",amberPill)) LoadExperimentCase(2);
            if(ArcadePill(Next(),"Case C: offset contact push",amberPill)) LoadExperimentCase(3);
            if(ArcadePill(Next(),"Case D: Koromochi on classic",amberPill)) LoadExperimentCase(4);
            if(ArcadePill(Next(),"Floor: "+cabinetProfile.Name+" (restart)",amberPill)) {cabinetOverride=cabinetProfile==CabinetProfile.Classic?CabinetProfile.Sweets:CabinetProfile.Classic; LoadStage(stage);}
        }
        void DrawExperiment()
        {
            if(AutoReturn && !paused && motion!=Motion.Result) DrawReturnHud();
        }
    }
}
