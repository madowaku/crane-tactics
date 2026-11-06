namespace CraneTactics
{
    // Immutable tuning data shared by the controller, floor and preview.
    public sealed class CabinetProfile
    {
        public readonly string Name;
        public readonly float ReturnSpeed, FloorFriction, Bounce;
        CabinetProfile(string name,float speed,float friction,float bounce)
        {Name=name; ReturnSpeed=speed; FloorFriction=friction; Bounce=bounce;}
        public static readonly CabinetProfile Classic=new CabinetProfile("CLASSIC",1.7f,.6f,0);
        public static readonly CabinetProfile Sweets=new CabinetProfile("SWEETS",1.7f,.08f,.12f);
    }
    public enum ClawEvent { Approach, GrabSuccess, GrabFailure, Release, GripBreak, Impact, Settled, Capture }
}
