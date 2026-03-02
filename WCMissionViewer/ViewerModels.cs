using WCMissionCore;

namespace WCMissionViewer;

/// <summary>Unified nav point for viewer display (works with both WC1 and WC2)</summary>
class ViewerNavPoint
{
    public int Index { get; init; }
    public string Name { get; init; } = "";
    public int NavType { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Z { get; init; }
    public int Radius { get; init; }
    public string BriefingNote { get; init; } = "";
    public int[][] Triggers { get; init; } = [];
    public int[] Preloads { get; init; } = [];
    public string[] PreloadNames { get; init; } = [];
    public int[] ShipIndices { get; init; } = [];
}

/// <summary>Unified ship for viewer display</summary>
class ViewerShip
{
    public int Index { get; init; }
    public string ClassName { get; init; } = "";
    public Allegiance Allegiance { get; init; }
    public string PilotName { get; init; } = "";
    public string OrdersName { get; init; } = "";
    public int X { get; init; }
    public int Y { get; init; }
    public int Z { get; init; }
    public int RotationX { get; init; }
    public int RotationY { get; init; }
    public int RotationZ { get; init; }
    public int Speed { get; init; }
    public int Size { get; init; }
    public int AiLevel { get; init; }
    public int Leader { get; init; } = -1;
    public int PrimaryTarget { get; init; } = -1;
    public int SecondaryTarget { get; init; } = -1;
    public int Formation { get; init; } = -1;
    public bool IsCarrier { get; init; }
    public bool IsAsteroidField { get; init; }
    public bool IsMineField { get; init; }
}

/// <summary>Unified briefing/flight plan entry</summary>
class ViewerBriefingItem
{
    public int Icon { get; init; }
    public int TargetNav { get; init; }
    public string Description { get; init; } = "";
}

/// <summary>Unified mission for viewer display</summary>
class ViewerMission
{
    public int SortieIndex { get; init; }
    public int SystemIndex { get; init; }
    public int MissionIndex { get; init; }
    public string Label { get; init; } = "";
    public string SystemName { get; init; } = "";
    public List<ViewerNavPoint> NavPoints { get; init; } = [];
    public List<ViewerShip> Ships { get; init; } = [];
    public List<ViewerBriefingItem> Briefing { get; init; } = [];
}

/// <summary>Converts WC1/WC2 parser models into unified viewer models</summary>
static class ViewerModelConverter
{
    public static List<ViewerMission> FromWc1(ModuleFile module)
    {
        return module.Missions.Select(m => new ViewerMission
        {
            SortieIndex = m.SortieIndex,
            SystemIndex = m.SystemIndex,
            MissionIndex = m.MissionIndex,
            Label = m.WingName,
            SystemName = m.SystemName?.Trim() ?? "",
            NavPoints = m.NavPoints.Select(n => new ViewerNavPoint
            {
                Index = n.Index, Name = n.Name, NavType = n.NavType,
                X = n.X, Y = n.Y, Z = n.Z, Radius = n.Radius,
                BriefingNote = n.BriefingNote,
                Triggers = n.Triggers, Preloads = n.Preloads,
                PreloadNames = n.Preloads.Select(p => ((ShipClass)p).ToString()).ToArray(),
                ShipIndices = n.ShipIndices
            }).ToList(),
            Ships = m.Ships.Select(s =>
            {
                bool isField = s.Class is ShipClass.AsteroidField or ShipClass.MineField;
                return new ViewerShip
                {
                    Index = s.Index, ClassName = s.Class.ToString(),
                    Allegiance = s.Allegiance,
                    PilotName = isField ? "" : s.Pilot.ToString(),
                    OrdersName = s.Orders.ToString(),
                    X = s.X, Y = s.Y, Z = s.Z,
                    RotationX = s.RotationX, RotationY = s.RotationY, RotationZ = s.RotationZ,
                    Speed = isField ? 0 : s.Speed,
                    Size = isField ? s.Speed : 0,
                    AiLevel = s.AiLevel,
                    Leader = s.Leader, PrimaryTarget = s.PrimaryTarget,
                    SecondaryTarget = s.SecondaryTarget, Formation = s.Formation,
                    IsCarrier = s.Class == ShipClass.TigersClaw,
                    IsAsteroidField = s.Class == ShipClass.AsteroidField,
                    IsMineField = s.Class == ShipClass.MineField
                };
            }).ToList(),
            Briefing = m.MapPoints.Select(mp => new ViewerBriefingItem
            {
                Icon = mp.IconFormat, TargetNav = mp.TargetIndex,
                Description = mp.Description
            }).ToList()
        }).ToList();
    }

