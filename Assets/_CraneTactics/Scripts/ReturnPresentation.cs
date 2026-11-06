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
        float beatTime;
        public string PresentationPhase {get;private set;}="";
        public int VisibleHp(Unit unit) => displayedHp.TryGetValue(unit,out int hp)?hp:unit.Hp;

        void ClearPresentation()
        {
            playback=null; beatStarts.Clear(); displayedHp.Clear(); beatIndex=0; beatTime=0; PresentationPhase="";
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
            RecordExperiment("visual-"+PresentationPhase);
        }
        void TickPresentation(float dt)
        {
            if(playback==null || beatIndex>=playback.Count) return;
            beatTime+=dt;
            var beat=playback[beatIndex];
            bool contact=beat.Label=="impact" || beat.Label=="contact";
            bool hasCapture=beat.Poses.Any(p=>p.Captured && actors[p.Unit].activeSelf);
            float duration=contact?(hasCapture?1.05f:.62f):.4f;
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
            var focus=new Vector3(mid,transport?1.25f:.85f,mid);
            if(transport) focus+=new Vector3(crane.Head.x-mid,0,crane.Head.z-mid)*.10f;
            float blend=1-Mathf.Exp(-dt*3);
            var offset=new Vector3(3.8f,11,-10.5f);
            view.transform.position=Vector3.Lerp(view.transform.position,focus+offset,blend);
            view.transform.rotation=Quaternion.Slerp(view.transform.rotation,Quaternion.LookRotation(-offset),blend);
            // Reserve the bottom HUD strip; fit the full board on narrower windows too.
            float size=(board.Size==4?3.15f:3.95f)+(transport?.18f:0);
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
        void DrawReturnHud()
        {
            WorldTag(Position(board.Pit,.1f)+Vector3.right*.85f,"HOME / CAPTURE",new Color(1,.82f,.4f),135);
            if(held!=null && (motion==Motion.Carrying || motion==Motion.Lifting))
                WorldTag(crane.Head+Vector3.up*.5f,held.Kind.ToString().ToUpperInvariant()+" / HELD",cream,145);
            float w=Mathf.Min(940,Screen.width-40), x=(Screen.width-w)/2, y=Screen.height-94;
            GUI.color=new Color(.025f,.065f,.075f,.97f); GUI.DrawTexture(new Rect(x,y,w,78),Texture2D.whiteTexture); GUI.color=Color.white;
            string state=motion==Motion.Selecting?"01  AIM":motion==Motion.Approaching?"02  CONTACT":motion==Motion.Lifting?"03  LIFT":motion==Motion.Carrying?"04  RETURN":motion==Motion.Falling?"05  DROP":"06  "+(PresentationPhase=="enemy-step"?"ENEMY MOVES":PresentationPhase=="enemy-attack"?"ENEMY ATTACKS":PresentationPhase=="knight"?"KNIGHT ATTACKS":"IMPACT → CAPTURE");
            GUI.Label(new Rect(x+16,y+8,255,25),state,text);
            string detail;
            if(motion==Motion.Selecting)
            {
                var candidate=board.Units.Where(u=>u.Alive).OrderBy(u=>Vector2.Distance(new Vector2(u.Cell.X,u.Cell.Y),new Vector2(target.x,target.z))).FirstOrDefault();
                float quality=candidate==null?0:GripProfile.Quality(candidate.Kind,Vector2.Distance(new Vector2(candidate.Cell.X,candidate.Cell.Y),new Vector2(target.x,target.z)));
                detail=quality>=.3f?$"{candidate.Kind}  ·  GRIP {GripProfile.Seconds(candidate.Kind,quality):0.0}s":"Aim for the center of a toy";
                GUI.Label(new Rect(x+280,y+8,w-300,25),PickHint+"  /  release to lock",text);
            }
            else
            {
                detail=held==null?"Claw contact → board changes":$"{held.Kind}  ·  {(held.Damage==0?"PUSH 1 TILE":"IMPACT "+held.Damage)}";
                GUI.Label(new Rect(x+280,y+8,w-300,25),motion==Motion.Carrying?"Space / click: DROP NOW   ·   Wait: HOME CAPTURE":motion==Motion.Presenting?"Watch the result — next move follows":"Follow the claw and the gold landing ring",text);
            }
            GUI.Label(new Rect(x+16,y+41,260,24),detail,small);
            GUI.Label(new Rect(x+280,y+41,w-300,24),stage==1?"Unlimited tries. Bring Slimy home.":motion==Motion.Presenting?"One release. One resolved turn.":experimentCase==1?"Heavy → Guard → chute; Knight finishes Heavy.":experimentCase==2?"Short route, lone rival: hold to capture safely.":"After landing (may change): "+board.Plan(),small);
            if(held!=null && (motion==Motion.Carrying || motion==Motion.Lifting))
            {
                float fraction=Mathf.Clamp01(GripRemaining/gripLimit);
                GUI.color=new Color(.2f,.28f,.28f); GUI.DrawTexture(new Rect(x,y-8,w,5),Texture2D.whiteTexture);
                GUI.color=fraction<.25f?new Color(1,.35f,.15f):gold; GUI.DrawTexture(new Rect(x,y-8,w*fraction,5),Texture2D.whiteTexture); GUI.color=Color.white;
                WorldTag(crane.Head+Vector3.up*.85f,$"GRIP {GripRemaining:0.0}s → HOME",fraction<.25f?new Color(1,.5f,.3f):gold,165);
            }
        }
    }
}
