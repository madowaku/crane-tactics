using System.Collections;
using System.Linq;
using CraneTactics;
using CraneTactics.Rules;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class AutoReturnFlowTests
{
    OneNightGame game;
    [UnitySetUp] public IEnumerator Setup() {game=new GameObject("AutoReturnTest").AddComponent<OneNightGame>(); game.Automation=true; yield return null; game.SetAutoReturn(true); yield return null;}
    [UnityTearDown] public IEnumerator Cleanup() {Object.Destroy(game.gameObject); yield return null;}
    IEnumerator Wait(System.Func<bool> condition,float timeout=12)
    {
        float end=Time.time+timeout; while(!condition() && Time.time<end) yield return null;
        Assert.That(condition(),Is.True,"Auto-return state timed out");
    }
    [UnityTest] public IEnumerator HoldToChuteCapturesExactlyOnce()
    {
        var guard=game.Board.Units.Single(u=>u.Kind==Kind.Guard); game.Grab(guard);
        yield return Wait(()=>game.IsCarrying); yield return Wait(()=>game.IsSelecting || game.IsResult);
        Assert.That(guard.Alive,Is.False); Assert.That(game.Board.Turns,Is.EqualTo(1));
        Assert.That(game.ExperimentLog.Any(x=>x.Contains("capture")),Is.True);
    }
    [UnityTest] public IEnumerator EnemyHeavyCanHitEnemyOnReturn()
    {
        game.LoadExperimentCase(1);
        var heavy=game.Board.Units.Single(u=>u.Kind==Kind.Heavy); game.Grab(heavy);
        yield return Wait(()=>game.IsCarrying);
        yield return Wait(()=>game.ClawPosition.x>=3.15f,4); game.Drop();
        yield return Wait(()=>game.IsSelecting || game.IsResult);
        var guard=game.Board.Units.Single(u=>u.Kind==Kind.Guard);
        Assert.That(guard.Hp,Is.LessThan(3),"Physical enemy-on-enemy impact must damage guard");
        Assert.That(game.Board.Captured,Does.Contain(Kind.Guard),"Heavy impact pushes surviving guard into chute");
        Assert.That(game.Board.Turns,Is.EqualTo(1));
        Assert.That(game.ExperimentLog.Any(x=>x.Contains("impact")),Is.True);
    }
    IEnumerator Capture(Kind kind)
    {
        game.Grab(game.Board.Units.Single(u=>u.Kind==kind)); yield return Wait(()=>game.IsCarrying);
        yield return Wait(()=>game.IsSelecting || game.IsResult);
    }
    [UnityTest] public IEnumerator AutoReturnFullOneNightLoop()
    {
        yield return Capture(Kind.Guard); yield return Capture(Kind.Scout);
        Assert.That(game.Board.Outcome,Is.EqualTo(Outcome.Victory)); game.NextCabinet(); yield return null;
        yield return Capture(Kind.Slimy); Assert.That(game.Board.Outcome,Is.EqualTo(Outcome.Recruited));
        game.NextCabinet(); yield return null;
        yield return Capture(Kind.Guard); yield return Capture(Kind.Scout);
        Assert.That(game.Board.Outcome,Is.EqualTo(Outcome.Victory));
        yield return new WaitForSeconds(1); game.NextCabinet(); Assert.That(game.Board.Stage,Is.Zero);
    }
    [UnityTest] public IEnumerator FailedOffsetGrabChangesBoardWithoutDamage()
    {
        var guard=game.Board.Units.Single(u=>u.Kind==Kind.Guard);
        game.BeginAutoGrab(new Vector3(3.55f,0,1)); yield return Wait(()=>game.IsSelecting || game.IsResult);
        Assert.That(game.Board.Turns,Is.EqualTo(1)); Assert.That(guard.Cell.X,Is.EqualTo(5));
        Assert.That(game.ExperimentLog.Any(x=>x.Contains("contact-push")),Is.True);
    }
    [UnityTest] public IEnumerator EmptyClawAndAxisLockAreRealInputPath()
    {
        for(int i=0;i<10;i++) game.PickInput(true,.05f);
        game.PickInput(false,.01f); Assert.That(game.PickHint.StartsWith("2"),Is.True);
        game.PickInput(true,.05f); game.PickInput(false,.01f);
        yield return Wait(()=>game.IsSelecting || game.IsResult);
        Assert.That(game.Board.Turns,Is.EqualTo(1));
        Assert.That(game.ExperimentLog.Any(x=>x.Contains("empty-miss")),Is.True);
    }
    [UnityTest] public IEnumerator GripPauseReleaseAndModeReset()
    {
        game.Grab(game.Board.Units.Single(u=>u.Kind==Kind.Heavy)); yield return Wait(()=>game.IsCarrying);
        game.PauseGame(true); float before=game.GripRemaining; game.Drop(); yield return new WaitForSeconds(.3f);
        Assert.That(game.GripRemaining,Is.EqualTo(before)); Assert.That(game.Board.Turns,Is.Zero);
        game.PauseGame(false); yield return Wait(()=>game.IsSelecting || game.IsResult);
        Assert.That(game.Board.Turns,Is.EqualTo(1));
        game.SetAutoReturn(false); Assert.That(game.Board.Turns,Is.Zero); Assert.That(game.Board.Units.Count,Is.EqualTo(4));
    }
}
