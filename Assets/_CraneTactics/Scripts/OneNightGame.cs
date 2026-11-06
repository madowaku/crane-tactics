using System.Collections.Generic;
using System.Linq;
using CraneTactics.Rules;
using UnityEngine;

namespace CraneTactics
{
    public sealed class UnitView : MonoBehaviour { public Unit Unit; }
    public sealed partial class OneNightGame : MonoBehaviour
    {
        enum Motion { Selecting, Approaching, Lifting, Carrying, Falling, Presenting, Result }
        readonly CraneInput controls=new CraneInput();
        readonly CraneController crane=new CraneController();
        readonly Dictionary<Unit,GameObject> actors=new Dictionary<Unit,GameObject>();
        readonly List<Material> materials=new List<Material>();
        BoardState board; Camera view; Transform world, head, cable, footprint, pushArrow;
        Unit held; Rigidbody body; Motion motion; Vector3 target, releaseVelocity;
        Cell pickup; int token, stage; float elapsed, quiet, carryTime, resultTime; bool paused, sound;
        string toast=""; AudioSource audioSource; GUIStyle title, text, small;
        bool keyboardMode;
        Transform effect, celebrating; float effectAge; bool teamShown;
        public float NearRestThreshold=.35f;
        public bool Automation {get;set;}
        public BoardState Board => board;
        public bool IsCarrying => motion==Motion.Carrying;
        public bool IsSelecting => motion==Motion.Selecting;
        public bool IsResult => motion==Motion.Result;
        public bool IsPresenting => motion==Motion.Presenting;
        public float CarrySeconds => carryTime;
        public void PauseGame(bool value)
        {
            paused=value;
            if(!paused) waitForRelease=true;
            if(body!=null && motion==Motion.Falling) {body.isKinematic=paused; if(!paused) body.linearVelocity=releaseVelocity;}
        }
        public void Aim(Cell cell) {target=Position(cell,3.5f);}
        public void Grab(Unit unit)
        {
            if(paused || motion!=Motion.Selecting || unit==null || !unit.Alive) return;
            if(AutoReturn) {BeginAutoGrab(Position(unit.Cell)); return;}
            held=unit; pickup=held.Cell; target=Position(pickup,1.2f); motion=Motion.Approaching; elapsed=0;
            RecordExperiment("approach");
        }
        public void Drop() {if(!paused && motion==Motion.Carrying) Release();}
        public bool SelectAtScreenPosition(Vector2 position)
        {
            if(motion!=Motion.Selecting || paused) return false;
            if(!Physics.Raycast(view.ScreenPointToRay(position),out var hit,100)) return false;
            var candidate=hit.collider.GetComponent<UnitView>()?.Unit;
            if(candidate==null || !candidate.Alive) return false;
            Grab(candidate); return true;
        }
        public void NextCabinet() {if(motion==Motion.Result) LoadStage(board.Outcome==Outcome.Defeat?stage:stage==2?0:stage+1);}
        Color cream=new Color(.86f,.81f,.66f), teal=new Color(.12f,.32f,.34f), gold=new Color(.85f,.59f,.2f);
        string mastery="";

