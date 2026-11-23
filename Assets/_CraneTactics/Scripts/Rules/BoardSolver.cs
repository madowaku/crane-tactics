using System;
using System.Collections.Generic;
using System.Linq;

namespace CraneTactics.Rules
{
    // One grid-level crane action in button play: carry a toy anywhere its grip reaches and let go,
    // or nudge a toy with a deliberately poor grip. Used to validate cabinet design data.
    public struct CraneMove
    {
        public int Unit; public Cell Landing, Direction; public bool Contact;
        public override string ToString() => Contact?$"nudge#{Unit}{Direction}":$"#{Unit}->{Landing}{Direction}";
    }
    public sealed class SolveResult
    {
        public int Fewest=-1, FewestWithoutHome=-1, WinningFirstMoves; public long States; public bool Exhausted; public List<CraneMove> Line=new List<CraneMove>();
    }
    public static class BoardSolver
    {
        public const float NudgeOffset=.7f;
        // Part of a grip goes to letting the swing settle, so the plan keeps a margin of the full time.
        public const float ReachMargin=.9f;
        // A winning line at par for each cabinet stage, found by `Tests/Rules --report` on .NET.
        // Search is minutes on Mono, so tests replay these and the report proves nothing shorter exists.
        public static readonly Dictionary<int,CraneMove[]> ReferenceLines=new Dictionary<int,CraneMove[]>
        {
            [3]=new[]{new CraneMove{Unit=0,Landing=new Cell(3,1),Direction=new Cell(0,1)}},
            [4]=new[]{new CraneMove{Unit=0,Landing=new Cell(0,2),Direction=new Cell(0,1)},new CraneMove{Unit=3,Landing=new Cell(2,2),Direction=new Cell(1,0)}},
            [5]=new[]{new CraneMove{Unit=0,Landing=new Cell(3,3),Direction=new Cell(1,0)}},
            [6]=new[]{new CraneMove{Unit=0,Landing=new Cell(0,0),Direction=new Cell(1,0)},new CraneMove{Unit=0,Landing=new Cell(1,0),Direction=new Cell(-1,0)},new CraneMove{Unit=1,Landing=new Cell(1,5),Direction=new Cell(1,0)}},
            [7]=new[]{new CraneMove{Unit=0,Landing=new Cell(3,3),Direction=new Cell(1,0)}},
            [8]=new[]{new CraneMove{Unit=0,Landing=new Cell(2,1),Direction=new Cell(1,0)},new CraneMove{Unit=3,Landing=new Cell(5,1),Direction=new Cell(0,1)},new CraneMove{Unit=0,Landing=new Cell(1,1),Direction=new Cell(-1,0)}},
            [9]=new[]{new CraneMove{Unit=0,Landing=new Cell(1,0),Direction=new Cell(0,-1)},new CraneMove{Unit=0,Landing=new Cell(2,4),Direction=new Cell(1,0)},new CraneMove{Unit=7,Landing=new Cell(5,3),Direction=new Cell(1,0)},new CraneMove{Unit=1,Landing=new Cell(1,0),Direction=new Cell(1,0)}},
        };
        static readonly Cell[] Four={new Cell(1,0),new Cell(-1,0),new Cell(0,1),new Cell(0,-1)};

