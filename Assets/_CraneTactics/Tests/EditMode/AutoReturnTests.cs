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
}
