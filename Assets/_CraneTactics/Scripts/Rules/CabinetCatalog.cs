using System.Collections.Generic;

namespace CraneTactics.Rules
{
    public sealed class Spawn
    {
        public Kind Kind; public int X,Y,Hp,Attack=1; public bool Hostile; public float GripScale=1; public Cell Facing=new Cell(1,0);
        public Spawn(Kind kind,int x,int y,int hp) {Kind=kind; X=x; Y=y; Hp=hp;}
    }
    public sealed class StageDefinition
    {
        public string Title,Hint; public int Size=6,Par; public Cell Pit; public bool Prize; public Spawn[] Spawns; public string[] Map;
        public Terrain TerrainAt(int x,int y)
        {
            if(Map==null) return Terrain.Floor;
            // Map rows read top to bottom as seen in the cabinet: first row is the back (highest Y).
            switch(Map[Size-1-y][x])
            {
                case 'F': return Terrain.Frosting; case 'J': return Terrain.Jelly; case 'M': return Terrain.Magnet; case 'G': return Terrain.Gum;
                case '^': return Terrain.BeltNorth; case 'v': return Terrain.BeltSouth; case '>': return Terrain.BeltEast; case '<': return Terrain.BeltWest;
                default: return Terrain.Floor;
            }
        }
    }
    public sealed class CabinetDefinition
    {
        public string Id,Name,Tagline,Friend,Machines; public int[] Stages; public float ReturnSpeed=1.7f; public Kind[] Team;
    }
    // One Night (stages 0-2) keeps its handoff-defined boards inside BoardState.Create.
    public static class CabinetCatalog
    {
        public static readonly CabinetDefinition[] Cabinets=
        {
            new CabinetDefinition {Id="classic",Team=new[]{Kind.Knight,Kind.Heavy,Kind.Slimy},Name="CLASSIC",Tagline="One Night: catch Slimy, win with three friends.",Friend="Slimy",Machines="The original board",Stages=new[]{0,1,2}},
            new CabinetDefinition {Id="sweets",ReturnSpeed=1.9f,Team=new[]{Kind.Knight,Kind.Heavy,Kind.Koromochi},Name="SWEETS",Tagline="Icing slides. Jelly bounces. Koromochi never stops rolling.",Friend="Koromochi",Machines="Icing, jelly, loose swing",Stages=new[]{3,4}},
            new CabinetDefinition {Id="magnet",ReturnSpeed=1.6f,Team=new[]{Kind.Knight,Kind.Heavy,Kind.Clockwork},Name="MAGNET LAB",Tagline="Metal toys drift to magnets. Wind a robot and aim it.",Friend="Clockwork Robot",Machines="Magnets, stiff arm",Stages=new[]{5,6}},
            new CabinetDefinition {Id="factory",Team=new[]{Kind.Knight,Kind.Slimy,Kind.Petapetan},Name="GUM FACTORY",Tagline="Belts carry everyone. Petapetan glues the rest in place.",Friend="Petapetan",Machines="Belts, gum",Stages=new[]{7,8}},
            new CabinetDefinition {Id="jackpot",Team=new[]{Kind.Knight,Kind.Heavy,Kind.Slimy,Kind.Koromochi,Kind.Petapetan,Kind.Clockwork},Name="JACKPOT",Tagline="Every machine, every friend, one golden chute.",Friend="Everyone",Machines="All of them",Stages=new[]{9}},
        };
        static readonly Dictionary<int,StageDefinition> stages=new Dictionary<int,StageDefinition>
        {
            [3]=new StageDefinition {Title="SWEETS  /  PRIZE",Hint="Koromochi slips fast. Let go early and let it roll home.",Size=4,Pit=new Cell(3,3),Prize=true,Par=1,
                Spawns=new[]{new Spawn(Kind.Koromochi,3,0,2){GripScale=.5f}},
                Map=new[]{"...O","...F","...F","...."}},
            [4]=new StageDefinition {Title="SWEETS  /  BATTLE",Hint="Icing carries toys until something stops them.",Par=2,Pit=new Cell(5,2),
                Spawns=new[]{new Spawn(Kind.Knight,0,5,3),new Spawn(Kind.Heavy,0,0,3),new Spawn(Kind.Koromochi,0,2,2),new Spawn(Kind.Scout,1,4,2){Attack=2,GripScale=.25f},new Spawn(Kind.Guard,2,2,3){GripScale=.25f}},
                Map=new[]{"......","..J...","......",".FFFFO","......","......"}},
            [5]=new StageDefinition {Title="MAGNET LAB  /  PRIZE",Hint="The wind-up robot drifts to the magnet. Beat it to the chute.",Size=4,Pit=new Cell(3,3),Prize=true,Par=1,
                Spawns=new[]{new Spawn(Kind.Clockwork,0,1,2)},
                Map=new[]{"...O","....","....","M..."}},
            [6]=new StageDefinition {Title="MAGNET LAB  /  BATTLE",Hint="Drop the robot facing a rival. It dashes when its key runs down.",Par=3,Pit=new Cell(4,3),
                Spawns=new[]{new Spawn(Kind.Knight,0,0,3),new Spawn(Kind.Heavy,0,5,3),new Spawn(Kind.Clockwork,0,2,2),new Spawn(Kind.Guard,1,3,3){GripScale=.3f},new Spawn(Kind.Guard,2,0,3){GripScale=.3f},new Spawn(Kind.Scout,2,5,2){Attack=2,GripScale=.3f}},
                Map=new[]{"......","......","....M.","....OM","......","M....."}},
            [7]=new StageDefinition {Title="GUM FACTORY  /  PRIZE",Hint="The belt never waits. Catch Petapetan as it rides past.",Size=4,Pit=new Cell(3,3),Prize=true,Par=1,
                Spawns=new[]{new Spawn(Kind.Petapetan,0,0,2)},
                Map=new[]{"...O","....",">>>^","...."}},
            [8]=new StageDefinition {Title="GUM FACTORY  /  BATTLE",Hint="A toy in gum cannot act. A belt can still feed the chute.",Par=3,Pit=new Cell(5,4),
                Spawns=new[]{new Spawn(Kind.Knight,0,0,3),new Spawn(Kind.Slimy,0,4,2),new Spawn(Kind.Petapetan,0,3,2),new Spawn(Kind.Clockwork,4,0,3){Hostile=true,Facing=new Cell(-1,0),GripScale=.5f},new Spawn(Kind.Scout,2,1,2){Attack=2,GripScale=.3f},new Spawn(Kind.Guard,1,2,3){GripScale=.3f}},
                Map=new[]{"......",".>>>>O",".....^","..G..^","......","......"}},
            [9]=new StageDefinition {Title="JACKPOT  /  FINALE",Hint="Icing, magnets and belts all lead to one golden chute.",Par=4,Pit=new Cell(5,5),
                Spawns=new[]{new Spawn(Kind.Knight,0,2,3),new Spawn(Kind.Heavy,0,0,3),new Spawn(Kind.Koromochi,0,5,2),new Spawn(Kind.Petapetan,2,0,2),new Spawn(Kind.Scout,2,3,2){Attack=2,GripScale=.3f},new Spawn(Kind.Guard,1,4,3){GripScale=.3f},new Spawn(Kind.Guard,1,1,3){GripScale=.3f},new Spawn(Kind.Clockwork,5,1,3){Hostile=true,Facing=new Cell(-1,0),GripScale=.5f}},
                Map=new[]{".FFFFO","...M.^",".....^","..J..^",".....^","......"}},
        };
        public static StageDefinition Stage(int stage) => stages[stage];
        public static CabinetDefinition ForStage(int stage) {foreach(var c in Cabinets) foreach(var s in c.Stages) if(s==stage) return c; return Cabinets[0];}
        // The stage after a clear within the same cabinet, or -1 at the cabinet's end.
        public static int NextInCabinet(int stage)
        {
            var stagesOfCabinet=ForStage(stage).Stages;
            for(int i=0;i<stagesOfCabinet.Length-1;i++) if(stagesOfCabinet[i]==stage) return stagesOfCabinet[i+1];
            return -1;
        }
    }
}
