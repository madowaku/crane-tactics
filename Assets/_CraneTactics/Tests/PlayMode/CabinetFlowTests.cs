using System.Collections;
using System.Linq;
using CraneTactics;
using CraneTactics.Rules;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

public class CabinetFlowTests
{
    OneNightGame game;
    [UnitySetUp] public IEnumerator Setup() {game=new GameObject("CabinetTest").AddComponent<OneNightGame>(); game.Automation=true; yield return null; game.SetAutoReturn(true); yield return null;}
    [UnityTearDown] public IEnumerator Cleanup() {Object.Destroy(game.gameObject); yield return null;}
    IEnumerator Wait(System.Func<bool> condition,float timeout=14)
    {
        float end=Time.time+timeout; while(!condition() && Time.time<end) yield return null;
        Assert.That(condition(),Is.True,"Cabinet state timed out");
    }
    // Plays like a person: carry with the direction controls, release by reading the ring.
    IEnumerator Perform(CraneMove move)
    {
        var unit=game.Board.Units[move.Unit];
        string plan=move.Contact?$"nudge {unit.Kind} {unit.Cell} dir {move.Direction}":$"{unit.Kind} {unit.Cell}->{move.Landing} dir {move.Direction}";
        yield return game.PlayMove(move);
        Assert.That(game.IsSelecting || game.IsResult,Is.True,"Move timed out: "+plan);
        Debug.Log($"CABINET_MOVE {plan} landed {game.LastLanding} dir {game.LastDirection} state {State()}");
    }
    string State() => string.Join(" ",game.Board.Units.Select(u=>$"{u.Kind}{u.Cell}:{u.Hp}"));
    [UnityTest] public IEnumerator EveryCabinetLoadsItsBoardAndRecoversFromRestart()
    {
        for(int i=0;i<CabinetCatalog.Cabinets.Length;i++)
        {
            game.LoadCabinet(i); yield return null; yield return null;
            Assert.That(game.Board.Stage,Is.EqualTo(CabinetCatalog.Cabinets[i].Stages[0]));
            Assert.That(game.IsSelecting,Is.True);
            Assert.That(Object.FindObjectsByType<UnitView>(FindObjectsSortMode.None).Count(v=>v.gameObject.activeInHierarchy),Is.EqualTo(game.Board.Units.Count));
        }
    }
    // Real claw + PhysX, released by reading the landing ring: the design par must be physically reachable.
    [UnityTest] public IEnumerator EveryPrizeAndBattleReachesItsParThroughTheRealClaw([ValueSource(nameof(PlayableStages))] int stage)
    {
        game.LoadStageForTests(stage); yield return null;
        var line=BoardSolver.ReferenceLines[stage];
        foreach(var move in line)
        {
            Assert.That(game.Board.Outcome,Is.EqualTo(Outcome.Playing),"Finished before the planned line ended");
            Assert.That(game.Board.Units[move.Unit].Alive,Is.True,"Physics diverged from the grid plan: "+State());
            yield return Perform(move);
        }
        var alive=game.Board.Units.Where(u=>u.Alive).Select(u=>u.Cell).ToList();
        Assert.That(alive.Distinct().Count(),Is.EqualTo(alive.Count),"One live toy per cell");
        Assert.That(game.Board.Outcome,Is.EqualTo(game.Board.IsPrize?Outcome.Recruited:Outcome.Victory),State());
        Assert.That(game.Board.Turns,Is.EqualTo(game.Board.Par));
        Assert.That((game.Board.Medals&Medal.Par)!=0,Is.True);
    }
    static readonly int[] PlayableStages={3,4,5,6,7,8,9};
    [UnityTest] public IEnumerator AnAllyCanBeCarriedAwayFromTheChuteAndPlaced()
    {
        game.LoadStageForTests(0); yield return null;
        var knight=game.Board.Units.Single(u=>u.Kind==Kind.Knight);
        Assert.That(knight.Cell,Is.EqualTo(new Cell(1,4)));
        yield return game.PlayMove(new CraneMove {Unit=0,Landing=new Cell(0,1),Direction=BoardSolver.StillDirection(knight.Cell,new Cell(0,1))});
        Assert.That(game.LastLanding,Is.EqualTo(new Cell(0,1)),"The claw goes where the player steers, away from the chute");
        Assert.That(game.Board.Turns,Is.EqualTo(1));
    }
    [UnityTest] public IEnumerator OneNightEndingStillReplaysAndOffersFloor()
    {
        game.LoadCabinet(0); yield return null;
        Assert.That(game.Board.Stage,Is.Zero); Assert.That(game.FloorOpen,Is.False);
        game.OpenFloor(true); Assert.That(game.ActiveDeckButton,Is.EqualTo(DeckButton.None)); game.OpenFloor(false);
        Assert.That(game.ActiveDeckButton,Is.EqualTo(DeckButton.Right));
        for(int i=0;i<4;i++) game.PickInput(true,.05f);
        game.PickInput(false,.01f); Assert.That(game.ActiveDeckButton,Is.EqualTo(DeckButton.Depth));
    }
    // Real Input System mouse events through the deck, with automation off: this is the player's path.
    [UnityTest] public IEnumerator MouseOnGlowingButtonsDrivesTheWholeCraneAction()
    {
        // The unfocused test runner would otherwise discard pointer events before they reach the game.
        var background=InputSystem.settings.backgroundBehavior; var editorRouting=InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var mouse=InputSystem.AddDevice<Mouse>(); mouse.MakeCurrent();
        try
        {
            game.Automation=false; game.LoadStageForTests(0); yield return null;
            IEnumerator Send(Vector2 guiPoint,bool down)
            {
                var state=new MouseState {position=new Vector2(guiPoint.x,Screen.height-guiPoint.y)};
                if(down) state=state.WithButton(MouseButton.Left);
                InputSystem.QueueStateEvent(mouse,state); yield return null; yield return null;
            }
            var right=OneNightGame.DeckButtonRect(DeckButton.Right,Screen.width,Screen.height).center;
            var depth=OneNightGame.DeckButtonRect(DeckButton.Depth,Screen.width,Screen.height).center;
            var drop=OneNightGame.DeckButtonRect(DeckButton.Drop,Screen.width,Screen.height).center;
            yield return Send(right,false);
            yield return Send(new Vector2(Screen.width/2f,Screen.height/3f),true);
            float aim=game.AimPoint.x; yield return new WaitForSeconds(.2f);
            Assert.That(game.AimPoint.x,Is.EqualTo(aim),"Holding over the board does not move the claw");
            yield return Send(new Vector2(Screen.width/2f,Screen.height/3f),false);
            yield return Send(depth,true); yield return new WaitForSeconds(.1f);
            Assert.That(game.ActiveDeckButton,Is.EqualTo(DeckButton.Right),"The unlit button does nothing");
            yield return Send(depth,false);
            yield return Send(right,true);
            float end=Time.time+5; while(game.AimPoint.x<.95f && Time.time<end) yield return null;
            Assert.That(game.AimPoint.x,Is.GreaterThanOrEqualTo(.95f),"Holding the lit button 1 moves the claw right");
            yield return Send(right,false);
            Assert.That(game.ActiveDeckButton,Is.EqualTo(DeckButton.Depth),"Letting go of 1 locks the axis and lights 2");
            yield return Send(depth,true);
            end=Time.time+5; while(game.AimPoint.z<2.95f && Time.time<end) yield return null;
            yield return Send(depth,false);
            yield return Wait(()=>game.IsCarrying);
            Assert.That(game.ActiveDeckButton,Is.EqualTo(DeckButton.Drop));
            Assert.That(game.DeckLit(DeckButton.Left) && game.DeckLit(DeckButton.Near) && game.DeckLit(DeckButton.Right) && game.DeckLit(DeckButton.Depth),Is.True,"All four direction domes light while carrying");
            yield return new WaitForSeconds(.15f);
            var still=game.ClawPosition; yield return new WaitForSeconds(.2f);
            Assert.That(Vector3.Distance(still,game.ClawPosition),Is.LessThan(.02f),"The claw waits for the player; it does not head home by itself");
            var left=OneNightGame.DeckButtonRect(DeckButton.Left,Screen.width,Screen.height).center;
            float startX=game.ClawPosition.x;
            yield return Send(left,true); yield return new WaitForSeconds(.35f); yield return Send(left,false);
            Assert.That(game.ClawPosition.x,Is.LessThan(startX-.2f),"Holding ← carries the toy left, away from the chute");
            Assert.That(game.IsCarrying,Is.True,"Steering is not a release");
            Assert.That(game.Board.Turns,Is.Zero);
            yield return Send(drop,false); yield return Send(drop,true); yield return Send(drop,false);
            Assert.That(game.IsCarrying,Is.False,"Pressing DROP releases");
            yield return Wait(()=>game.IsSelecting || game.IsResult);
            Assert.That(game.Board.Turns,Is.EqualTo(1),"One press, one action");
        }
        finally
        {
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior=background; InputSystem.settings.editorInputBehaviorInPlayMode=editorRouting;
        }
    }
    [UnityTest] public IEnumerator DropButtonLightsOnlyWhileCarrying()
    {
        game.Grab(game.Board.Units.Single(u=>u.Kind==Kind.Knight));
        Assert.That(game.ActiveDeckButton,Is.EqualTo(DeckButton.None));
        Assert.That(game.DeckLit(DeckButton.Left),Is.False,"Direction domes stay dark until a toy is held");
        yield return Wait(()=>game.IsCarrying);
        Assert.That(game.ActiveDeckButton,Is.EqualTo(DeckButton.Drop)); Assert.That(game.DeckLit(DeckButton.Near),Is.True);
        game.Drop(); Assert.That(game.ActiveDeckButton,Is.EqualTo(DeckButton.None)); Assert.That(game.DeckLit(DeckButton.Near),Is.False);
        yield return Wait(()=>game.IsSelecting || game.IsResult);
    }
}
