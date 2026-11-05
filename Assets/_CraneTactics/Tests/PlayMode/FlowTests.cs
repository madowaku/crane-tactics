using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CraneTactics;
using CraneTactics.Rules;

public class FlowTests
{
    OneNightGame game;
    [UnitySetUp] public IEnumerator Setup() {game=new GameObject("TestGame").AddComponent<OneNightGame>(); game.Automation=true; yield return null;}
    [UnityTearDown] public IEnumerator Cleanup() {Object.Destroy(game.gameObject); var world=GameObject.Find("OneNight Cabinet"); if(world) Object.Destroy(world); yield return null;}
    IEnumerator Wait(System.Func<bool> condition,float timeout)
    {
        float end=Time.time+timeout; while(!condition() && Time.time<end) yield return null; Assert.That(condition(),Is.True,"Timed out waiting for crane state");
    }
    IEnumerator Drop(Kind kind,Cell destination)
    {
        game.Grab(game.Board.Units.Single(u=>u.Kind==kind)); yield return Wait(()=>game.IsCarrying,5);
        game.Aim(destination); yield return new WaitForSeconds(2.2f); game.Drop(); yield return Wait(()=>game.IsSelecting || game.IsResult,3);
    }
    [UnityTest] public IEnumerator PhysicsDrivenFullLoopWithPrizeMiss()
    {
        yield return Drop(Kind.Heavy,new Cell(3,3));
        Assert.That(game.Board.Units.Single(u=>u.Kind==Kind.Scout).Alive,Is.False,"Heavy impact");
        yield return Drop(Kind.Guard,game.Board.Pit); Assert.That(game.Board.Outcome,Is.EqualTo(Outcome.Victory));
        game.NextCabinet(); yield return null;
        yield return Drop(Kind.Slimy,new Cell(2,2)); Assert.That(game.Board.Outcome,Is.EqualTo(Outcome.Playing));
        yield return Drop(Kind.Slimy,game.Board.Pit); Assert.That(game.Board.Outcome,Is.EqualTo(Outcome.Recruited));
        game.NextCabinet(); yield return null;
        yield return Drop(Kind.Slimy,new Cell(4,3));
        Assert.That(game.Board.Units.Single(u=>u.Kind==Kind.Knight).Hp,Is.EqualTo(1));
        yield return Drop(Kind.Heavy,new Cell(1,1)); Assert.That(game.Board.Outcome,Is.EqualTo(Outcome.Victory));
        game.NextCabinet(); yield return null; Assert.That(game.Board.Stage,Is.EqualTo(0));
    }
    [UnityTest] public IEnumerator EnemyHoldTimeoutCommitsExactlyOnce()
    {
        game.Grab(game.Board.Units.Single(u=>u.Kind==Kind.Guard)); yield return Wait(()=>game.IsCarrying,5);
        yield return Wait(()=>game.IsSelecting || game.IsResult,7); Assert.That(game.Board.Turns,Is.EqualTo(1));
    }
    [UnityTest] public IEnumerator PauseFreezesHoldClock()
    {
        game.Grab(game.Board.Units.Single(u=>u.Kind==Kind.Heavy)); yield return Wait(()=>game.IsCarrying,5);
        game.PauseGame(true); float time=game.CarrySeconds; yield return new WaitForSeconds(.4f);
        Assert.That(game.CarrySeconds,Is.EqualTo(time)); Assert.That(game.Board.Turns,Is.Zero);
        game.PauseGame(false); yield return new WaitForSeconds(.1f); Assert.That(game.CarrySeconds,Is.GreaterThan(time));
    }
    [UnityTest] public IEnumerator ReleaseTransfersVisibleSwingMomentum()
    {
        game.Grab(game.Board.Units.Single(u=>u.Kind==Kind.Knight)); yield return Wait(()=>game.IsCarrying,5);
        game.Aim(new Cell(4,4)); yield return new WaitForSeconds(.35f); game.Drop();
        var toy=Object.FindObjectsByType<UnitView>(FindObjectsSortMode.None).Single(v=>v.Unit.Kind==Kind.Knight);
        var body=toy.GetComponent<Rigidbody>();
        Assert.That(body.isKinematic,Is.False); Assert.That(Mathf.Abs(body.linearVelocity.x),Is.GreaterThan(.2f));
        Assert.That(game.Board.Turns,Is.EqualTo(1));
        yield return Wait(()=>game.IsSelecting || game.IsResult,3);
    }
    [UnityTest] public IEnumerator PointerRaycastSelectsToy()
    {
            var toy=Object.FindObjectsByType<UnitView>(FindObjectsSortMode.None).Single(v=>v.Unit.Kind==Kind.Knight);
            Vector2 position=Camera.main.WorldToScreenPoint(toy.transform.position);
            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(Camera.main.ScreenPointToRay(position),out var hit,100),Is.True,"Toy ray misses all colliders");
            Assert.That(hit.collider.GetComponent<UnitView>(),Is.EqualTo(toy),$"Ray intercepted by {hit.collider.name}; pointer {position}, screen {Screen.width}x{Screen.height}");
            Assert.That(game.SelectAtScreenPosition(position),Is.True);
            yield return Wait(()=>game.IsCarrying,5); Assert.That(game.Board.Turns,Is.Zero);
    }
}
