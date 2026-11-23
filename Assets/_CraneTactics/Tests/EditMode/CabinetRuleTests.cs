using System.Linq;
using CraneTactics;
using CraneTactics.Rules;
using NUnit.Framework;
using UnityEngine;
using Terrain=CraneTactics.Rules.Terrain;

public class CabinetRuleTests
{
    static readonly Cell East=new Cell(1,0), North=new Cell(0,1);
    // An otherwise empty terrain board keeps each rule readable and independent of cabinet tuning.
    static BoardState Board(params string[] map)
    {
        var d=new StageDefinition {Size=map.Length,Pit=new Cell(map.Length-1,map.Length-1),Map=map,Spawns=new Spawn[0]};
        for(int y=0;y<map.Length;y++) for(int x=0;x<map.Length;x++) if(map[map.Length-1-y][x]=='O') d.Pit=new Cell(x,y);
        return BoardState.FromDefinitionForTests(d);
    }
    static void Drop(BoardState b,Unit u,Cell at,Cell direction) => Assert.That(b.Resolve(b.CommitRelease(),u,at,direction,true),Is.True);

    [Test] public void IcingCarriesToyUntilSomethingStopsIt()
    {
        var b=Board("....","....","....","FFF."); var knight=b.Add(Kind.Knight,0,3,3); var far=b.Add(Kind.Guard,0,2,3);
        Drop(b,knight,new Cell(0,0),East);
        Assert.That(knight.Cell,Is.EqualTo(new Cell(3,0)),"Slides over icing and stops on the first plain cell");
        var c=Board("....","....","....","FF.."); var a=c.Add(Kind.Heavy,0,3,3); var wall=c.Add(Kind.Guard,1,0,3); c.Add(Kind.Knight,3,3,3);
        Drop(c,a,new Cell(0,0),East);
        Assert.That(wall.Hp,Is.EqualTo(3),"A blocked slide deals no damage"); Assert.That(a.Cell,Is.EqualTo(new Cell(0,0)));
    }
    [Test] public void IcingCanCarryAToyIntoTheChute()
    {
        var b=Board("....","FFFO","....","...."); var guard=b.Add(Kind.Guard,0,0,2); var knight=b.Add(Kind.Knight,0,3,3);
        Drop(b,guard,new Cell(0,2),East);
        Assert.That(guard.Alive,Is.False); Assert.That(b.Captured,Does.Contain(Kind.Guard)); Assert.That(b.Outcome,Is.EqualTo(Outcome.Victory));
        Assert.That(b.DirectCaptures,Is.Zero,"A slide is not a carried home capture");
    }
    [Test] public void JellyBouncesADropTwoCellsAndImpactsThere()
    {
        var b=Board("....","....","....",".J.."); var heavy=b.Add(Kind.Heavy,0,3,3); var guard=b.Add(Kind.Guard,3,0,3); b.Add(Kind.Knight,0,2,3);
        Drop(b,heavy,new Cell(1,0),East);
        Assert.That(guard.Hp,Is.EqualTo(1),"Heavy still lands with impact 2 after the bounce");
        Assert.That(heavy.Cell,Is.EqualTo(new Cell(3,0))); Assert.That(b.Resolution[0].Label,Is.EqualTo("bounce"));
    }
    [Test] public void KoromochiRollsToTheFirstObstacleAndPushes()
    {
        var b=Board(".....",".....",".....",".....","....."); var roll=b.Add(Kind.Koromochi,0,4,2); var guard=b.Add(Kind.Guard,3,0,2); b.Add(Kind.Knight,0,3,3);
        Drop(b,roll,new Cell(0,0),East);
        Assert.That(guard.Cell,Is.EqualTo(new Cell(4,0)),"Pushed one cell"); Assert.That(roll.Cell,Is.EqualTo(new Cell(3,0))); Assert.That(guard.Hp,Is.EqualTo(2));
    }
    [Test] public void PetapetanLeavesGumThatStopsARivalButNotItself()
    {
        var b=Board(".....",".....",".....",".....","....."); var peta=b.Add(Kind.Petapetan,0,4,2); var scout=b.Add(Kind.Scout,3,0,2); var knight=b.Add(Kind.Knight,0,1,3);
        Drop(b,peta,scout.Cell,East);
        Assert.That(b.TerrainAt(new Cell(3,0)),Is.EqualTo(Terrain.Gum)); Assert.That(peta.Cell,Is.EqualTo(new Cell(2,0)));
        Assert.That(scout.Cell,Is.EqualTo(new Cell(3,0)),"A stuck rival takes no step"); Assert.That(b.IsStuck(scout),Is.True); Assert.That(b.Plan().Actor,Is.Null);
        Drop(b,peta,new Cell(1,3),East); Assert.That(b.IsStuck(peta),Is.False,"Petapetan never sticks to its own gum");
        Assert.That(b.ResolveMiss(b.CommitRelease(),scout,East),Is.True); Assert.That(scout.Cell,Is.EqualTo(new Cell(3,0)),"Gum resists a claw nudge");
    }
    [Test] public void AKnightInGumCannotStrike()
    {
        var b=Board("....","....","....","G..."); var knight=b.Add(Kind.Knight,0,0,3); var scout=b.Add(Kind.Scout,1,0,1); var heavy=b.Add(Kind.Heavy,3,3,3);
        Assert.That(b.ResolveMiss(b.CommitRelease(),null,East),Is.True);
        Assert.That(scout.Alive,Is.True,"Its own hint says a toy in gum cannot act");
    }
    [Test] public void MagnetPullsMetalInLineAndClampsIt()
    {
        var b=Board(".....",".....",".....",".....","M...."); var guard=b.Add(Kind.Guard,3,0,3); var scout=b.Add(Kind.Scout,0,4,1); var heavy=b.Add(Kind.Heavy,4,4,3);
        Drop(b,heavy,new Cell(4,3),North);
        var pull=b.Resolution.Single(r=>r.Label=="magnet");
        Assert.That(pull.Poses.Single(p=>p.Unit==guard).Cell,Is.EqualTo(new Cell(2,0)),"Metal drifts one cell in its own phase, before the enemy acts");
        Assert.That(scout.Metal,Is.False);
        var c=Board("....","....","....","M.G."); var blocked=c.Add(Kind.Guard,3,0,3); c.Add(Kind.Slimy,1,0,2);
        Assert.That(c.MagnetPull(blocked),Is.Null,"A toy in between blocks the pull");
    }
    [Test] public void MagnetBehindTheChutePullsMetalIn()
    {
        var b=Board("....","....","....","..OM"); var guard=b.Add(Kind.Guard,1,0,3); var knight=b.Add(Kind.Knight,0,3,3);
        Drop(b,knight,new Cell(0,2),East);
        Assert.That(guard.Alive,Is.False); Assert.That(b.Outcome,Is.EqualTo(Outcome.Victory)); Assert.That(b.DirectCaptures,Is.Zero);
    }
    [Test] public void BeltCarriesAllRidersOnceAndFeedsTheChute()
    {
        var b=Board(".....",".....",".....",".....",">>>>O"); var front=b.Add(Kind.Guard,3,0,3); var rear=b.Add(Kind.Scout,2,0,3); var knight=b.Add(Kind.Knight,0,4,3);
        Drop(b,knight,new Cell(0,3),East);
        Assert.That(front.Alive,Is.False,"The front rider enters the chute"); Assert.That(rear.Cell.X,Is.EqualTo(3),"The rear rider moves exactly one cell");
    }
    [Test] public void DroppingClockworkAimsItAndItDashesWithFriendlyFire()
    {
        var b=Board("......","......","......","....G.","......","......");
        var robot=b.Add(Kind.Clockwork,0,0,3); robot.Hostile=true; var guard=b.Add(Kind.Guard,4,2,3); var knight=b.Add(Kind.Knight,0,5,3);
        Drop(b,robot,new Cell(1,2),East);
        Assert.That(robot.Facing,Is.EqualTo(East)); Assert.That(robot.Wind,Is.EqualTo(1),"Dropping rewinds, then this turn ticks once");
        Drop(b,knight,new Cell(0,4),East);
        Assert.That(guard.Hp,Is.EqualTo(1),"The dash hits the first toy on the line"); Assert.That(robot.Cell,Is.EqualTo(new Cell(3,2)));
        Assert.That(b.Resolution.Any(r=>r.Label=="clock-dash"),Is.True);
    }
    [Test] public void ClockworkForecastMatchesTheDash()
    {
        var b=Board("......","......","......","......","......","......");
        var robot=b.Add(Kind.Clockwork,0,1,3); b.Add(Kind.Guard,3,1,3);
        Assert.That(b.ClockworkPath(robot).Last(),Is.EqualTo(new Cell(3,1)));
    }
    [Test] public void MedalsSeparateClearParAndTrickShot()
    {
        var b=Board("....","FFFO","....","...."); var guard=b.Add(Kind.Guard,0,0,2); b.Add(Kind.Knight,0,3,3);
        Assert.That(b.Medals,Is.EqualTo(Medal.None));
        Drop(b,guard,new Cell(0,2),East);
        Assert.That(b.Medals&Medal.TrickShot,Is.EqualTo(Medal.TrickShot));
        var c=Board("....","...O","....","...."); var carried=c.Add(Kind.Guard,0,0,2); c.Add(Kind.Knight,0,3,3);
        Drop(c,carried,c.Pit,East);
        Assert.That(c.Outcome,Is.EqualTo(Outcome.Victory)); Assert.That(c.Medals&Medal.TrickShot,Is.EqualTo(Medal.None),"Carrying a rival home still clears, without the trick medal");
    }
    [Test] public void OneNightBoardsKeepHandoffLayoutsAndHaveNoTerrain()
    {
        var one=BoardState.Create(0); Assert.That(one.Pit,Is.EqualTo(new Cell(5,2))); Assert.That(one.TerrainCells,Is.Empty); Assert.That(one.Units.Select(u=>u.Kind),Is.EqualTo(new[]{Kind.Knight,Kind.Heavy,Kind.Scout,Kind.Guard}));
        var two=BoardState.Create(2); Assert.That(two.Pit,Is.EqualTo(new Cell(5,3))); Assert.That(two.TerrainCells,Is.Empty); Assert.That(two.Units.Single(u=>u.Kind==Kind.Scout).Attack,Is.EqualTo(2));
        Assert.That(BoardState.Create(1).IsPrize,Is.True);
    }
    // The exhaustive par proof runs on .NET (`Tests/Rules --report`); on Mono each stage replays its
    // reference line, proves no line one drop shorter where that search is quick, and checks home rush.
    [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)]
    public void CabinetStagesWinAtParAndRewardMoreThanHomeRush(int stage)
    {
        var board=BoardState.Create(stage); float speed=CabinetCatalog.ForStage(stage).ReturnSpeed;
        var line=BoardSolver.ReferenceLines[stage];
        Assert.That(line.Length,Is.EqualTo(board.Par));
        var played=board;
        foreach(var move in line)
        {
            Assert.That(BoardSolver.Moves(played,speed).Contains(move),Is.True,$"{move} must be a legal claw action");
            played=BoardSolver.Apply(played,move);
        }
        Assert.That(played.Outcome,Is.EqualTo(board.IsPrize?Outcome.Recruited:Outcome.Victory));
        if(board.Par>1 && board.Par<=3) Assert.That(BoardSolver.Solve(board,speed,board.Par-1).Fewest,Is.EqualTo(-1),"Par must be the true fewest drops");
        if(board.IsPrize) return;
        var rush=BoardSolver.HomeRush(board,speed);
        Assert.That(rush.Outcome!=Outcome.Victory || rush.Turns>board.Par,Is.True,$"Carrying rivals home took {rush.Turns} vs par {board.Par}");
    }
    [Test] public void FreeCarryReachesAnyCellWithinTheGripsSteps()
    {
        var b=Board("......","......","......","......","......","......"); b.Pit=new Cell(5,5);
        var knight=b.Add(Kind.Knight,0,0,3); var guard=b.Add(Kind.Guard,3,3,2); guard.GripScale=.3f;
        int reach=BoardSolver.Reach(b,guard,1.7f);
        Assert.That(reach,Is.EqualTo(2),"A rival's short grip carries it only a couple of steps");
        var guardLandings=BoardSolver.Moves(b,1.7f).Where(m=>!m.Contact && m.Unit==1).Select(m=>m.Landing).Distinct().ToList();
        Assert.That(guardLandings.All(c=>c.Distance(guard.Cell)<=reach),Is.True);
        Assert.That(guardLandings,Has.Member(new Cell(1,3)),"Any direction, not only toward the chute");
        Assert.That(guardLandings,Has.No.Member(b.Pit));
        var knightLandings=BoardSolver.Moves(b,1.7f).Where(m=>!m.Contact && m.Unit==0).ToList();
        Assert.That(knightLandings.Any(m=>m.Landing.Equals(new Cell(0,5))),Is.True,"An ally can be placed where you want it");
        Assert.That(knightLandings.Where(m=>m.Landing.Equals(guard.Cell)).Select(m=>m.Direction).Distinct().Count(),Is.GreaterThan(1),"Hitting a toy can come from different sides");
        Assert.That(knightLandings.Where(m=>m.Landing.Equals(new Cell(0,5))).Count(),Is.EqualTo(1),"On plain floor the heading changes nothing");
    }
    [Test] public void DeckButtonsDoNotOverlapAndHitTestIsRound()
    {
        var buttons=new[]{DeckButton.Left,DeckButton.Near,DeckButton.Right,DeckButton.Depth,DeckButton.Drop}.Select(b=>OneNightGame.DeckButtonRect(b,1280,800)).ToList();
        for(int i=1;i<buttons.Count;i++) {Assert.That(buttons[i-1].Overlaps(buttons[i]),Is.False); Assert.That(buttons[i-1].x,Is.LessThan(buttons[i].x));}
        var right=buttons[2]; var drop=buttons[4];
        Assert.That(buttons[0].xMin,Is.GreaterThan(400),"Room is left for the deck text");
        Assert.That(drop.xMax,Is.LessThanOrEqualTo(1280)); Assert.That(drop.yMax,Is.LessThanOrEqualTo(800));
        Assert.That(right.width,Is.GreaterThanOrEqualTo(96),"These are big buttons");
        Assert.That(OneNightGame.DeckHit(drop,drop.center),Is.True);
        Assert.That(OneNightGame.DeckHit(drop,new Vector2(drop.xMin+1,drop.yMin+1)),Is.False,"The sprite's corner is not the button");
    }
}
