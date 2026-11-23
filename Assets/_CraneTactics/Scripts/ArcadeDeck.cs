using System.Collections.Generic;
using System.Linq;
using CraneTactics.Rules;
using UnityEngine;

namespace CraneTactics
{
    // Right doubles as button 1 and Depth as button 2 while aiming; all four steer while carrying.
    public enum DeckButton { None, Right, Depth, Drop, Left, Near }

    // Player-facing buttons are the cabinet's own glowing push buttons, not generic UI.
    public sealed partial class OneNightGame
    {
        const float BaseDeckHeight=118, BaseDomeSize=112;
        static float DeckScale => Mathf.Clamp(Screen.height/800f,.72f,1.4f);
        public static float DeckHeight => BaseDeckHeight*DeckScale;
        Texture2D domeCyan, domeAmber, domeRed, domeMint, consolePlate, glow;
        GUIStyle cyanPill, amberPill, redPill, mintPill, domeNumber, domeLabel, medalLabel, wrapped;
        float pressRight, pressDepth, pressDrop, pressLeft, pressNear;
        bool floorOpen, labOpen;
        int deckPressedFrame=-1;

        public static Rect DeckButtonRect(DeckButton button,float screenWidth,float screenHeight)
        {
            float scale=Mathf.Clamp(screenHeight/800f,.72f,1.4f), size=BaseDomeSize*scale, gap=16*scale;
            float baseline=screenHeight-(BaseDeckHeight*scale-size)/2-size+2*scale;
            float dropX=screenWidth-size-22*scale;
            // Left to right: ←  ↓  1 →  2 ↑  DROP. Each axis shares a color: cyan across, amber in depth.
            int slot=button==DeckButton.Drop?0:button==DeckButton.Depth?1:button==DeckButton.Right?2:button==DeckButton.Near?3:4;
            return new Rect(dropX-slot*(size+gap),baseline,size,size);
        }
        public static readonly DeckButton[] SteerButtons={DeckButton.Left,DeckButton.Near,DeckButton.Right,DeckButton.Depth};
        public static Vector2Int SteerVector(DeckButton button) =>
            button==DeckButton.Left?Vector2Int.left:button==DeckButton.Near?Vector2Int.down:button==DeckButton.Right?Vector2Int.right:button==DeckButton.Depth?Vector2Int.up:Vector2Int.zero;
        bool Carrying => AutoReturn && !paused && !floorOpen && motion==Motion.Carrying;
        public bool DeckLit(DeckButton button) => button!=DeckButton.None && (button==ActiveDeckButton || Carrying && SteerButtons.Contains(button));
        // Domes are round: corners of the square sprite are not part of the button.
        public static bool DeckHit(Rect rect,Vector2 guiPoint) => Vector2.Distance(rect.center,guiPoint)<=rect.width*.47f;
        public DeckButton ActiveDeckButton => !AutoReturn || paused || floorOpen?DeckButton.None:motion==Motion.Selecting?(pickAxis==0?DeckButton.Right:DeckButton.Depth):motion==Motion.Carrying?DeckButton.Drop:DeckButton.None;
        bool PointerOnActiveDeckButton
        {
            get
            {
                var active=ActiveDeckButton; if(active==DeckButton.None) return false;
                return DeckHit(DeckButtonRect(active,Screen.width,Screen.height),new Vector2(controls.Pointer.x,Screen.height-controls.Pointer.y));
            }
        }
        static Texture2D LoadUi(string name) => Resources.Load<Texture2D>("UI/"+name);
        void SetupGlow()
        {
            const int size=128;
            glow=new Texture2D(size,size,TextureFormat.RGBA32,false) {hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float d=Vector2.Distance(new Vector2(x,y),new Vector2(size/2f,size/2f))/(size/2f);
                float a=Mathf.Clamp01(1-d); pixels[y*size+x]=new Color(1,1,1,a*a);
            }
            glow.SetPixels(pixels); glow.Apply();
            domeCyan=LoadUi("button-cyan"); domeAmber=LoadUi("button-amber"); domeRed=LoadUi("button-red"); domeMint=LoadUi("button-mint"); consolePlate=LoadUi("console-plate");
        }
        GUIStyle MakePill(string texture,Color textColor)
        {
            var style=new GUIStyle(GUI.skin.button) {fontSize=14,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,padding=new RectOffset(20,20,2,2)};
            var background=LoadUi(texture);
            if(background!=null)
            {
                style.normal.background=style.hover.background=style.active.background=style.focused.background=background;
                style.border=new RectOffset(21,21,0,0);
            }
            style.normal.textColor=style.hover.textColor=style.active.textColor=style.focused.textColor=textColor;
            return style;
        }
        void SetupDeckStyles()
        {
            var ink=new Color(.05f,.08f,.1f);
            cyanPill=MakePill("pill-cyan",ink); amberPill=MakePill("pill-amber",ink); redPill=MakePill("pill-red",Color.white); mintPill=MakePill("pill-mint",ink);
            domeNumber=new GUIStyle(GUI.skin.label) {fontSize=28,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
            domeLabel=new GUIStyle(GUI.skin.label) {fontSize=16,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
            medalLabel=new GUIStyle(GUI.skin.label) {fontSize=12,fontStyle=FontStyle.Bold,alignment=TextAnchor.UpperCenter};
            wrapped=new GUIStyle(GUI.skin.label) {fontSize=13,wordWrap=true};
            wrapped.normal.textColor=CabinetProfile.Classic.TileA;
        }
        // A pill-shaped lit push button: hover lights it up, press sinks it.
        bool ArcadePill(Rect rect,string label,GUIStyle style)
        {
            bool over=rect.Contains(Event.current.mousePosition), down=over && GUIUtility.hotControl!=0;
            var previous=GUI.color;
            if(over) {GUI.color=new Color(1,1,1,.5f); GUI.DrawTexture(new Rect(rect.x-14,rect.y-14,rect.width+28,rect.height+28),glow); }
            GUI.color=over?Color.white:new Color(.86f,.86f,.86f);
            if(down) rect=new Rect(rect.x+1,rect.y+2,rect.width-2,rect.height-2);
            bool clicked=GUI.Button(rect,label,style);
            GUI.color=previous;
            return clicked;
        }
        void DrawDome(DeckButton button,Texture2D dome,Color lightColor,string number,string label,ref float press,bool lit,bool pressedNow)
        {
            var rect=DeckButtonRect(button,Screen.width,Screen.height);
            press=Mathf.MoveTowards(press,pressedNow?1:0,Time.unscaledDeltaTime*14);
            float pulse=.65f+.35f*Mathf.Sin(Time.unscaledTime*(pressedNow?16:6));
            if(lit)
            {
                float bloom=rect.width*.42f*(pressedNow?1.2f:1);
                GUI.color=new Color(lightColor.r,lightColor.g,lightColor.b,pulse*.85f);
                GUI.DrawTexture(new Rect(rect.x-bloom,rect.y-bloom,rect.width+2*bloom,rect.height+2*bloom),glow);
            }
            float inset=rect.width*.045f*press;
            var body=new Rect(rect.x+inset,rect.y+inset,rect.width-2*inset,rect.height-2*inset);
            GUI.color=lit?Color.white:new Color(.27f,.27f,.32f);
            if(dome!=null) GUI.DrawTexture(body,dome);
            else {GUI.color=lit?lightColor:lightColor*.3f; GUI.DrawTexture(body,glow);}
            var textColor=lit?new Color(.04f,.05f,.07f,.95f):new Color(.75f,.75f,.75f,.55f);
            domeNumber.normal.textColor=textColor; domeLabel.normal.textColor=textColor;
            GUI.color=Color.white;
            if(string.IsNullOrEmpty(label)) GUI.Label(body,number,domeNumber);
            else
            {
                float s=DeckScale;
                GUI.Label(new Rect(body.x,body.y+body.height*.2f,body.width,body.height*.36f),number,domeNumber);
                GUI.Label(new Rect(body.x,body.y+body.height*.54f,body.width,body.height*.26f),label,domeLabel);
            }
        }
        void DrawDeck(string state,string instruction,string detail)
        {
            float s=DeckScale, h=DeckHeight, y=Screen.height-h;
            if(consolePlate!=null) {GUI.color=Color.white; GUI.DrawTexture(new Rect(0,y,Screen.width,h),consolePlate,ScaleMode.StretchToFill);}
            else {GUI.color=new Color(.05f,.13f,.15f); GUI.DrawTexture(new Rect(0,y,Screen.width,h),Texture2D.whiteTexture);}
            GUI.color=Color.white;
            var active=ActiveDeckButton;
            bool holding=controls.Held;
            bool Steering(DeckButton b) => Carrying && SteerDirection==SteerVector(b);
            bool rightDown=active==DeckButton.Right && (axisMoving || holding && PointerOnActiveDeckButton) || Steering(DeckButton.Right);
            bool depthDown=active==DeckButton.Depth && (axisMoving || holding && PointerOnActiveDeckButton) || Steering(DeckButton.Depth);
            bool dropDown=active==DeckButton.Drop && holding && PointerOnActiveDeckButton;
            Color cyanLight=new Color(.25f,.9f,1), amberLight=new Color(1,.68f,.2f);
            DrawDome(DeckButton.Left,domeCyan,cyanLight,"←","",ref pressLeft,DeckLit(DeckButton.Left),Steering(DeckButton.Left));
            DrawDome(DeckButton.Near,domeAmber,amberLight,"↓","",ref pressNear,DeckLit(DeckButton.Near),Steering(DeckButton.Near));
            DrawDome(DeckButton.Right,domeCyan,cyanLight,Carrying?"→":"1",Carrying?"":"→",ref pressRight,DeckLit(DeckButton.Right),rightDown);
            DrawDome(DeckButton.Depth,domeAmber,amberLight,Carrying?"↑":"2",Carrying?"":"↑",ref pressDepth,DeckLit(DeckButton.Depth),depthDown);
            DrawDome(DeckButton.Drop,domeRed,new Color(1,.22f,.2f),"DROP","",ref pressDrop,DeckLit(DeckButton.Drop),dropDown);
            float left=26*s, textWidth=DeckButtonRect(DeckButton.Left,Screen.width,Screen.height).x-left-24*s;
            int stateSize=title.fontSize; title.fontSize=Mathf.RoundToInt(22*s);
            GUI.Label(new Rect(left,y+10*s,textWidth,36*s),state,title); title.fontSize=stateSize;
            GUI.Label(new Rect(left,y+48*s,textWidth,26*s),instruction,text);
            GUI.Label(new Rect(left,y+78*s,textWidth,24*s),detail,small);
        }
        public static string DisplayName(Kind kind)
        {
            switch(kind)
            {
                case Kind.Knight: return "Ring Knight"; case Kind.Clockwork: return "Clockwork Robot";
                default: return kind.ToString();
            }
        }
        public static string AbilityText(Kind kind)
        {
            switch(kind)
            {
                case Kind.Knight: return "IMPACT 1, strikes a neighbor";
                case Kind.Heavy: return "IMPACT 2, slips fast";
                case Kind.Slimy: return "PUSH 1 TILE";
                case Kind.Koromochi: return "ROLLS until stopped, pushes";
                case Kind.Petapetan: return "LEAVES GUM where it lands";
                case Kind.Clockwork: return "DASHES the way you drop it";
                default: return "IMPACT 1, reusable as a projectile";
            }
        }
        readonly struct MedalSlot {public readonly Medal Medal; public readonly string Label; public MedalSlot(Medal m,string l) {Medal=m; Label=l;}}
        Medal earnedMedals;
        static string MedalKey(int stage) => "crane.v2.medals."+stage;
        public static Medal StoredMedals(int stage) => (Medal)PlayerPrefs.GetInt(MedalKey(stage),0);
        void RecordMedals()
        {
            earnedMedals=board.Medals;
            if(earnedMedals==Medal.None || Automation || experimentCase!=0) return;
            PlayerPrefs.SetInt(MedalKey(stage),(int)(StoredMedals(stage)|earnedMedals)); PlayerPrefs.Save();
        }
        void DrawMedal(Rect rect,bool lit,string label)
        {
            GUI.color=lit?Color.white:new Color(.25f,.25f,.28f);
            if(domeAmber!=null) GUI.DrawTexture(new Rect(rect.center.x-rect.height*.35f,rect.y,rect.height*.7f,rect.height*.7f),domeAmber);
            GUI.color=Color.white;
            medalLabel.normal.textColor=lit?new Color(1,.86f,.5f):new Color(.6f,.6f,.6f);
            GUI.Label(new Rect(rect.x-20,rect.y+rect.height*.72f,rect.width+40,22),label,medalLabel);
        }
        void DrawPanel(Rect rect)
        {
            GUI.color=new Color(.85f,.59f,.2f,.95f); GUI.DrawTexture(new Rect(rect.x-3,rect.y-3,rect.width+6,rect.height+6),Texture2D.whiteTexture);
            GUI.color=new Color(.03f,.08f,.09f,.98f); GUI.DrawTexture(rect,Texture2D.whiteTexture); GUI.color=Color.white;
        }
        void DrawResultPanel()
        {
            float w=480,h=260,x=Screen.width/2-w/2,y=Screen.height-h-24; var panel=new Rect(x,y,w,h); DrawPanel(panel);
            GUI.Label(new Rect(x+24,y+14,w-48,52),toast,text);
            bool won=board.Outcome==Outcome.Victory || board.Outcome==Outcome.Recruited;
            if(won)
            {
                string parLabel=board.Par>0?$"PAR {board.Par}":"PAR";
                var slots=board.IsPrize?new[]{new MedalSlot(Medal.Clear,"GET"),new MedalSlot(Medal.Par,"ONE DROP")}:new[]{new MedalSlot(Medal.Clear,"CLEAR"),new MedalSlot(Medal.Par,parLabel),new MedalSlot(Medal.TrickShot,"TRICK SHOT")};
                float cell=w/(slots.Length+1);
                for(int i=0;i<slots.Length;i++) DrawMedal(new Rect(x+cell*(i+1)-40,y+62,80,68),(earnedMedals&slots[i].Medal)!=0,slots[i].Label);
                if(board.Outcome==Outcome.Victory) GUI.Label(new Rect(x+24,y+152,w-48,22),mastery+(board.DirectCaptures==0?"":"   ·   TRICK SHOT: win without carrying a rival home"),small);
            }
            if(resultTime<=1.1f) return;
            bool cabinetDone=won && (stage==2 || !board.IsPrize && CabinetCatalog.NextInCabinet(stage)<0);
            string action=board.Outcome==Outcome.Defeat?"TRY THIS CABINET AGAIN":stage==0?"ENTER PRIZE CHANCE":stage==1?"TEAM UP: NEXT BATTLE":cabinetDone?"ARCADE FLOOR":"NEXT";
            var style=board.Outcome==Outcome.Defeat?redPill:mintPill;
            if(cabinetDone)
            {
                if(ArcadePill(new Rect(x+24,y+196,w/2-36,44),stage==2?"PLAY ANOTHER NIGHT":"REPLAY CABINET",amberPill)) LoadStage(stage==2?0:CabinetCatalog.ForStage(stage).Stages[0]);
                if(ArcadePill(new Rect(x+w/2+12,y+196,w/2-36,44),action,style)) OpenFloor(true);
            }
            else if(ArcadePill(new Rect(x+70,y+196,w-140,44),action,style)) NextCabinet();
        }
        void DrawPauseMenu()
        {
            float w=420,h=250,x=Screen.width/2-w/2,y=Screen.height/2-h/2; DrawPanel(new Rect(x,y,w,h));
            GUI.Label(new Rect(x+24,y+16,w-48,30),"Take a breath. Your claw can wait.",text);
            if(ArcadePill(new Rect(x+24,y+56,w/2-30,46),"RESUME",mintPill)) PauseGame(false);
            if(ArcadePill(new Rect(x+w/2+6,y+56,w/2-30,46),"RESTART",amberPill)) LoadStage(stage);
            if(ArcadePill(new Rect(x+24,y+112,w-48,46),"ARCADE FLOOR",cyanPill)) OpenFloor(true);
            if(ArcadePill(new Rect(x+24,y+168,w/2-30,46),shakeEnabled?"SHAKE ON":"SHAKE OFF",amberPill)) shakeEnabled=!shakeEnabled;
            if(ArcadePill(new Rect(x+w/2+6,y+168,w/2-30,46),"LAB",amberPill)) labOpen=!labOpen;
            if(labOpen) DrawLab(24,130);
        }
        public void OpenFloor(bool open) {floorOpen=open; waitForRelease=true;}
        public bool FloorOpen => floorOpen;
        public Vector3 AimPoint => target;
        public void LoadStageForTests(int value) {experimentCase=0; cabinetOverride=null; floorOpen=false; AutoReturn=true; LoadStage(value);}
        public void LoadCabinet(int index)
        {
            experimentCase=0; cabinetOverride=null; floorOpen=false; AutoReturn=true;
            LoadStage(CabinetCatalog.Cabinets[Mathf.Clamp(index,0,CabinetCatalog.Cabinets.Length-1)].Stages[0]);
        }
        int MedalTotal(CabinetDefinition cabinet) {int total=0; foreach(int s in cabinet.Stages) {var m=StoredMedals(s); total+=(m&Medal.Clear)!=0?1:0; total+=(m&Medal.Par)!=0?1:0; total+=(m&Medal.TrickShot)!=0?1:0;} return total;}
        int MedalMax(CabinetDefinition cabinet) => cabinet.Stages.Sum(s=>(s<3?s==1:CabinetCatalog.Stage(s).Prize)?2:3);
        void DrawFloor()
        {
            GUI.color=new Color(.015f,.03f,.04f,1); GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture); GUI.color=Color.white;
            GUI.Label(new Rect(28,20,700,40),"THE ARCADE AFTER MIDNIGHT",title);
            GUI.Label(new Rect(30,62,800,24),"Every cabinet is awake. Choose where your claw goes next.",text);
            var cabinets=CabinetCatalog.Cabinets; int count=cabinets.Length; float gap=14, margin=24;
            float w=(Screen.width-2*margin-gap*(count-1))/count, h=Mathf.Min(Screen.height-150,w*.75f+272), top=(Screen.height-h)/2+30;
            for(int i=0;i<count;i++)
            {
                var c=cabinets[i]; var profile=CabinetProfile.ForId(c.Id); float x=margin+i*(w+gap);
                GUI.color=profile.Trim; GUI.DrawTexture(new Rect(x-2,top-2,w+4,h+4),Texture2D.whiteTexture);
                GUI.color=profile.Backdrop; GUI.DrawTexture(new Rect(x,top,w,h),Texture2D.whiteTexture); GUI.color=Color.white;
                var art=Resources.Load<Texture2D>("Marquee/"+c.Id); float artHeight=w*.75f;
                if(art!=null) GUI.DrawTexture(new Rect(x,top,w,artHeight),art,ScaleMode.ScaleAndCrop);
                GUI.Label(new Rect(x+10,top+artHeight+10,w-20,28),profile.Name,text);
                GUI.Label(new Rect(x+10,top+artHeight+36,w-20,56),c.Tagline,wrapped);
                GUI.Label(new Rect(x+10,top+artHeight+92,w-20,22),"FRIEND  "+c.Friend,small);
                GUI.Label(new Rect(x+10,top+artHeight+114,w-20,22),c.Machines,small);
                GUI.Label(new Rect(x+10,top+h-92,w-20,24),$"MEDALS  {MedalTotal(c)} / {MedalMax(c)}",small);
                if(ArcadePill(new Rect(x+12,top+h-60,w-24,46),"PLAY",i==0?amberPill:mintPill)) LoadCabinet(i);
            }
            if(ArcadePill(new Rect(Screen.width-170,18,146,40),"BACK",amberPill)) OpenFloor(false);
        }
    }
}
