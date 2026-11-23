using System.Collections.Generic;
using System.Linq;
using CraneTactics.Rules;
using UnityEngine;

namespace CraneTactics
{
    public sealed partial class OneNightGame
    {
        readonly Dictionary<Unit,Vector3> beatStarts=new Dictionary<Unit,Vector3>();
        readonly Dictionary<Unit,int> displayedHp=new Dictionary<Unit,int>();
        IReadOnlyList<ResolutionBeat> playback;
        int beatIndex;
        float beatTime, hitStop, shake;
        bool shakeEnabled=true;
        public string PresentationPhase {get;private set;}="";
        public int VisibleHp(Unit unit) => displayedHp.TryGetValue(unit,out int hp)?hp:unit.Hp;

        void ClearPresentation()
        {
            playback=null; beatStarts.Clear(); displayedHp.Clear(); popups.Clear(); visuals.Clear(); beatIndex=0; beatTime=hitStop=shake=0; PresentationPhase="";
        }
        void BeginPresentation()
        {
            // BoardState is already final. Keep input locked until all visual beats finish.
            playback=board.Resolution; beatIndex=0; motion=Motion.Presenting;
            footprint.gameObject.SetActive(false); pushArrow.gameObject.SetActive(false);
            foreach(var actor in actors.Values) {actor.GetComponent<Rigidbody>().isKinematic=true; actor.GetComponent<Collider>().enabled=false;}
            BeginBeat();
        }
        void BeginBeat()
        {
            beatTime=0; beatStarts.Clear();
            foreach(var pair in actors) beatStarts[pair.Key]=pair.Value.transform.position;
            PresentationPhase=playback[beatIndex].Label;
            var opening=playback[beatIndex];
            if(opening.Label=="impact" && opening.Target!=null && opening.Actor?.Kind==Kind.Heavy) {hitStop=.065f; shake=shakeEnabled?.16f:0;}
            if(opening.Label=="clock-dash" && opening.Target!=null) shake=shakeEnabled?.1f:0;
            RecordExperiment("visual-"+PresentationPhase);
        }
        void TickPresentation(float dt)
        {
            if(playback==null || beatIndex>=playback.Count) return;
            if(hitStop>0) {hitStop-=dt; return;}
            beatTime+=dt;
            var beat=playback[beatIndex];
            bool contact=beat.Label=="impact" || beat.Label=="contact";
            bool hasCapture=beat.Poses.Any(p=>p.Captured && actors[p.Unit].activeSelf);
            float duration=hasCapture?1.05f:contact?.62f:.4f;
            float move=Mathf.SmoothStep(0,1,Mathf.Clamp01((beatTime-(contact?.12f:0))/(contact?.38f:.4f)));
            foreach(var pose in beat.Poses)
            {
                var actor=actors[pose.Unit]; if(!actor.activeSelf) continue;
                var start=beatStarts[pose.Unit]; var end=Position(pose.Captured?board.Pit:pose.Cell);
                var position=Vector3.Lerp(start,end,move);
                // The contacted piece recoils before sliding. All motion is cosmetic.
                if(contact && pose.Unit==beat.Target && beatTime<.12f)
                {
                    float wobble=Mathf.Sin(beatTime/.12f*Mathf.PI);
                    actor.transform.rotation=Quaternion.Euler(0,0,-12*wobble);
                    position.y+=.07f*wobble;
                }
                else if(beat.Target==pose.Unit && !contact)
                    actor.transform.rotation=Quaternion.Euler(0,0,Mathf.Sin(beatTime/duration*Mathf.PI)*10);
                else actor.transform.rotation=Quaternion.identity;
                if(contact && pose.Unit==beat.Actor && beatTime<.3f && !pose.Captured)
                    actor.transform.localScale=new Vector3(1,1,1)+new Vector3(.12f,-.18f,.12f)*Mathf.Sin(Mathf.Clamp01(beatTime/.3f)*Mathf.PI);
                else actor.transform.localScale=Vector3.one;
                if(pose.Captured && beatTime>.5f)
                {
                    float fall=Mathf.Clamp01((beatTime-.5f)/.55f);
                    position=end+Vector3.down*(2.2f*fall*fall);
                    actor.transform.rotation=Quaternion.Euler(0,fall*50,fall*35);
                    actor.transform.localScale=Vector3.one*Mathf.Lerp(1,.15f,fall);
                }
                else if(pose.Hp<=0 && !pose.Captured && beatTime>duration-.22f)
                {
                    float defeated=Mathf.Clamp01((beatTime-duration+.22f)/.22f);
                    actor.transform.rotation=Quaternion.Euler(0,0,defeated*65);
                    actor.transform.localScale=Vector3.one*Mathf.Lerp(1,.1f,defeated);
                }
                actor.transform.position=position;
                if(beatTime>=duration)
                {
                    if(displayedHp.TryGetValue(pose.Unit,out int shown) && pose.Hp<shown && !pose.Captured) AddPopup(actor.transform.position,$"-{shown-pose.Hp}",pose.Unit.Enemy?new Color(1,.85f,.35f):new Color(1,.45f,.4f));
                    displayedHp[pose.Unit]=pose.Hp;
                    actor.transform.rotation=Quaternion.identity; actor.transform.localScale=Vector3.one;
                    if(pose.Hp<=0) actor.SetActive(false);
                }
            }
            if(beatTime<duration) return;
            if(hasCapture) {PlayTone(740,.16f); RecordExperiment("visual-capture");}
            beatIndex++;
            if(beatIndex<playback.Count) BeginBeat();
            else {playback=null; PresentationPhase=""; RecordExperiment("visual-complete"); CompleteResolution();}
        }
        void TickCamera(float dt)
        {
            if(!AutoReturn) return;
            float mid=(board.Size-1)/2f;
            bool transport=motion==Motion.Carrying || motion==Motion.Lifting || motion==Motion.Approaching;
            var focus=new Vector3(mid,transport?.95f:.55f,mid);
            if(transport) focus+=new Vector3(crane.Head.x-mid,0,crane.Head.z-mid)*.10f;
            float blend=1-Mathf.Exp(-dt*3);
            var offset=new Vector3(3.8f,11,-10.5f);
            view.transform.position=Vector3.Lerp(view.transform.position,focus+offset,blend);
            if(shake>0) {shake=Mathf.Max(0,shake-dt); view.transform.position+=new Vector3(Mathf.Sin(Time.time*71),Mathf.Sin(Time.time*93),0)*shake*.5f;}
            view.transform.rotation=Quaternion.Slerp(view.transform.rotation,Quaternion.LookRotation(-offset),blend);
            // Reserve the bottom HUD strip; fit the full board on narrower windows too.
            float size=(board.Size==4?3.55f:4.3f)+(transport?.18f:0);
            size=Mathf.Max(size,(board.Size+1.3f)/(2*Mathf.Max(.65f,view.aspect)));
            view.orthographicSize=Mathf.Lerp(view.orthographicSize,size,blend);
        }
        void WorldTag(Vector3 at,string label,Color color,int width=140)
        {
            var p=view.WorldToScreenPoint(at); if(p.z<=0) return;
            var rect=new Rect(p.x-width/2,Screen.height-p.y,width,25);
            GUI.color=new Color(.025f,.065f,.075f,.94f); GUI.DrawTexture(rect,Texture2D.whiteTexture);
            GUI.color=Color.white; var previous=small.normal.textColor; small.normal.textColor=color;
            GUI.Label(new Rect(rect.x+7,rect.y+2,width-10,24),label,small); small.normal.textColor=previous;
        }
        string PhaseLabel()
        {
            switch(PresentationPhase)
            {
                case "enemy-step": return "ENEMY MOVES"; case "enemy-attack": return "ENEMY ATTACKS"; case "knight": return "KNIGHT ATTACKS";
                case "bounce": return "JELLY BOUNCE"; case "slide": return "ICING SLIDE"; case "magnet": return "MAGNET PULL";
                case "belt": return "BELT MOVES"; case "clock-dash": return "WIND-UP DASH"; default: return "IMPACT";
            }
        }
        void DrawReturnHud()
        {
            WorldTag(Position(board.Pit,.1f)+Vector3.right*.85f,"HOME / CAPTURE",new Color(1,.82f,.4f),135);
            if(held!=null && (motion==Motion.Carrying || motion==Motion.Lifting))
                WorldTag(crane.Head+Vector3.up*.5f,DisplayName(held.Kind).ToUpperInvariant()+" / HELD",CabinetProfile.Classic.TileA,165);
            string state=motion==Motion.Selecting?(pickAxis==0?"HOLD BUTTON 1":"HOLD BUTTON 2"):motion==Motion.Approaching?"CLAW DESCENDS":motion==Motion.Lifting?"LIFTING":motion==Motion.Carrying?"CARRY, THEN DROP":motion==Motion.Falling?"DROP":PhaseLabel();
            string instruction,detail;
            if(motion==Motion.Selecting)
            {
                var candidate=board.Units.Where(u=>u.Alive).OrderBy(u=>Vector2.Distance(new Vector2(u.Cell.X,u.Cell.Y),new Vector2(target.x,target.z))).FirstOrDefault();
                float quality=candidate==null?0:GripProfile.Quality(candidate.Kind,Vector2.Distance(new Vector2(candidate.Cell.X,candidate.Cell.Y),new Vector2(target.x,target.z)));
                instruction=quality>=GripProfile.SlipBelowQuality?$"{DisplayName(candidate.Kind)}: {AbilityText(candidate.Kind)}  ·  GRIP {GripSeconds(candidate,quality):0.0}s":pickAxis==0?"Hold to slide right, let go to lock. Space works too.":"Hold to move into the cabinet, let go to descend.";
                detail=board.IsPrize?"Unlimited tries. Bring your new friend home.":"Rival intent: "+board.Plan();
            }
            else
            {
                instruction=held==null?"The claw brushed the board. Watch what moved.":$"{DisplayName(held.Kind)}: {AbilityText(held.Kind)}";
                detail=motion==Motion.Carrying?$"Hold ← ↓ → ↑ (or arrow keys) to carry. Gold dots: reach {ReachRemaining:0.0} steps. DROP lets go.":motion==Motion.Presenting?"One release. One resolved turn.":"Watch the claw and the landing ring.";
            }
            DrawDeck(state,instruction,detail);
            if(held!=null && (motion==Motion.Carrying || motion==Motion.Lifting))
            {
                float fraction=Mathf.Clamp01(GripRemaining/gripLimit), barY=Screen.height-DeckHeight-8;
                GUI.color=new Color(.2f,.28f,.28f); GUI.DrawTexture(new Rect(0,barY,Screen.width,6),Texture2D.whiteTexture);
                GUI.color=fraction<.25f?new Color(1,.35f,.15f):new Color(1,.8f,.35f); GUI.DrawTexture(new Rect(0,barY,Screen.width*fraction,6),Texture2D.whiteTexture); GUI.color=Color.white;
                WorldTag(crane.Head+Vector3.up*.85f,$"GRIP {GripRemaining:0.0}s",fraction<.25f?new Color(1,.5f,.3f):new Color(1,.8f,.35f),100);
            }
        }
    }
}