    /// <param name="moduleNumber">MODULE file number (0=base, 1=SO1, 2=SO2) for class ID disambiguation</param>
    public static List<ViewerMission> FromWc2(Wc2ModuleFile module, int moduleNumber = 0)
    {
        return module.Missions.Select(m => new ViewerMission
        {
            SortieIndex = m.SortieIndex,
            SystemIndex = m.SystemIndex,
            MissionIndex = m.MissionIndex,
            Label = m.MissionLabel,
            SystemName = m.SystemName?.Trim() ?? "",
            NavPoints = m.NavPoints.Select(n => new ViewerNavPoint
            {
                Index = n.Index, Name = n.Name, NavType = n.NavType,
                X = n.X, Y = n.Y, Z = n.Z, Radius = n.Radius,
                BriefingNote = n.BriefingNote,
                Triggers = n.Triggers, Preloads = n.Preloads,
                PreloadNames = n.Preloads.Select(p => ((Wc2ShipClass)p).ToString()).ToArray(),
                ShipIndices = n.ShipIndices
            }).ToList(),
            Ships = m.Ships.Select(s =>
            {
                // Class 41 is asteroids in SO1, MorningStar in SO2
                bool is41Asteroid = s.Class == Wc2ShipClass.MorningStarOrAsteroids && moduleNumber == 1;
                bool is41MorningStar = s.Class == Wc2ShipClass.MorningStarOrAsteroids && moduleNumber != 1;
                bool isAsteroid = s.Class is Wc2ShipClass.Asteroids or Wc2ShipClass.AsteroidsSO2 || is41Asteroid;
                bool isMine = s.Class is Wc2ShipClass.Mines or Wc2ShipClass.MinesSO1 or Wc2ShipClass.MinesSO2;
                bool isField = isAsteroid || isMine;
                string className = is41Asteroid ? "Asteroids"
                    : is41MorningStar ? "MorningStar"
                    : s.Class == Wc2ShipClass.AsteroidsSO2 ? "Asteroids"
                    : s.Class == Wc2ShipClass.MinesSO1 ? "Mines"
                    : s.Class == Wc2ShipClass.MinesSO2 ? "Mines"
                    : s.Class.ToString();
                return new ViewerShip
                {
                    Index = s.Index, ClassName = className,
                    Allegiance = s.Allegiance,
                    PilotName = isField ? "" : s.Character.ToString(),
                    OrdersName = s.Orders.ToString(),
                    X = s.X, Y = s.Y, Z = s.Z,
                    RotationX = s.RotationX, RotationY = s.RotationY, RotationZ = s.RotationZ,
                    Speed = isField ? 0 : s.Speed,
                    Size = isField ? s.Speed : 0,
                    AiLevel = s.AiLevel,
                    Leader = s.Leader, PrimaryTarget = s.PrimaryTarget,
                    SecondaryTarget = s.SecondaryTarget, Formation = s.FormationSlot,
                    IsCarrier = s.Class is Wc2ShipClass.HumanStarbase or Wc2ShipClass.Concordia,
                    IsAsteroidField = isAsteroid,
                    IsMineField = isMine
                };
            }).ToList(),
            Briefing = m.FlightPlans.Select(fp => new ViewerBriefingItem
            {
                Icon = fp.ObjectiveIcon, TargetNav = fp.TargetNav,
                Description = fp.Description
            }).ToList()
        }).ToList();
    }
}
