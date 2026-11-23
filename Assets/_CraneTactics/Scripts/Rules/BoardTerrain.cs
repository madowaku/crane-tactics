using System;
using System.Collections.Generic;
using System.Linq;

namespace CraneTactics.Rules
{
    public enum Terrain { Floor, Frosting, Jelly, Magnet, Gum, BeltNorth, BeltSouth, BeltEast, BeltWest }

    // Deterministic per-attempt medals: rewards different solutions without forbidding home captures.
    [Flags] public enum Medal { None=0, Clear=1, Par=2, TrickShot=4 }

    // Cabinet machines run as discrete phases between the Knight attack and the enemy action.
    public sealed partial class BoardState
    {
        public const int ClockworkWind=2;
        const int MagnetRange=3;
        public const int ClockworkDamage=2;
        readonly Dictionary<Cell,Terrain> terrain=new Dictionary<Cell,Terrain>();
        public bool IsPrize {get;private set;}
        public int Par {get;private set;}
        public int DirectCaptures {get;private set;}
        public Terrain TerrainAt(Cell c) => terrain.TryGetValue(c,out var t)?t:Terrain.Floor;
        public IEnumerable<KeyValuePair<Cell,Terrain>> TerrainCells => terrain.OrderBy(p=>p.Key.Y).ThenBy(p=>p.Key.X);
        public static Cell BeltDirection(Terrain t) => t==Terrain.BeltNorth?new Cell(0,1):t==Terrain.BeltSouth?new Cell(0,-1):t==Terrain.BeltEast?new Cell(1,0):t==Terrain.BeltWest?new Cell(-1,0):new Cell(0,0);
        public bool IsStuck(Unit u) {var t=TerrainAt(u.Cell); return t==Terrain.Gum && u.Kind!=Kind.Petapetan || t==Terrain.Magnet && u.Metal;}

        public Medal Medals
        {
            get
            {
                if(Outcome!=Outcome.Victory && Outcome!=Outcome.Recruited) return Medal.None;
                var earned=Medal.Clear;
                if(Par>0 && Turns<=Par) earned|=Medal.Par;
                if(!IsPrize && DirectCaptures==0) earned|=Medal.TrickShot;
                return earned;
            }
        }

        // Lets rule tests describe a one-off board without adding it to the catalog.
        public static BoardState FromDefinitionForTests(StageDefinition d) => FromDefinition(3,d);
        static BoardState FromDefinition(int stage,StageDefinition d)
        {
            var b=new BoardState {Stage=stage,Size=d.Size,Pit=d.Pit,IsPrize=d.Prize,Par=d.Par};
            for(int y=0;y<d.Size;y++) for(int x=0;x<d.Size;x++)
            {
                var t=d.TerrainAt(x,y);
                if(t!=Terrain.Floor) b.terrain[new Cell(x,y)]=t;
            }
            foreach(var s in d.Spawns)
            {
                var u=b.Add(s.Kind,s.X,s.Y,s.Hp); u.Attack=s.Attack; u.Hostile=s.Hostile; u.GripScale=s.GripScale; u.Facing=s.Facing;
            }
            return b;
        }

        // Independent deep copy for the solver: playback data and tokens are intentionally not copied.
        public BoardState Clone()
        {
            var b=new BoardState {Stage=Stage,Size=Size,Pit=Pit,Outcome=Outcome,Turns=Turns,IsPrize=IsPrize,Par=Par,DirectCaptures=DirectCaptures};
            foreach(var pair in terrain) b.terrain[pair.Key]=pair.Value;
            foreach(var u in Units) b.Units.Add(new Unit {Id=u.Id,Kind=u.Kind,Cell=u.Cell,Hp=u.Hp,Attack=u.Attack,Hostile=u.Hostile,GripScale=u.GripScale,Facing=u.Facing,Wind=u.Wind});
            b.Captured.AddRange(Captured);
            return b;
        }
        public string StateKey() => string.Join("|",Units.Select(u=>u.Alive?$"{u.Cell.X},{u.Cell.Y},{u.Hp},{u.Facing.X},{u.Facing.Y},{u.Wind}":"x"))+"/"+string.Join(",",terrain.Where(p=>p.Value==Terrain.Gum).Select(p=>p.Key.ToString()).OrderBy(k=>k,StringComparer.Ordinal))+"/"+DirectCaptures;

        // Jelly launches an unobstructed drop two cells on; Koromochi keeps rolling until something stops it.
        Cell ApplyLandingTerrain(Unit dropper,Cell landing,Cell direction)
        {
            for(int hops=0;hops<3 && TerrainAt(landing)==Terrain.Jelly && At(landing,dropper)==null;hops++)
            {
                var next=landing+direction+direction;
                if(!Inside(next)) next=landing+direction;
                if(!Inside(next)) break;
                dropper.Cell=landing; Beat("bounce",dropper);
                landing=next;
            }
            if(dropper.Kind!=Kind.Koromochi || landing.Equals(Pit) || At(landing,dropper)!=null) return landing;
            while(true)
            {
                var next=landing+direction;
                if(!Inside(next)) return landing;
                landing=next;
                if(landing.Equals(Pit) || At(landing,dropper)!=null) return landing;
            }
        }

        void LandPetapetan(Unit dropper,Unit target,Cell landing,Cell direction)
        {
            if(TerrainAt(landing)!=Terrain.Magnet) terrain[landing]=Terrain.Gum;
            if(target==null) {dropper.Cell=landing; return;}
            var back=new Cell(landing.X-direction.X,landing.Y-direction.Y);
            dropper.Cell=Empty(back,dropper)?back:Nearest(landing,dropper);
        }

