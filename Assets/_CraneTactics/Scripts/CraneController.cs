using UnityEngine;

namespace CraneTactics
{
    // Presentation motion is continuous; board rules consume only the final settled cell.
    public sealed class CraneController
    {
        public Vector3 Head, Load, LoadVelocity;
        public float Speed=3.2f, Spring=18f, Damping=4.8f;
        public void Reset(Vector3 position) {Head=position; Load=position+Vector3.down*.65f; LoadVelocity=Vector3.zero;}
        public void Tick(Vector3 target,float dt)
        {
            Head=Vector3.MoveTowards(Head,target,Speed*dt);
            var remaining=dt;
            while(remaining>0) {var step=Mathf.Min(remaining,1f/120); remaining-=step; LoadVelocity+=((Head+Vector3.down*.65f-Load)*Spring-LoadVelocity*Damping)*step; Load+=LoadVelocity*step;}
        }
    }
}
