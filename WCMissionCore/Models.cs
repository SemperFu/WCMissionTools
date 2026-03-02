namespace WCMissionCore;

/// <summary>A navigation point in a WC1 mission</summary>
public class NavPoint
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public int NavType { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int Radius { get; set; }

    /// <summary>Briefing note for this nav point (from map section)</summary>
    public string BriefingNote { get; set; } = "";

    /// <summary>4 trigger pairs (type, value)</summary>
    public int[][] Triggers { get; set; } = [[], [], [], []];

    /// <summary>Preload ship class indices</summary>
    public int[] Preloads { get; set; } = [];

    /// <summary>Ship indices present at this nav point</summary>
    public int[] ShipIndices { get; set; } = [];
}

/// <summary>A map/route waypoint shown during briefing</summary>
public class MapPoint
{
    public int Index { get; set; }
    public int IconFormat { get; set; }
    public int TargetIndex { get; set; }
    public string Description { get; set; } = "";
}

/// <summary>A ship definition in a WC1 mission</summary>
public class Ship
{
    public int Index { get; set; }
    public ShipClass Class { get; set; }
    public Allegiance Allegiance { get; set; }
    public int Leader { get; set; } = -1;
    public ShipOrders Orders { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int RotationX { get; set; }
    public int RotationY { get; set; }
    public int RotationZ { get; set; }
    public int Speed { get; set; }
    public int Size { get; set; }
    public Pilot Pilot { get; set; }
    public int AiLevel { get; set; }
    public int PrimaryTarget { get; set; } = -1;
    public int SecondaryTarget { get; set; } = -1;
    public int Formation { get; set; } = -1;
}

/// <summary>A single WC1 mission (sortie)</summary>
public class Mission
{
    public int SortieIndex { get; set; }
    public int SystemIndex { get; set; }
    public int MissionIndex { get; set; }
    public string WingName { get; set; } = "";
    public string SystemName { get; set; } = "";
    public List<NavPoint> NavPoints { get; set; } = [];
    public List<MapPoint> MapPoints { get; set; } = [];
    public List<Ship> Ships { get; set; } = [];
}

/// <summary>The full contents of a MODULE file</summary>
public class ModuleFile
{
    public string SourcePath { get; set; } = "";
    public List<Mission> Missions { get; set; } = [];

    /// <summary>Raw decompressed section bytes for 1:1 round-trip repacking</summary>
    public byte[][]? RawSections { get; set; }
}
