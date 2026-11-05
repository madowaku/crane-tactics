using System;
using System.Linq;
using CraneTactics.Rules;

static class Program
{
    static int passed;
    static void Check(bool ok,string name) {if(!ok) throw new Exception(name); passed++; Console.WriteLine("PASS "+name);}
    static Unit Get(BoardState b,Kind k)=>b.Units.First(u=>u.Kind==k);
    static void Drop(BoardState b,Kind k,Cell c,Cell? direction=null) {Check(b.Resolve(b.CommitRelease(),Get(b,k),c,direction??new Cell(1,0)),"release resolves"); Check(b.Units.Where(u=>u.Alive).Select(u=>u.Cell).Distinct().Count()==b.Units.Count(u=>u.Alive),"unique cells");}
    static void Main()
    {
        var b=BoardState.Create(2);
        Drop(b,Kind.Slimy,new Cell(4,3));
        Check(!Get(b,Kind.Guard).Alive,"Slimy pushes Guard into pit");
        Check(Get(b,Kind.Scout).Hp==1 && Get(b,Kind.Knight).Hp==1,"balanced reference survives first turn");
        Drop(b,Kind.Heavy,Get(b,Kind.Scout).Cell);
        Check(b.Outcome==Outcome.Victory && b.Turns==2,"two-drop Battle 2 victory");
        Check(b.CommitRelease()==-1,"terminal state rejects new turn");
        b=BoardState.Create(0); Drop(b,Kind.Heavy,new Cell(3,3));
        Check(!Get(b,Kind.Scout).Alive,"Heavy defeats Scout");
        Check(Get(b,Kind.Guard).Cell.Equals(new Cell(4,2)),"Guard moves Y first");
        Drop(b,Kind.Guard,b.Pit); Check(b.Outcome==Outcome.Victory,"Battle 1 collection victory");
        b=BoardState.Create(1); Drop(b,Kind.Slimy,new Cell(2,2)); Check(b.Outcome==Outcome.Playing && Get(b,Kind.Slimy).Cell.Equals(new Cell(2,2)),"Prize miss preserved");
        Drop(b,Kind.Slimy,b.Pit); Check(b.Outcome==Outcome.Recruited,"Prize recruits");
        b=BoardState.Create(2); var token=b.CommitRelease(); Check(b.CommitRelease()==-1,"concurrent release rejected"); b.CancelPending(); Check(!b.Resolve(token,Get(b,Kind.Heavy),new Cell(1,1),new Cell(1,0)),"cancel invalidates late settle");
        token=b.CommitRelease(); Check(b.Resolve(token,Get(b,Kind.Heavy),new Cell(0,4),new Cell(1,0)),"new release after cancellation"); Check(!b.Resolve(token,Get(b,Kind.Heavy),new Cell(0,4),new Cell(1,0)),"duplicate collision ignored");
        b=BoardState.Create(0); Drop(b,Kind.Heavy,Get(b,Kind.Knight).Cell); Check(Get(b,Kind.Knight).Hp==1,"friendly fire");
        b=BoardState.Create(2); Get(b,Kind.Guard).Cell=new Cell(5,4); Drop(b,Kind.Slimy,new Cell(5,4)); Check(Get(b,Kind.Guard).Alive && !Get(b,Kind.Slimy).Cell.Equals(Get(b,Kind.Guard).Cell),"wall blocks slime push");
        Check(BoardState.Push(0,0,new Cell(2,3),new Cell(4,3),.35f).Equals(new Cell(1,0)),"quiet displacement direction");
        Check(BoardState.Push(-2,1,new Cell(0,0),new Cell(2,0),.35f).Equals(new Cell(-1,0)),"velocity dominates displacement");
        Check(BoardState.Push(0,0,new Cell(0,0),new Cell(0,0),.35f).Equals(new Cell(1,0)),"stationary defaults right");
        Check(BoardState.Push(1,1,new Cell(0,0),new Cell(0,0),.35f).Equals(new Cell(1,0)),"equal axes choose X");
        // Stress deterministic legal drops for occupancy and one-turn invariants.
        var random=new Random(1403);
        for(int run=0;run<100;run++) {b=BoardState.Create(run%3); for(int turn=0;turn<40 && b.Outcome==Outcome.Playing;turn++) {var alive=b.Units.Where(u=>u.Alive).ToArray(); var unit=alive[random.Next(alive.Length)]; int before=b.Turns; b.Resolve(b.CommitRelease(),unit,new Cell(random.Next(b.Size),random.Next(b.Size)),new Cell(1,0)); if(b.Turns!=before+1 || b.Units.Where(u=>u.Alive).GroupBy(u=>u.Cell).Any(g=>g.Count()>1)) throw new Exception("random invariant");}}
        Check(true,"100 seeded runs preserve unique cells and one action per release");
        Console.WriteLine($"{passed} assertions passed");
    }
}
