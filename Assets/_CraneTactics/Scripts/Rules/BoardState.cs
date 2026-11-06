using System;
using System.Collections.Generic;
using System.Linq;

namespace CraneTactics.Rules
{
    public enum Kind { Knight, Heavy, Slimy, Scout, Guard }
    public enum Outcome { Playing, Victory, Defeat, Recruited }
    public struct Cell : IEquatable<Cell>
    {
        public int X, Y;
        public Cell(int x, int y) { X = x; Y = y; }
        public bool Equals(Cell c) => X == c.X && Y == c.Y;
        public override bool Equals(object o) => o is Cell c && Equals(c);
        public override int GetHashCode() => X * 397 ^ Y;
        public int Distance(Cell c) => Math.Abs(X-c.X)+Math.Abs(Y-c.Y);
        public override string ToString() => $"({X},{Y})";
        public static Cell operator +(Cell a, Cell b) => new Cell(a.X+b.X,a.Y+b.Y);
    }
    public sealed class Unit
    {
        public string Id; public Kind Kind; public Cell Cell; public int Hp, Attack = 1;
        public bool Alive => Hp > 0;
        public bool Hostile;
        public bool Enemy => Hostile || Kind == Kind.Scout || Kind == Kind.Guard;
        public int Damage => Kind == Kind.Slimy ? 0 : Kind == Kind.Heavy ? 2 : 1;
        public float HoldSeconds => Kind == Kind.Heavy ? 5 : Enemy ? 3 : float.PositiveInfinity;
    }
    public sealed class Intent
    {
        public Unit Actor, Target; public Cell Destination; public bool Attacks;
        public override string ToString() => Actor == null ? "No enemy move" : Attacks ? $"{Actor.Kind} → {Target.Kind}" : $"{Actor.Kind} → {Destination}";
    }
    public sealed class BoardState
    {
        public readonly List<Unit> Units = new List<Unit>();
        public readonly List<Kind> Captured = new List<Kind>();
        public int Size, Stage, Turns, Revision; public Cell Pit; public Outcome Outcome;
        int pending; bool released;
        static readonly Cell[] Directions = {new Cell(0,-1),new Cell(0,1),new Cell(-1,0),new Cell(1,0)};
        public static BoardState Create(int stage)
        {
            var b = new BoardState {Stage=stage, Size=stage==1?4:6, Pit=stage==0?new Cell(5,2):stage==1?new Cell(3,3):new Cell(5,3)};
            if(stage==0) { b.Add(Kind.Knight,1,4,3); b.Add(Kind.Heavy,1,3,3); b.Add(Kind.Scout,3,3,1); b.Add(Kind.Guard,4,1,2); }
            if(stage==1) b.Add(Kind.Slimy,1,1,2);
            if(stage==2) { b.Add(Kind.Knight,1,2,3); b.Add(Kind.Heavy,0,4,3); b.Add(Kind.Slimy,2,3,2); b.Add(Kind.Scout,1,1,2).Attack=2; b.Add(Kind.Guard,4,3,2); }
            return b;
        }
        public Unit Add(Kind kind,int x,int y,int hp) { var u = new Unit {Id=$"{Units.Count:D3}",Kind=kind,Cell=new Cell(x,y),Hp=hp}; Units.Add(u); return u; }
        public bool Inside(Cell c) => c.X>=0 && c.Y>=0 && c.X<Size && c.Y<Size;
        public Unit At(Cell c, Unit except=null) => Units.FirstOrDefault(u=>u.Alive && u!=except && u.Cell.Equals(c));
        bool Empty(Cell c,Unit except=null) => Inside(c) && !c.Equals(Pit) && At(c,except)==null;
        public int CommitRelease() { if(Outcome!=Outcome.Playing || released) return -1; released=true; Turns++; return ++pending; }
        public void CancelPending() { pending++; released=false; Revision++; }
        // A failed grab is one committed action, with no phantom drop damage.
        public bool ResolveMiss(int token,Unit contacted,Cell direction)
        {
            if(!released || token!=pending || Outcome!=Outcome.Playing) return false;
            released=false;
            if(contacted!=null && contacted.Alive && contacted.Kind!=Kind.Heavy)
            {
                var next=contacted.Cell+direction;
                if(Inside(next) && At(next)==null) {if(next.Equals(Pit)) {Capture(contacted); if(Stage==1) Outcome=Outcome.Recruited;} else contacted.Cell=next;}
            }
            Finish(); return true;
        }
        public static Cell Push(float vx,float vz,Cell pickup,Cell landing,float threshold)
        {
            if(vx*vx+vz*vz<threshold*threshold) {vx=landing.X-pickup.X; vz=landing.Y-pickup.Y;}
            return Math.Abs(vx)>=Math.Abs(vz)?new Cell(vx<0?-1:1,0):new Cell(0,vz<0?-1:1);
        }
        Cell Nearest(Cell c,Unit except)
        {
            return Enumerable.Range(0,Size*Size).Select(i=>new Cell(i%Size,i/Size)).Where(p=>Empty(p,except)).OrderBy(p=>p.Distance(c)).ThenBy(p=>p.Y).ThenBy(p=>p.X).First();
        }
        void Capture(Unit unit) {if(unit.Enemy) Captured.Add(unit.Kind); unit.Hp=0;}
        public bool Resolve(int token,Unit dropper,Cell landing,Cell direction,bool directionalImpact=false)
        {
            if(!released || token!=pending || Outcome!=Outcome.Playing || !dropper.Alive) return false;
            released=false; landing=new Cell(Math.Max(0,Math.Min(Size-1,landing.X)),Math.Max(0,Math.Min(Size-1,landing.Y)));
            if(landing.Equals(Pit)) {Capture(dropper); if(Stage==1) Outcome=Outcome.Recruited;}
            else
            {
                var target=At(landing,dropper);
                if(target!=null && dropper.Kind==Kind.Slimy)
                {
                    var pushed=landing+direction;
                    if(Inside(pushed) && At(pushed,dropper)==null) { if(pushed.Equals(Pit)) Capture(target); else target.Cell=pushed; dropper.Cell=landing; }
                    else dropper.Cell=Nearest(landing,dropper);
                }
                else
                {
                    if(target!=null) { target.Hp-=dropper.Damage; if(target.Alive) {var pushed=landing+direction; if(directionalImpact && Inside(pushed) && At(pushed,dropper)==null) {if(pushed.Equals(Pit)) Capture(target); else target.Cell=pushed;} else {var choices=Directions.Select(d=>landing+d).Where(c=>Empty(c,dropper)).ToList(); if(choices.Count>0) target.Cell=choices[0]; else {dropper.Cell=Nearest(landing,dropper); Finish(); return true;} } } }
                    dropper.Cell=landing;
                }
            }
            Finish(); return true;
        }
        void Finish()
        {
            Revision++;
            if(Stage==1 || CheckEnd()) return;
            var knight=Units.FirstOrDefault(u=>u.Alive && u.Kind==Kind.Knight);
            if(knight!=null) {var target=Units.Where(u=>u.Alive && u.Enemy && u.Cell.Distance(knight.Cell)==1).OrderBy(u=>u.Hp).ThenBy(u=>u.Cell.Y).ThenBy(u=>u.Cell.X).FirstOrDefault(); if(target!=null) target.Hp--;}
            if(CheckEnd()) return;
            var plan=Plan();
            if(plan.Actor!=null) { if(plan.Attacks) plan.Target.Hp-=plan.Actor.Attack; else plan.Actor.Cell=plan.Destination; }
            CheckEnd();
        }
        bool CheckEnd()
        {
            if(!Units.Any(u=>u.Alive && !u.Enemy)) Outcome=Outcome.Defeat;
            else if(!Units.Any(u=>u.Alive && u.Enemy)) Outcome=Outcome.Victory;
            return Outcome!=Outcome.Playing;
        }
        public Intent Plan()
        {
            var allies=Units.Where(u=>u.Alive && !u.Enemy).ToList();
            if(allies.Count==0 || Stage==1) return new Intent();
            var actor=Units.Where(u=>u.Alive && u.Enemy).OrderBy(u=>allies.Any(a=>a.Cell.Distance(u.Cell)==1)?0:1).ThenBy(u=>allies.Min(a=>a.Cell.Distance(u.Cell))).ThenBy(u=>u.Id,StringComparer.Ordinal).FirstOrDefault();
            if(actor==null) return new Intent();
            var target=allies.Where(a=>a.Cell.Distance(actor.Cell)==1).OrderBy(a=>a.Hp).ThenBy(a=>a.Id,StringComparer.Ordinal).FirstOrDefault();
            if(target!=null) return new Intent {Actor=actor,Target=target,Attacks=true};
            target=allies.OrderBy(a=>a.Cell.Distance(actor.Cell)).ThenBy(a=>a.Id,StringComparer.Ordinal).First();
            var steps=Directions.Select(d=>actor.Cell+d).Where(c=>Empty(c) && c.Distance(target.Cell)<actor.Cell.Distance(target.Cell)).OrderBy(c=>c.Distance(target.Cell)).ToList();
            return new Intent {Actor=actor,Target=target,Destination=steps.Count>0?steps[0]:actor.Cell};
        }
    }
}