        // Grid cells the claw can carry this toy across before a best grip gives out. The claw moves
        // along one axis at a time, so reach is measured in steps (Manhattan distance).
        public static int Reach(BoardState b,Unit u,float carrySpeed) =>
            (int)Math.Floor(GripProfile.Seconds(u.Kind,1)*u.GripScale*(b.IsStuck(u)?.6f:1)*carrySpeed*ReachMargin);
        // Heading of a drop made after the claw has stopped: the board reads it from the carry.
        public static Cell StillDirection(Cell from,Cell to) => BoardState.Push(0,0,from,to,1);
        // Only some landings care which way the toy was moving.
        static bool DirectionMatters(BoardState b,Unit u,Cell landing)
        {
            if(landing.Equals(b.Pit)) return false;
            if(u.Kind==Kind.Koromochi || u.Kind==Kind.Clockwork) return true;
            if(b.At(landing,u)!=null) return true;
            var t=b.TerrainAt(landing); return t==Terrain.Frosting || t==Terrain.Jelly;
        }
        public static IEnumerable<CraneMove> Moves(BoardState b,float carrySpeed)
        {
            for(int i=0;i<b.Units.Count;i++)
            {
                var u=b.Units[i]; if(!u.Alive) continue;
                int reach=Reach(b,u,carrySpeed);
                for(int y=0;y<b.Size;y++) for(int x=0;x<b.Size;x++)
                {
                    var c=new Cell(x,y); if(u.Cell.Distance(c)>reach) continue;
                    var still=StillDirection(u.Cell,c);
                    yield return new CraneMove {Unit=i,Landing=c,Direction=still};
                    if(!DirectionMatters(b,u,c)) continue;
                    // A moving release: the claw arrives along this heading and lets go on the way.
                    foreach(var d in Four)
                    {
                        if(d.Equals(still)) continue;
                        var before=new Cell(c.X-d.X,c.Y-d.Y);
                        if(!b.Inside(before) || u.Cell.Distance(before)+1>reach) continue;
                        yield return new CraneMove {Unit=i,Landing=c,Direction=d};
                    }
                }
                if(u.Kind==Kind.Heavy || b.IsStuck(u)) continue;
                foreach(var d in Four)
                {
                    // A nudge needs the claw to come down on the toy's far side, inside the cabinet,
                    // with this toy the nearest one to that point.
                    float ax=u.Cell.X-d.X*NudgeOffset, ay=u.Cell.Y-d.Y*NudgeOffset;
                    if(ax<0 || ay<0 || ax>b.Size-1 || ay>b.Size-1) continue;
                    var near=b.At(new Cell(u.Cell.X-d.X,u.Cell.Y-d.Y));
                    if(near!=null) continue;
                    yield return new CraneMove {Unit=i,Direction=d,Contact=true};
                }
            }
        }
        public static BoardState Apply(BoardState source,CraneMove m)
        {
            var b=source.Clone(); int token=b.CommitRelease();
            if(m.Contact) b.ResolveMiss(token,b.Units[m.Unit],m.Direction);
            else b.Resolve(token,b.Units[m.Unit],m.Landing,m.Direction,true);
            return b;
        }
        static bool Won(BoardState b) => b.Outcome==Outcome.Victory || b.Outcome==Outcome.Recruited;
        // Breadth-first and exhaustive up to maxDepth: Fewest is the true par when it is found.
        public static SolveResult Solve(BoardState start,float carrySpeed,int maxDepth=3,long stateLimit=20000000)
        {
            var result=new SolveResult();
            var frontier=new List<(BoardState State,List<CraneMove> Line,int First)>{(start,new List<CraneMove>(),-1)};
            var seen=new HashSet<string>{start.StateKey()};
            var winningFirst=new HashSet<int>();
            for(int depth=1;depth<=maxDepth && frontier.Count>0;depth++)
            {
                var next=new List<(BoardState,List<CraneMove>,int)>();
                foreach(var node in frontier)
                {
                    var moves=Moves(node.State,carrySpeed).ToList();
                    for(int i=0;i<moves.Count;i++)
                    {
                        var after=Apply(node.State,moves[i]); result.States++;
                        int first=node.First<0?i:node.First;
                        if(Won(after))
                        {
                            if(result.Fewest<0) {result.Fewest=depth; result.Line=new List<CraneMove>(node.Line){moves[i]};}
                            if(result.Fewest==depth) winningFirst.Add(first);
                            if(after.DirectCaptures==0 && result.FewestWithoutHome<0) result.FewestWithoutHome=depth;
                            continue;
                        }
                        if(result.Fewest>0 || depth==maxDepth) continue;
                        if(after.Outcome!=Outcome.Playing || !seen.Add(after.StateKey())) continue;
                        next.Add((after,new List<CraneMove>(node.Line){moves[i]},first));
                    }
                }
                if(result.Fewest>0) break;
                if(result.States>stateLimit) {result.Exhausted=true; break;}
                frontier=next;
            }
            result.WinningFirstMoves=winningFirst.Count;
            return result;
        }
        // Depth-first search for any winning line of exactly `depth` drops; use after Solve has
        // shown that no shorter line exists. Optionally skips lines that carry a rival home.
        public static List<CraneMove> FindLine(BoardState start,float carrySpeed,int depth,bool trickShot=false)
        {
            var failed=new HashSet<string>(); var line=new List<CraneMove>();
            bool Search(BoardState b,int left)
            {
                string key=left+"#"+b.StateKey(); if(failed.Contains(key)) return false;
                foreach(var m in Moves(b,carrySpeed))
                {
                    var after=Apply(b,m);
                    if(trickShot && after.DirectCaptures>0) continue;
                    if(Won(after)) {line.Add(m); return true;}
                    if(left>1 && after.Outcome==Outcome.Playing && Search(after,left-1)) {line.Insert(0,m); return true;}
                }
                failed.Add(key); return false;
            }
            return Search(start,depth)?line:null;
        }
        // Naive player: always carries the closest rival as near the chute as its grip allows.
        // Measures capture dominance.
        public static (Outcome Outcome,int Turns) HomeRush(BoardState start,float carrySpeed,int maxTurns=20)
        {
            var b=start.Clone();
            while(b.Outcome==Outcome.Playing && b.Turns<maxTurns)
            {
                var targets=b.Units.Select((u,i)=>(u,i)).Where(p=>p.u.Alive && (b.IsPrize || p.u.Enemy)).OrderBy(p=>p.u.Cell.Distance(b.Pit)).ToList();
                if(targets.Count==0) break;
                var best=Moves(b,carrySpeed).Where(m=>!m.Contact && m.Unit==targets[0].i).OrderBy(m=>m.Landing.Distance(b.Pit)).First();
                b=Apply(b,best);
            }
            return (b.Outcome,b.Turns);
        }
        // Uniformly random legal actions: approximate difficulty for an unplanned player.
        public static double RandomWinRate(BoardState start,float carrySpeed,int runs,int seed,int maxTurns=12)
        {
            var random=new Random(seed); int wins=0;
            for(int run=0;run<runs;run++)
            {
                var b=start.Clone();
                while(b.Outcome==Outcome.Playing && b.Turns<maxTurns) {var moves=Moves(b,carrySpeed).ToList(); b=Apply(b,moves[random.Next(moves.Count)]);}
                if(Won(b)) wins++;
            }
            return wins/(double)runs;
        }
    }
}