        void Start()
        {
            view=Camera.main;
            if(view==null) {var cameraObject=new GameObject("Gameplay Camera",typeof(Camera),typeof(AudioListener)); cameraObject.tag="MainCamera"; cameraObject.transform.SetParent(transform); view=cameraObject.GetComponent<Camera>();}
            view.transform.position=new Vector3(9,12,-12); view.transform.LookAt(new Vector3(2.5f,.4f,2.5f)); view.orthographic=true; view.orthographicSize=5.8f;
            view.backgroundColor=new Color(.035f,.075f,.09f); view.clearFlags=CameraClearFlags.SolidColor;
            if(FindObjectsByType<Light>(FindObjectsSortMode.None).Length==0) {var light=new GameObject("Warm Key",typeof(Light)).GetComponent<Light>(); light.transform.SetParent(transform); light.type=LightType.Directional; light.intensity=1.05f; light.color=new Color(1,.88f,.72f); light.transform.rotation=Quaternion.Euler(45,-25,0); light.shadows=LightShadows.Soft;}
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.23f,.28f,.32f);
            audioSource=gameObject.AddComponent<AudioSource>();
            LoadStage(0);
        }
        Material Mat(Color color)
        {
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color=color; materials.Add(material); return material;
        }
        GameObject Shape(string name,PrimitiveType shape,Vector3 position,Vector3 scale,Color color,Transform parent,bool collider=false)
        {
            var o=GameObject.CreatePrimitive(shape); o.name=name; o.transform.SetParent(parent,false); o.transform.localPosition=position; o.transform.localScale=scale; o.GetComponent<Renderer>().sharedMaterial=Mat(color);
            if(!collider) Destroy(o.GetComponent<Collider>()); return o;
        }
        Vector3 Position(Cell cell,float height=.42f) => new Vector3(cell.X,height,cell.Y);
        Cell Grid(Vector3 p) => new Cell(Mathf.Clamp(Mathf.FloorToInt(p.x+.5f),0,board.Size-1),Mathf.Clamp(Mathf.FloorToInt(p.z+.5f),0,board.Size-1));
        void LoadStage(int value)
        {
            if(board!=null) board.CancelPending();
            if(world!=null) {world.gameObject.SetActive(false); Destroy(world.gameObject);}
            foreach(var material in materials) Destroy(material); materials.Clear(); actors.Clear();
            stage=value; board=BoardState.Create(stage); held=null; body=null; effect=null; celebrating=null; teamShown=false; motion=Motion.Selecting; paused=false; elapsed=quiet=carryTime=resultTime=0;
            ClearPresentation();
            ApplyExperimentCase();
            world=new GameObject("OneNight Cabinet").transform;
            world.SetParent(transform,false);
            float n=board.Size, mid=(n-1)/2;
            var cabinet=Resources.Load<GameObject>("Toys/Cabinet");
            if(cabinet!=null) {var shell=Instantiate(cabinet,world); float scale=n/6f; shell.transform.localScale=new Vector3(scale,1,scale); shell.transform.localPosition=new Vector3(mid-2.5f*scale,0,mid-2.5f*scale);}
            var plinth=Shape("Cabinet plinth collision",PrimitiveType.Cube,new Vector3(mid,-.65f,mid),new Vector3(n+1,.8f,n+1),teal,world,true);
            if(cabinet!=null) plinth.GetComponent<Renderer>().enabled=false;
            for(int y=0;y<n;y++) for(int x=0;x<n;x++)
            {
                var c=new Cell(x,y);
                if(c.Equals(board.Pit))
                {
                    Shape("Visible collection chute",PrimitiveType.Cube,Position(c,-.3f),new Vector3(.95f,.12f,.95f),new Color(.025f,.055f,.055f),world);
                    for(int edge=-1;edge<=1;edge+=2) {Shape("Glowing chute edge",PrimitiveType.Cube,Position(c,.03f)+new Vector3(edge*.45f,0,0),new Vector3(.08f,.09f,.96f),gold,world); Shape("Glowing chute edge",PrimitiveType.Cube,Position(c,.03f)+new Vector3(0,0,edge*.45f),new Vector3(.96f,.09f,.08f),gold,world);}
                    var glow=new GameObject("Chute glow",typeof(Light)); glow.transform.SetParent(world); glow.transform.position=Position(c,.5f); var lamp=glow.GetComponent<Light>(); lamp.type=LightType.Point; lamp.color=new Color(1,.66f,.2f); lamp.intensity=2; lamp.range=2;
                    continue;
                }
                Shape("Cell "+c,PrimitiveType.Cube,Position(c,-.1f),new Vector3(.96f,.2f,.96f),(x+y)%2==0?cream:Color.Lerp(cream,teal,.2f),world,true);
            }
            Shape("Back rail",PrimitiveType.Cube,new Vector3(mid,.18f,n-.4f),new Vector3(n+.2f,.45f,.2f),gold,world,true);
            Shape("Front rail",PrimitiveType.Cube,new Vector3(mid,.18f,-.6f),new Vector3(n+.2f,.45f,.2f),gold,world,true);
            Shape("Left rail",PrimitiveType.Cube,new Vector3(-.6f,.18f,mid),new Vector3(.2f,.45f,n),gold,world,true);
            Shape("Right rail",PrimitiveType.Cube,new Vector3(n-.4f,.18f,mid),new Vector3(.2f,.45f,n),gold,world,true);
            foreach(var u in board.Units) MakeUnit(u);
            head=Shape("Claw housing",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.6f,.16f,.6f),gold,world).transform;
            var clawModel=Resources.Load<GameObject>("Toys/Claw");
            if(clawModel!=null) {head.GetComponent<Renderer>().enabled=false; head.localScale=Vector3.one*1.2f; Instantiate(clawModel,head);}
            else for(int i=0;i<3;i++) {float a=i*Mathf.PI*2/3; var finger=Shape("Claw finger",PrimitiveType.Cube,new Vector3(Mathf.Cos(a)*.3f,-.3f,Mathf.Sin(a)*.3f),new Vector3(.09f,.55f,.09f),cream,head); finger.transform.localRotation=Quaternion.Euler(Mathf.Sin(a)*-20,0,Mathf.Cos(a)*20);}
            cable=Shape("Suspension",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.035f,1,.035f),teal,world).transform;
            footprint=Shape("Landing uncertainty",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.85f,.018f,.85f),gold,world).transform;
            footprint.GetComponent<Renderer>().enabled=false;
            var ring=footprint.gameObject.AddComponent<LineRenderer>(); ring.useWorldSpace=false; ring.loop=true; ring.widthMultiplier=.035f; ring.sharedMaterial=Mat(gold); ring.positionCount=48;
            for(int i=0;i<48;i++) ring.SetPosition(i,new Vector3(Mathf.Cos(i*Mathf.PI/24)*.5f,0,Mathf.Sin(i*Mathf.PI/24)*.5f));
            pushArrow=Shape("Push direction",PrimitiveType.Cube,Vector3.zero,new Vector3(.14f,.04f,.6f),new Color(.2f,.9f,.65f),world).transform;
            pushArrow.GetComponent<Renderer>().enabled=false; pushArrow.localScale=Vector3.one;
            var arrow=pushArrow.gameObject.AddComponent<LineRenderer>(); arrow.useWorldSpace=false; arrow.widthMultiplier=.06f; arrow.sharedMaterial=Mat(new Color(.2f,.9f,.65f)); arrow.positionCount=5;
            arrow.SetPositions(new[]{new Vector3(0,0,-.3f),new Vector3(0,0,.3f),new Vector3(-.16f,0,.1f),new Vector3(0,0,.3f),new Vector3(.16f,0,.1f)});
            target=new Vector3(mid,3.5f,mid); crane.Reset(target);
            view.transform.position=new Vector3(mid+4.5f,9,mid-12); view.transform.LookAt(new Vector3(mid,1.1f,mid)); view.orthographicSize=n==4?4.2f:4.8f;
            toast=stage==0?"Pick up a friend. Drop a plan.":stage==1?"Catch Slimy in the golden chute.":"A new friend. A new way to win.";
            ResetExperiment();
        }
        void MakeUnit(Unit unit)
        {
            var root=new GameObject(unit.Kind.ToString()); root.transform.SetParent(world); root.transform.position=Position(unit.Cell); root.AddComponent<UnitView>().Unit=unit;
            var collider=root.AddComponent<BoxCollider>(); collider.size=new Vector3(.65f,.8f,.65f);
            var rb=root.AddComponent<Rigidbody>(); rb.isKinematic=true; rb.interpolation=RigidbodyInterpolation.Interpolate; rb.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative; rb.constraints=RigidbodyConstraints.FreezeRotation;
            var toy=Resources.Load<GameObject>("Toys/"+unit.Kind);
            if(toy!=null) {var visual=Instantiate(toy,root.transform); visual.transform.localPosition=new Vector3(0,-.4f,0); actors.Add(unit,root); return;}
            Color color=unit.Kind==Kind.Knight?cream:unit.Kind==Kind.Heavy?new Color(.68f,.27f,.15f):unit.Kind==Kind.Slimy?new Color(.28f,.8f,.59f):new Color(.3f,.35f,.48f);
            if(unit.Kind==Kind.Koromochi) color=new Color(.95f,.65f,.72f);
            var type=unit.Kind==Kind.Slimy || unit.Kind==Kind.Koromochi?PrimitiveType.Sphere:unit.Kind==Kind.Knight?PrimitiveType.Capsule:PrimitiveType.Cube;
            Shape("Toy body",type,Vector3.zero,unit.Kind==Kind.Heavy?new Vector3(.78f,.8f,.72f):new Vector3(.6f,.65f,.6f),color,root.transform);
            if(unit.Kind==Kind.Knight) {Shape("Teal visor",PrimitiveType.Cube,new Vector3(0,.16f,-.27f),new Vector3(.43f,.13f,.12f),teal,root.transform); for(int i=0;i<12;i++) {float a=i*Mathf.PI/6; Shape("Golden ring",PrimitiveType.Sphere,new Vector3(Mathf.Cos(a)*.22f,.72f+Mathf.Sin(a)*.22f,0),Vector3.one*.095f,gold,root.transform);} }
            else {for(int i=-1;i<=1;i+=2) Shape("Eye",PrimitiveType.Sphere,new Vector3(i*.12f,.12f,-.3f),Vector3.one*.075f,Color.black,root.transform);}
            actors.Add(unit,root);
        }
        void Update()
        {
            if(board==null) return;
            if(controls.Pause) PauseGame(!paused);
            if(paused) return;
            float dt=Mathf.Min(Time.deltaTime,.05f); elapsed+=dt;
            TickCamera(dt);
            TickExperiment(dt);
            if(effect!=null) {effectAge+=dt; effect.localScale=new Vector3(.5f+effectAge*2,.018f,.5f+effectAge*2); if(effectAge>.45f) {Destroy(effect.gameObject); effect=null;} }
            if(motion==Motion.Result)
            {
                resultTime+=dt;
                if(resultTime>.65f && !teamShown && (stage==1 || stage==2 && board.Outcome==Outcome.Victory)) ShowTeam();
                if(celebrating!=null) celebrating.localScale=Vector3.one*(1+Mathf.Sin(resultTime*3)*.035f);
                return;
            }
            if(motion==Motion.Presenting) {TickPresentation(dt); return;}
            var ray=view.ScreenPointToRay(controls.Pointer); var plane=new Plane(Vector3.up,Vector3.zero);
            if(!AutoReturn && !Automation && (motion==Motion.Selecting || motion==Motion.Carrying))
            {
                var movement=controls.Movement;
                if(movement.sqrMagnitude>0) keyboardMode=true;
                else if(controls.PointerMoved) keyboardMode=false;
                if(movement.sqrMagnitude>0) target+=new Vector3(movement.x,0,movement.y)*3*dt;
                else if(!keyboardMode && plane.Raycast(ray,out float distance)) {var p=ray.GetPoint(distance); target.x=p.x; target.z=p.z;}
                target.x=Mathf.Clamp(target.x,0,board.Size-1); target.z=Mathf.Clamp(target.z,0,board.Size-1); target.y=3.5f;
            }
            if(!AutoReturn && !Automation && motion==Motion.Selecting && controls.Action && (controls.KeyboardAction || !OverHud()))
            {
                if(controls.KeyboardAction) {var candidate=board.At(Grid(target)); if(candidate!=null) Grab(candidate);}
                else SelectAtScreenPosition(controls.Pointer);
            }
            crane.Tick(target,dt); head.position=crane.Head;
            cable.position=new Vector3(crane.Head.x,(5+crane.Head.y)/2,crane.Head.z); cable.localScale=new Vector3(.06f,(5-crane.Head.y)/2,.06f);
            footprint.gameObject.SetActive(motion==Motion.Carrying || motion==Motion.Selecting);
            var prediction=motion==Motion.Carrying?crane.Load+crane.LoadVelocity*.55f:target;
            prediction.x=Mathf.Clamp(prediction.x,0,board.Size-1); prediction.z=Mathf.Clamp(prediction.z,0,board.Size-1); prediction.y=.025f; footprint.position=prediction;
            footprint.localScale=new Vector3(.7f+crane.LoadVelocity.magnitude*.1f,.018f,.7f+crane.LoadVelocity.magnitude*.1f);
            pushArrow.gameObject.SetActive(motion==Motion.Carrying && held.Kind==Kind.Slimy);
            if(pushArrow.gameObject.activeSelf) {var d=BoardState.Push(crane.LoadVelocity.x,crane.LoadVelocity.z,pickup,Grid(prediction),NearRestThreshold); pushArrow.position=prediction+new Vector3(d.X,0,d.Y)*.55f; pushArrow.rotation=Quaternion.LookRotation(new Vector3(d.X,0,d.Y));}
            if(motion==Motion.Approaching && Vector3.Distance(crane.Head,target)<.04f) {if(AutoReturn && !ResolveAutoGrab()) return; motion=Motion.Lifting; carryTime=0; target.y=3.5f; crane.Load=actors[held].transform.position; crane.LoadVelocity=Vector3.zero;}
            if(motion==Motion.Lifting || motion==Motion.Carrying)
            {
                if(!AutoReturn || motion==Motion.Carrying) carryTime+=dt; actors[held].transform.position=crane.Load;
                if(motion==Motion.Lifting && Vector3.Distance(crane.Head,target)<.04f) motion=Motion.Carrying;
                if((!Automation && motion==Motion.Carrying && controls.Action && (controls.KeyboardAction || !OverHud())) || carryTime>=HoldLimit) Release();
            }
            if(motion==Motion.Falling)
            {
                releaseVelocity=body.linearVelocity;
                var p=body.position;
                if(p.x<0 || p.x>board.Size-1) {p.x=Mathf.Clamp(p.x,0,board.Size-1); var v=body.linearVelocity; v.x*=-.5f; body.linearVelocity=v;}
                if(p.z<0 || p.z>board.Size-1) {p.z=Mathf.Clamp(p.z,0,board.Size-1); var v=body.linearVelocity; v.z*=-.5f; body.linearVelocity=v;}
                body.position=p;
                quiet=body.linearVelocity.sqrMagnitude<.03f?quiet+dt:0;
                if(impactUnit!=null) contactAge+=dt;
                if(quiet>.15f || elapsed>=2 || p.y<-.2f || AutoReturn && impactUnit!=null && contactAge>.06f) Settle();
            }
        }
        bool OverHud() => controls.Pointer.y>Screen.height-100 || controls.Pointer.x>Screen.width-180 && controls.Pointer.y>Screen.height-200 || controls.Pointer.x<280 && controls.Pointer.y<170;
        Vector3 launchVelocity;
        void Release()
        {
            token=board.CommitRelease(); if(token<0) return;
            RecordExperiment(carryTime>=HoldLimit?"grip-break":"release");
            body=actors[held].GetComponent<Rigidbody>(); body.isKinematic=false; body.linearVelocity=crane.LoadVelocity; launchVelocity=crane.LoadVelocity; releaseVelocity=launchVelocity;
            motion=Motion.Falling; elapsed=quiet=0;
        }
        void Settle()
        {
            var landing=AutoReturn && impactUnit!=null && impactUnit.Alive?impactUnit.Cell:Grid(body.position); body.linearVelocity=Vector3.zero; body.isKinematic=true;
            var direction=BoardState.Push(launchVelocity.x,launchVelocity.z,pickup,landing,NearRestThreshold);
            int captures=board.Captured.Count;
            if(!board.Resolve(token,held,landing,direction,AutoReturn)) return;
            PlayTone(held.Kind==Kind.Heavy?90:420,.14f);
            if(board.Captured.Count>captures) toast="PRIZE GET!  "+board.Captured.Last();
            RecordExperiment(landing.Equals(board.Pit)?"capture":"settled");
            effect=Shape("Impact ripple",PrimitiveType.Cylinder,Position(landing,.035f),new Vector3(.5f,.018f,.5f),held.Kind==Kind.Slimy?new Color(.2f,.8f,.5f):gold,world).transform; effectAge=0;
            if(AutoReturn) {BeginPresentation(); return;}
            CompleteResolution();
        }
        void CompleteResolution()
        {
            foreach(var u in board.Units) displayedHp[u]=u.Hp;
            foreach(var pair in actors) {pair.Value.SetActive(pair.Key.Alive); if(pair.Key.Alive) {pair.Value.transform.position=Position(pair.Key.Cell); pair.Value.transform.rotation=Quaternion.identity; pair.Value.transform.localScale=Vector3.one; pair.Value.GetComponent<Collider>().enabled=true;} }
            held=null; body=null; motion=board.Outcome==Outcome.Playing?Motion.Selecting:Motion.Result; elapsed=resultTime=0;
            ResetAutoPick();
            if(board.Outcome==Outcome.Recruited) toast="GET!  Slimy joins the team.";
            else if(board.Outcome==Outcome.Victory) toast=stage==2?"Three friends. One unforgettable night.":"One more cabinet is still awake…";
            else if(board.Outcome==Outcome.Defeat) toast="A different drop could change everything.";
            if(board.Outcome==Outcome.Victory)
            {
                string key="crane.v1.best."+stage; int best=PlayerPrefs.GetInt(key,int.MaxValue);
                if(board.Turns<best) {if(!Automation) {PlayerPrefs.SetInt(key,board.Turns); PlayerPrefs.Save();} best=board.Turns;}
                mastery=$"{board.Turns} DROPS   ·   PERSONAL BEST {best}";
            }
        }
        void ShowTeam()
        {
            teamShown=true; foreach(var actor in actors.Values) actor.SetActive(false); actors.Clear();
            float center=(board.Size-1)/2f;
            for(int i=0;i<3;i++)
            {
                var u=new Unit{Kind=(Kind)i,Hp=i==2?2:3,Id="celebration"+i,Cell=new Cell(i,1)};
                MakeUnit(u); actors[u].transform.position=new Vector3(center+(i-1)*1.1f,.42f,center);
                actors[u].transform.rotation=Quaternion.Euler(0,-25,0);
                if(i==2) celebrating=actors[u].transform;
            }
            head.gameObject.SetActive(false); cable.gameObject.SetActive(false); footprint.gameObject.SetActive(false); pushArrow.gameObject.SetActive(false);
            PlayTone(700,.25f);
        }
        void PlayTone(float frequency,float duration)
        {
            if(!sound) return; int samples=(int)(44100*duration); var data=new float[samples];
            for(int i=0;i<samples;i++) data[i]=Mathf.Sin(2*Mathf.PI*frequency*i/44100)*.18f*(1-(float)i/samples);
            var clip=AudioClip.Create("Toy chime",samples,1,44100,false); clip.SetData(data,0); audioSource.PlayOneShot(clip); Destroy(clip,duration+1);
        }
        void OnGUI()
        {
            if(board==null) return;
            if(title==null) {title=new GUIStyle(GUI.skin.label){fontSize=26,fontStyle=FontStyle.Bold,font=Font.CreateDynamicFontFromOSFont("Georgia",26)}; text=new GUIStyle(GUI.skin.label){fontSize=16}; small=new GUIStyle(GUI.skin.label){fontSize=13}; title.normal.textColor=cream; text.normal.textColor=cream; small.normal.textColor=cream;}
            GUI.color=Color.white;
            DrawExperiment();
            GUI.Label(new Rect(24,18,360,36),"CRANE TACTICS",title);
            GUI.Label(new Rect(26,56,360,24),stage==1?"PRIZE CHANCE  /  A NEW FRIEND":$"MIDNIGHT SHIFT 0{(stage==0?1:2)}",small);
            GUI.Label(new Rect(Screen.width-290,98,270,25),stage==1?"CATCH SLIMY":$"{board.Units.Count(u=>VisibleHp(u)>0 && u.Enemy)} RIVALS LEFT  ·  {board.Turns} DROPS",text);
            if(!paused && motion!=Motion.Result && !AutoReturn)
            {
                GUI.color=gold; GUI.DrawTexture(new Rect(20,Screen.height-166,255,145),Texture2D.whiteTexture);
                GUI.color=cream; GUI.DrawTexture(new Rect(23,Screen.height-163,249,139),Texture2D.whiteTexture); GUI.color=Color.white;
                text.normal.textColor=new Color(.08f,.2f,.21f); small.normal.textColor=new Color(.08f,.2f,.21f);
                GUI.Label(new Rect(36,Screen.height-154,230,28),held==null?"YOUR NEXT MOVE":held.Kind.ToString().ToUpperInvariant(),text);
                GUI.Label(new Rect(36,Screen.height-124,230,24),held==null?(AutoReturn?PickHint:"Click a toy to grab"):held.Kind==Kind.Slimy?"PUSH 1 TILE":$"IMPACT {held.Damage}",small);
                GUI.Label(new Rect(36,Screen.height-98,230,24),held==null?(AutoReturn?"Hold Space / left mouse":"WASD / arrows + Space also work"):float.IsInfinity(HoldLimit)?"Let the swing settle…":$"GRIP  {Mathf.Max(0,HoldLimit-carryTime):0.0}s",small);
                GUI.Label(new Rect(36,Screen.height-66,230,24),stage==1?"Unlimited tries. Keep going.":"Next? "+board.Plan(),small);
                text.normal.textColor=cream; small.normal.textColor=cream;
                GUI.Label(new Rect(Screen.width/2-130,Screen.height-40,330,24),held==null?"One drop can change everything.":"Click / Space — release your plan.",small);
            }
            if(GUI.Button(new Rect(Screen.width-145,18,120,30),paused?"Resume [Esc]":"Pause [Esc]")) PauseGame(!paused);
            if(GUI.Button(new Rect(Screen.width-145,54,120,30),sound?"Sound: ON":"Sound: OFF")) {sound=!sound; if(!sound) audioSource.Stop();}
            if(!teamShown) foreach(var pair in actors.Where(p=>p.Value.activeSelf)) {int hp=VisibleHp(pair.Key); if(hp<=0 || AutoReturn && pair.Key==held && (motion==Motion.Carrying || motion==Motion.Lifting)) continue; var point=view.WorldToScreenPoint(pair.Value.transform.position+Vector3.up*1.25f); var badge=new Rect(point.x-24,Screen.height-point.y,52,23); GUI.color=new Color(.035f,.1f,.12f,.9f); GUI.DrawTexture(badge,Texture2D.whiteTexture); GUI.color=pair.Key.Enemy?new Color(1,.58f,.5f):Color.white; GUI.Label(new Rect(point.x-20,Screen.height-point.y,70,25),new string('♥',hp),text);} GUI.color=Color.white;
            if(paused || motion==Motion.Result)
            {
                float x=Screen.width/2-210,y=paused?Screen.height/2-95:Screen.height-210; GUI.Box(new Rect(x,y,420,190),"");
                GUI.Label(new Rect(x+20,y+20,390,60),paused?"Take a breath. Your claw can wait.":toast,text);
                if(!paused && board.Outcome==Outcome.Victory) GUI.Label(new Rect(x+20,y+70,390,24),mastery,small);
                if(paused) {if(GUI.Button(new Rect(x+30,y+95,160,40),"Resume")) PauseGame(false); if(GUI.Button(new Rect(x+220,y+95,160,40),"Restart cabinet")) LoadStage(stage);}
                else if(resultTime>1.1f)
                {
                    string action=board.Outcome==Outcome.Defeat?"Try this cabinet again":stage==0?"Enter Prize Chance":stage==1?"Team up — next battle":"Play another night";
                    if(GUI.Button(new Rect(x+45,y+100,330,45),action)) LoadStage(board.Outcome==Outcome.Defeat?stage:stage==2?0:stage+1);
                }
            }
            else if(elapsed<4 && motion==Motion.Selecting) GUI.Label(new Rect(26,108,650,30),toast,text);
        }
        void OnDestroy() {foreach(var material in materials) if(material!=null) Destroy(material); if(floorMaterial!=null) Destroy(floorMaterial); if(softMaterial!=null) Destroy(softMaterial);}
    }
}
