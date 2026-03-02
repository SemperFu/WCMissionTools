namespace WCMissionCore;

/// <summary>A navigation point in a WC2 mission</summary>
public class Wc2NavPoint
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public int NavType { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int Radius { get; set; }
    public string BriefingNote { get; set; } = "";

    /// <summary>4 trigger pairs (type, target nav). Type -1 = none</summary>
    public int[][] Triggers { get; set; } = [[], [], [], []];

    /// <summary>3 preload ship class indices (WC2 has 3 vs WC1's 2)</summary>
    public int[] Preloads { get; set; } = [];

    /// <summary>Ship indices present at this nav point (10 slots)</summary>
    public int[] ShipIndices { get; set; } = [];
}

/// <summary>A flight plan entry shown during WC2 briefing</summary>
public class Wc2FlightPlan
{
    public int Index { get; set; }
    public int ObjectiveIcon { get; set; }
    public int TargetNav { get; set; }
    public string Description { get; set; } = "";
}

/// <summary>A ship definition in a WC2 mission</summary>
public class Wc2Ship
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public Wc2ShipClass Class { get; set; }
    public Allegiance Allegiance { get; set; }
    public Wc2Orders Orders { get; set; }
    public int FormationSlot { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int RotationX { get; set; }
    public int RotationY { get; set; }
    public int RotationZ { get; set; }
    public int Speed { get; set; }
    public int AiLevel { get; set; }
    public int PilotId { get; set; }
    public int Leader { get; set; } = -1;
    public int PrimaryTarget { get; set; } = -1;
    public int SecondaryTarget { get; set; } = -1;
    public Wc2Character Character { get; set; }
}

/// <summary>A single WC2 mission (sortie)</summary>
public class Wc2Mission
{
    public int SortieIndex { get; set; }
    public int SystemIndex { get; set; }
    public int MissionIndex { get; set; }
    public string MissionLabel { get; set; } = "";
    public string SystemName { get; set; } = "";
    public int TakeoffShip { get; set; } = -1;
    public int LandingShip { get; set; } = -1;
    public List<Wc2NavPoint> NavPoints { get; set; } = [];
    public List<Wc2FlightPlan> FlightPlans { get; set; } = [];
    public List<Wc2Ship> Ships { get; set; } = [];
}

/// <summary>The full contents of a WC2 MODULE file</summary>
public class Wc2ModuleFile
{
    public string SourcePath { get; set; } = "";
    public List<Wc2Mission> Missions { get; set; } = [];

    /// <summary>Raw decompressed section bytes for 1:1 round-trip repacking</summary>
    public byte[][]? RawSections { get; set; }
}
