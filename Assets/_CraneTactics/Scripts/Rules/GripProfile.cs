using System;

namespace CraneTactics.Rules
{
    // Deterministic contact proxy shared by the claw and the board solver. No hidden randomness.
    public static class GripProfile
    {
        public const float SlipBelowQuality=.3f;
        static float Clamp01(float v) => v<0?0:v>1?1:v;
        static float Radius(Kind kind) =>
            kind==Kind.Koromochi?.24f:kind==Kind.Heavy?.38f:kind==Kind.Clockwork?.42f:kind==Kind.Slimy?.48f:kind==Kind.Petapetan?.5f:.55f;
        static float BaseSeconds(Kind kind) =>
            kind==Kind.Koromochi?1.2f:kind==Kind.Heavy?2f:kind==Kind.Clockwork?2.4f:kind==Kind.Petapetan?4.4f:kind==Kind.Slimy?5f:3.8f;
        public static float Quality(Kind kind,float offset) => Clamp01(1-offset/Radius(kind));
        public static float Seconds(Kind kind,float quality)
        {
            float q=Clamp01(quality);
            return BaseSeconds(kind)*(.65f+(1.6f-.65f)*q);
        }
    }
}
