using CraneTactics.Rules;
using UnityEngine;

namespace CraneTactics
{
    // Immutable tuning and look shared by the controller, floor and preview.
    public sealed class CabinetProfile
    {
        public readonly string Name,Marquee;
        public readonly float ReturnSpeed, FloorFriction, Bounce, Spring, Damping;
        public readonly Color Body, Trim, TileA, TileB, Backdrop, Lamp;
        CabinetProfile(string id,string name,float friction,float bounce,float spring,float damping,Color body,Color trim,Color tileA,Color tileB,Color backdrop,Color lamp)
        {
            Name=name; Marquee=id; FloorFriction=friction; Bounce=bounce; Spring=spring; Damping=damping;
            Body=body; Trim=trim; TileA=tileA; TileB=tileB; Backdrop=backdrop; Lamp=lamp;
            foreach(var cabinet in CabinetCatalog.Cabinets) if(cabinet.Id==id) ReturnSpeed=cabinet.ReturnSpeed;
        }
        static Color Hex(string hex) {ColorUtility.TryParseHtmlString(hex,out var c); return c;}
        static readonly Color Cream=new Color(.86f,.81f,.66f), Teal=new Color(.12f,.32f,.34f), Gold=new Color(.85f,.59f,.2f);
        public static readonly CabinetProfile Classic=new CabinetProfile("classic","CLASSIC",.6f,0,18,4.8f,Teal,Gold,Cream,Color.Lerp(Cream,Teal,.2f),new Color(.035f,.075f,.09f),Hex("#FFC870"));
        // Loose springs make candy swing wide; the lab's stiff arm holds a straighter line.
        public static readonly CabinetProfile Sweets=new CabinetProfile("sweets","SWEETS",.35f,.12f,14,3.4f,Hex("#C2557F"),Hex("#FFD9E6"),Hex("#FFF1E2"),Hex("#F8D8DC"),Hex("#1E0B18"),Hex("#FF8CC0"));
        public static readonly CabinetProfile Magnet=new CabinetProfile("magnet","MAGNET LAB",.45f,0,26,7f,Hex("#3A4654"),Hex("#D6372E"),Hex("#C4CBD1"),Hex("#A8B2BA"),Hex("#07101A"),Hex("#6FD8FF"));
        public static readonly CabinetProfile Factory=new CabinetProfile("factory","GUM FACTORY",.6f,.05f,18,4.8f,Hex("#6B2A78"),Hex("#F5B72A"),Hex("#EADCF0"),Hex("#D3BFDD"),Hex("#120818"),Hex("#FF5FC8"));
        public static readonly CabinetProfile Jackpot=new CabinetProfile("jackpot","JACKPOT",.4f,.05f,16,4f,Hex("#3B2A10"),Hex("#FFC629"),Hex("#F0E1B4"),Hex("#D9C27F"),Hex("#120B02"),Hex("#FFE27A"));
        public static CabinetProfile ForId(string id) => id=="sweets"?Sweets:id=="magnet"?Magnet:id=="factory"?Factory:id=="jackpot"?Jackpot:Classic;
    }
    public enum ClawEvent { Approach, GrabSuccess, GrabFailure, Release, GripBreak, Impact, Settled, Capture }
}