        void SlideAfterImpact(Unit dropper,Unit target,Cell direction)
        {
            var first=target!=null && target.Alive?target:dropper; var second=first==dropper?null:dropper;
            bool moved=SlideQuiet(first,direction);
            if(second!=null) moved|=SlideQuiet(second,direction);
            if(moved) Beat("slide",dropper,target);
        }
        void SlideOnFrosting(Unit unit,Cell direction) {if(SlideQuiet(unit,direction)) Beat("slide",unit);}
        bool SlideQuiet(Unit unit,Cell direction)
        {
            bool moved=false;
            while(unit.Alive && TerrainAt(unit.Cell)==Terrain.Frosting && (direction.X!=0 || direction.Y!=0))
            {
                var next=unit.Cell+direction;
                if(!Inside(next) || At(next,unit)!=null) break;
                moved=true;
                if(next.Equals(Pit)) {Capture(unit); break;}
                unit.Cell=next;
            }
            return moved;
        }

        void RunFloorMachines()
        {
            RunMagnets(); if(Outcome!=Outcome.Playing) return;
            RunBelts();
        }
        void RunMagnets()
        {
            if(!terrain.Values.Contains(Terrain.Magnet)) return;
            bool moved=false; Unit last=null;
            foreach(var unit in Units.Where(u=>u.Alive && u.Metal && TerrainAt(u.Cell)!=Terrain.Magnet).OrderBy(u=>u.Id,StringComparer.Ordinal).ToList())
            {
                if(!unit.Alive || Outcome!=Outcome.Playing) continue;
                var step=MagnetPull(unit);
                if(step==null) continue;
                var next=unit.Cell+step.Value;
                if(next.Equals(Pit)) {Capture(unit); moved=true; last=unit;}
                else if(Empty(next,unit)) {unit.Cell=next; moved=true; last=unit;}
            }
            if(moved) Beat("magnet",last);
        }
        // Nearest magnet in the same row or column with no toy between; X wins ties for a stable result.
        public Cell? MagnetPull(Unit unit)
        {
            Cell? best=null; int bestDistance=int.MaxValue;
            foreach(var pair in terrain.Where(p=>p.Value==Terrain.Magnet).OrderBy(p=>p.Key.Y).ThenBy(p=>p.Key.X))
            {
                var m=pair.Key; int distance=m.Distance(unit.Cell);
                if(distance==0 || distance>MagnetRange || m.X!=unit.Cell.X && m.Y!=unit.Cell.Y) continue;
                var step=new Cell(Math.Sign(m.X-unit.Cell.X),Math.Sign(m.Y-unit.Cell.Y));
                bool clear=true;
                for(var c=unit.Cell+step;!c.Equals(m);c=c+step) if(At(c)!=null) {clear=false; break;}
                if(!clear || At(m,unit)!=null) continue;
                if(distance<bestDistance || distance==bestDistance && step.X!=0) {best=step; bestDistance=distance;}
            }
            return best;
        }
        void RunBelts()
        {
            var riders=Units.Where(u=>u.Alive && BeltDirection(TerrainAt(u.Cell)).Distance(new Cell(0,0))==1).OrderBy(u=>u.Id,StringComparer.Ordinal).ToList();
            if(riders.Count==0) return;
            bool any=false, progress=true; Unit last=null;
            // Front riders clear the way, so repeat until the remaining riders stay blocked.
            while(progress && riders.Count>0 && Outcome==Outcome.Playing)
            {
                progress=false;
                foreach(var unit in riders.ToList())
                {
                    if(Outcome!=Outcome.Playing) break;
                    var direction=BeltDirection(TerrainAt(unit.Cell)); var next=unit.Cell+direction;
                    if(next.Equals(Pit)) {Capture(unit); riders.Remove(unit); progress=any=true; last=unit;}
                    else if(Empty(next,unit)) {unit.Cell=next; riders.Remove(unit); progress=any=true; last=unit;}
                }
            }
            if(any) Beat("belt",last);
        }
        void RunClockworks()
        {
            foreach(var unit in Units.Where(u=>u.Alive && u.Kind==Kind.Clockwork).OrderBy(u=>u.Id,StringComparer.Ordinal).ToList())
            {
                if(!unit.Alive || Outcome!=Outcome.Playing || IsStuck(unit)) continue;
                if(--unit.Wind>0) continue;
                unit.Wind=ClockworkWind;
                Unit hit=null; bool moved=false;
                while(true)
                {
                    var next=unit.Cell+unit.Facing;
                    if(!Inside(next)) break;
                    if(next.Equals(Pit)) {Capture(unit); moved=true; break;}
                    hit=At(next,unit);
                    if(hit!=null) {hit.Hp-=ClockworkDamage; break;}
                    unit.Cell=next; moved=true;
                }
                if(moved || hit!=null) Beat("clock-dash",unit,hit);
                if(CheckEnd()) return;
            }
        }
        // Read-only forecast for spatial HUD; shares the same stop rules as RunClockworks.
        public IEnumerable<Cell> ClockworkPath(Unit unit)
        {
            for(var c=unit.Cell+unit.Facing;Inside(c);c=c+unit.Facing)
            {
                yield return c;
                if(c.Equals(Pit) || At(c,unit)!=null) yield break;
            }
        }
    }
}
