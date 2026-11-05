using System.Linq;
using NUnit.Framework;
using CraneTactics.Rules;
public class BoardTests
{
    [Test] public void TwoDropReferenceRoute()
    {
        var b=BoardState.Create(2); var slime=b.Units.Single(u=>u.Kind==Kind.Slimy);
        Assert.That(b.Resolve(b.CommitRelease(),slime,new Cell(4,3),new Cell(1,0)),Is.True);
        Assert.That(b.Units.Single(u=>u.Kind==Kind.Knight).Hp,Is.EqualTo(1));
        var scout=b.Units.Single(u=>u.Kind==Kind.Scout);
        b.Resolve(b.CommitRelease(),b.Units.Single(u=>u.Kind==Kind.Heavy),scout.Cell,new Cell(1,0));
        Assert.That(b.Outcome,Is.EqualTo(Outcome.Victory)); Assert.That(b.Turns,Is.EqualTo(2));
    }
    [Test] public void DuplicateAndCancelledSettlesDoNothing()
    {
        var b=BoardState.Create(1); int token=b.CommitRelease(); b.CancelPending();
        Assert.That(b.Resolve(token,b.Units[0],b.Pit,new Cell(1,0)),Is.False);
        token=b.CommitRelease(); Assert.That(b.Resolve(token,b.Units[0],new Cell(2,2),new Cell(1,0)),Is.True);
        Assert.That(b.Resolve(token,b.Units[0],b.Pit,new Cell(1,0)),Is.False);
    }
    [Test] public void IntentAndExecutionUseSamePlanner()
    {
        var b=BoardState.Create(0); var heavy=b.Units.Single(u=>u.Kind==Kind.Heavy);
        b.Resolve(b.CommitRelease(),heavy,new Cell(3,3),new Cell(1,0));
        Assert.That(b.Units.Single(u=>u.Kind==Kind.Guard).Cell,Is.EqualTo(new Cell(4,2)));
    }
}
