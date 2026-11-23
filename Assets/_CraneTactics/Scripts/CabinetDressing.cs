using System.Collections.Generic;
using System.Linq;
using CraneTactics.Rules;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Terrain=CraneTactics.Rules.Terrain;

namespace CraneTactics
{
    // Presentation of a cabinet's identity. Nothing here changes BoardState.
    public sealed partial class OneNightGame
    {
        readonly Dictionary<Cell,Renderer> tileViews=new Dictionary<Cell,Renderer>();
        readonly Dictionary<Unit,Transform> visuals=new Dictionary<Unit,Transform>();
        readonly List<Material> beltMaterials=new List<Material>();
        readonly List<LineRenderer> previewLines=new List<LineRenderer>();
        readonly List<Popup> popups=new List<Popup>();
        readonly Material[] bulbMaterials=new Material[3];
        int previewRevision=-1; bool previewWasVisible; Volume postVolume; VolumeProfile postProfile;
        sealed class Popup {public Vector3 At; public string Label; public Color Color; public float Age;}
        Material Unlit(Color color)
        {
            var source=Resources.Load<Material>("RouteGuide");
            var material=source!=null?new Material(source):Mat(color); material.color=color;
            if(source!=null) materials.Add(material);
            return material;
        }
        void DressCabinet()
        {
            previewRevision=-1; previewLines.Clear(); beltMaterials.Clear();
            RefreshTiles();
            TintShell();
            AddMarquee();
            AddBulbs();
            AddMagnetFields();
            for(int i=0;i<14;i++) previewLines.Add(MakePreviewLine());
            SetupBloom();
        }
        static string TileTexture(Terrain t) =>
            t==Terrain.Frosting?"frosting":t==Terrain.Jelly?"jelly":t==Terrain.Magnet?"magnet":t==Terrain.Gum?"gum":t==Terrain.Floor?null:"belt";
        void RefreshTiles()
        {
            foreach(var pair in tileViews)
            {
                var terrain=board.TerrainAt(pair.Key); string file=TileTexture(terrain);
                if(file==null) continue;
                var texture=Resources.Load<Texture2D>("Terrain/"+file); var material=pair.Value.sharedMaterial;
                if(texture==null || material.mainTexture==texture) continue;
                material.mainTexture=texture; material.color=Color.white;
                material.SetFloat("_Smoothness",terrain==Terrain.Frosting?.85f:terrain==Terrain.Jelly?.7f:.3f);
                var d=BoardState.BeltDirection(terrain);
                if(d.X!=0 || d.Y!=0)
                {
                    pair.Value.transform.rotation=Quaternion.Euler(0,d.Y>0?180:d.Y<0?0:d.X>0?270:90,0);
                    beltMaterials.Add(material);
                }
            }
        }
        void TintShell()
        {
            if(cabinetProfile==CabinetProfile.Classic) return;
            var shell=world.Find("Cabinet(Clone)"); if(shell==null) return;
            foreach(var renderer in shell.GetComponentsInChildren<Renderer>())
            {
                var instances=renderer.materials;
                foreach(var m in instances)
                {
                    materials.Add(m);
                    if(m.name.StartsWith("teal")) m.color=cabinetProfile.Body;
                    else if(m.name.StartsWith("brass")) m.color=cabinetProfile.Trim;
                }
            }
        }
        void AddMarquee()
        {
            if(cabinetProfile==CabinetProfile.Classic) return;
            var shell=world.Find("Cabinet(Clone)"); var art=Resources.Load<Texture2D>("Marquee/"+cabinetProfile.Marquee);
            if(shell==null || art==null) return;
            var poster=GameObject.CreatePrimitive(PrimitiveType.Quad); poster.name="Marquee poster"; Destroy(poster.GetComponent<Collider>());
            poster.transform.SetParent(shell,false); poster.transform.localPosition=new Vector3(2.5f,1.95f,5.6f); poster.transform.localScale=new Vector3(4.6f,2.65f,1);
            var material=Unlit(Color.white); material.mainTexture=art; poster.GetComponent<Renderer>().sharedMaterial=material;
        }
        void AddBulbs()
        {
            for(int i=0;i<bulbMaterials.Length;i++) bulbMaterials[i]=Unlit(cabinetProfile.Lamp);
            float n=board.Size, low=-.6f, high=n-.4f; int index=0;
            void Bulb(float x,float z)
            {
                var bulb=Shape("Marquee bulb",PrimitiveType.Sphere,new Vector3(x,.46f,z),Vector3.one*.13f,cabinetProfile.Lamp,world);
                bulb.GetComponent<Renderer>().sharedMaterial=bulbMaterials[index++%bulbMaterials.Length];
            }
            for(float x=low;x<=high+.01f;x+=.5f) Bulb(x,low);
            for(float z=low+.5f;z<=high+.01f;z+=.5f) Bulb(high,z);
            for(float z=low+.5f;z<high;z+=.5f) Bulb(low,z);
        }
        void AddMagnetFields()
        {
            foreach(var pair in board.TerrainCells.Where(p=>p.Value==Terrain.Magnet))
            {
                var line=new GameObject("Magnet reach").AddComponent<LineRenderer>(); line.transform.SetParent(world);
                line.widthMultiplier=.03f; line.sharedMaterial=Unlit(new Color(.45f,.85f,1)); line.positionCount=5;
                float reach=3,h=.05f; var c=Position(pair.Key,h);
                Vector3 clampX(float dx) => new Vector3(Mathf.Clamp(c.x+dx,-.3f,board.Size-.7f),h,c.z);
                Vector3 clampZ(float dz) => new Vector3(c.x,h,Mathf.Clamp(c.z+dz,-.3f,board.Size-.7f));
                line.SetPositions(new[]{clampX(-reach),clampX(reach),c,clampZ(-reach),clampZ(reach)});
            }
        }
        LineRenderer MakePreviewLine()
        {
            var line=new GameObject("Tactical preview").AddComponent<LineRenderer>(); line.transform.SetParent(world);
            line.widthMultiplier=.075f; line.numCapVertices=2; line.positionCount=5; line.enabled=false;
            return line;
        }
        void DrawPreview(ref int index,Cell from,Cell to,Color color)
        {
            if(index>=previewLines.Count) return;
            Vector3 a=Position(from,.09f), b=Position(to,.09f), d=(b-a).normalized, side=new Vector3(-d.z,0,d.x);
            if(d==Vector3.zero) return;
            var tip=b-d*.12f; var line=previewLines[index++];
            if(line.sharedMaterial==null || line.sharedMaterial.color!=color)
            {
                line.sharedMaterial=Unlit(color);
            }
            line.SetPositions(new[]{a+d*.2f,tip,tip-d*.24f+side*.17f,tip,tip-d*.24f-side*.17f}); line.enabled=true;
        }
        // Spatial forecast shares the board's own planners, so what is drawn is what will run.
        void RefreshPreview()
        {
            bool visible=motion==Motion.Selecting && AutoReturn;
            if(visible==previewWasVisible && previewRevision==board.Revision) return;
            previewWasVisible=visible; previewRevision=board.Revision;
            foreach(var line in previewLines) line.enabled=false;
            if(!visible || board.Outcome!=Outcome.Playing) return;
            int i=0;
            var enemy=new Color(1,.3f,.25f); var ally=new Color(.35f,1,.6f);
            var plan=board.Plan();
            if(plan.Actor!=null) DrawPreview(ref i,plan.Actor.Cell,plan.Attacks?plan.Target.Cell:plan.Destination,enemy);
            foreach(var u in board.Units.Where(x=>x.Alive))
            {
                if(u.Kind==Kind.Clockwork && !board.IsStuck(u)) {var end=u.Cell; foreach(var c in board.ClockworkPath(u)) end=c; if(!end.Equals(u.Cell)) DrawPreview(ref i,u.Cell,end,u.Enemy?enemy:ally);}
                if(u.Metal) {var pull=board.MagnetPull(u); if(pull!=null) DrawPreview(ref i,u.Cell,u.Cell+pull.Value,new Color(.45f,.85f,1));}
                var belt=BoardState.BeltDirection(board.TerrainAt(u.Cell));
                if(belt.X!=0 || belt.Y!=0) DrawPreview(ref i,u.Cell,u.Cell+belt,new Color(1,.8f,.2f));
            }
        }
        void TickDressing(float dt)
        {
            float t=Time.time;
            for(int i=0;i<bulbMaterials.Length;i++)
            {
                if(bulbMaterials[i]==null) continue;
                float lit=Mathf.Clamp01(Mathf.Sin(t*5-i*2.094f)*1.4f+.2f);
                bulbMaterials[i].color=Color.Lerp(cabinetProfile.Lamp*.28f,cabinetProfile.Lamp*2.2f,lit);
            }
            foreach(var m in beltMaterials) if(m!=null) m.mainTextureOffset=new Vector2(0,-t*.6f);
            int order=0;
            foreach(var pair in visuals)
            {
                if(pair.Value==null) continue;
                float wave=Mathf.Sin(t*2.4f+order++*1.3f);
                bool soft=pair.Key.Kind==Kind.Slimy || pair.Key.Kind==Kind.Koromochi || pair.Key.Kind==Kind.Petapetan;
                float amount=soft?.035f:.012f;
                pair.Value.localScale=new Vector3(1-wave*amount*.5f,1+wave*amount,1-wave*amount*.5f);
            }
            RefreshPreview();
            for(int i=popups.Count-1;i>=0;i--) {popups[i].Age+=dt; if(popups[i].Age>.9f) popups.RemoveAt(i);}
        }
        void AddPopup(Vector3 at,string label,Color color) => popups.Add(new Popup {At=at+Vector3.up*.9f,Label=label,Color=color});
        void DrawWorldNotes()
        {
            int previous=title.fontSize; var previousColor=title.normal.textColor;
            title.fontSize=30;
            foreach(var popup in popups)
            {
                var p=view.WorldToScreenPoint(popup.At+Vector3.up*popup.Age*.8f); if(p.z<=0) continue;
                title.normal.textColor=new Color(popup.Color.r,popup.Color.g,popup.Color.b,1-popup.Age/.9f);
                GUI.Label(new Rect(p.x-30,Screen.height-p.y-20,80,40),popup.Label,title);
            }
            title.fontSize=previous; title.normal.textColor=previousColor;
            if(!AutoReturn || paused || motion==Motion.Result) return;
            foreach(var unit in board.Units.Where(u=>u.Alive && u.Kind==Kind.Clockwork))
                if(motion==Motion.Selecting) WorldTag(Position(unit.Cell,.0f)+Vector3.back*.55f,board.IsStuck(unit)?"KEY HELD":$"DASH IN {unit.Wind}",unit.Enemy?new Color(1,.6f,.5f):new Color(.5f,1,.7f),78);
            if(board.Turns!=0 || motion!=Motion.Selecting) return;
            var shown=new HashSet<Terrain>();
            foreach(var pair in board.TerrainCells)
            {
                var kind=pair.Value; string belt=kind==Terrain.BeltNorth||kind==Terrain.BeltSouth||kind==Terrain.BeltEast||kind==Terrain.BeltWest?"BELT: MOVES 1 EACH TURN":null;
                var key=belt!=null?Terrain.BeltNorth:kind;
                if(!shown.Add(key)) continue;
                string label=belt??(kind==Terrain.Frosting?"ICING: SLIDES":kind==Terrain.Jelly?"JELLY: BOUNCES +2":kind==Terrain.Magnet?"MAGNET: PULLS METAL":"GUM: STUCK");
                WorldTag(Position(pair.Key,.05f)+Vector3.forward*.1f,label,new Color(1,.9f,.7f),label.Length*8+14);
            }
        }
        // Soft bloom sells the lit bulbs and the chute. The profile is owned here and freed with the game.
        void SetupBloom()
        {
            if(postVolume!=null || view==null) return;
            var data=view.GetUniversalAdditionalCameraData(); data.renderPostProcessing=true;
            postProfile=ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom=postProfile.Add<Bloom>(true); bloom.threshold.Override(.95f); bloom.intensity.Override(.8f); bloom.scatter.Override(.6f);
            var holder=new GameObject("Arcade bloom"); holder.transform.SetParent(transform);
            postVolume=holder.AddComponent<Volume>(); postVolume.isGlobal=true; postVolume.sharedProfile=postProfile;
        }
    }
}
