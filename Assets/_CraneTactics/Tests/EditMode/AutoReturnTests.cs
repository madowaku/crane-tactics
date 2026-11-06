using CraneTactics;
using CraneTactics.Rules;
using NUnit.Framework;
using UnityEngine;

public class AutoReturnTests
{
    [Test] public void RouteLocksHorizontalBeforeDepth()
    {
        var r=new ReturnRoute(new Vector3(1,3.5f,4),new Vector3(5,0,2));
        Assert.That(r.Points[1],Is.EqualTo(new Vector3(5,3.5f,4)));
        Assert.That(r.Points[2],Is.EqualTo(new Vector3(5,3.5f,2)));
        Assert.That(r.Length,Is.EqualTo(6));
        Assert.That(new ReturnRoute(Vector3.zero,Vector3.zero).Length,Is.Zero);
    }
    [Test] public void GripHasNoHiddenRandomnessAndRewardsAccuracy()
    {
        foreach(Kind kind in System.Enum.GetValues(typeof(Kind)))
        {
            Assert.That(GripProfile.Quality(kind,0),Is.EqualTo(1));
            Assert.That(GripProfile.Quality(kind,1),Is.Zero);
            Assert.That(GripProfile.Seconds(kind,1),Is.GreaterThan(GripProfile.Seconds(kind,0)));
            Assert.That(GripProfile.Seconds(kind,-1),Is.EqualTo(GripProfile.Seconds(kind,0)));
        }
        Assert.That(GripProfile.Seconds(Kind.Heavy,1),Is.LessThan(GripProfile.Seconds(Kind.Knight,1)));
    }
    [Test] public void MissPushPersistsAndCannotResolveTwice()
    {
        var b=BoardState.Create(1); var slime=b.Units[0]; int token=b.CommitRelease();
        Assert.That(b.ResolveMiss(token,slime,new Cell(1,0)),Is.True);
        Assert.That(slime.Cell,Is.EqualTo(new Cell(2,1)));
        Assert.That(slime.Hp,Is.EqualTo(2));
        Assert.That(b.ResolveMiss(token,slime,new Cell(1,0)),Is.False);
        Assert.That(b.Turns,Is.EqualTo(1));
    }
    [Test] public void EmptyMissAndBlockedPushStillConsumeOneTurn()
    {
        var b=BoardState.Create(1); var slime=b.Units[0]; slime.Cell=new Cell(0,0);
        b.ResolveMiss(b.CommitRelease(),slime,new Cell(-1,0));
        Assert.That(slime.Cell,Is.EqualTo(new Cell(0,0)));
        b.ResolveMiss(b.CommitRelease(),null,new Cell(1,0));
        Assert.That(b.Turns,Is.EqualTo(2));
        int token=b.CommitRelease(); b.CancelPending(); Assert.That(b.ResolveMiss(token,slime,new Cell(1,0)),Is.False);
    }
    [Test] public void DirectionalHeavyImpactCanCaptureSurvivingEnemy()
    {
        var b=BoardState.Create(0); b.Units.Clear(); b.Pit=new Cell(5,2);
        b.Add(Kind.Knight,1,4,3); var heavy=b.Add(Kind.Heavy,1,2,3); heavy.Hostile=true;
        var guard=b.Add(Kind.Guard,4,2,3);
        b.Resolve(b.CommitRelease(),heavy,guard.Cell,new Cell(1,0),true);
        Assert.That(b.Captured,Is.EqualTo(new[]{Kind.Guard})); Assert.That(guard.Alive,Is.False);
        Assert.That(heavy.Cell,Is.EqualTo(new Cell(4,3)),"After landing, the surviving enemy takes its normal Y-first turn");
    }
    [Test] public void SeededFailedContactsPreserveOccupancyAndReproduce()
    {
        for(int seed=0;seed<100;seed++)
        {
            var a=BoardState.Create(1); var b=BoardState.Create(1); var random=new System.Random(seed);
            for(int turn=0;turn<12 && a.Outcome==Outcome.Playing;turn++)
            {
                var direction=new[]{new Cell(1,0),new Cell(-1,0),new Cell(0,1),new Cell(0,-1)}[random.Next(4)];
                a.ResolveMiss(a.CommitRelease(),a.Units[0],direction); b.ResolveMiss(b.CommitRelease(),b.Units[0],direction);
                Assert.That(a.Units[0].Cell,Is.EqualTo(b.Units[0].Cell)); Assert.That(a.Outcome,Is.EqualTo(b.Outcome));
                Assert.That(a.Turns,Is.EqualTo(turn+1));
            }
        }
    }
    [Test] public void SweetFloorAndKoromochiHaveDistinctTuning()
    {
        Assert.That(CabinetProfile.Sweets.FloorFriction,Is.LessThan(CabinetProfile.Classic.FloorFriction));
        Assert.That(GripProfile.Quality(Kind.Koromochi,.3f),Is.Zero);
        Assert.That(GripProfile.Quality(Kind.Knight,.3f),Is.GreaterThan(.3f));
    }
}
