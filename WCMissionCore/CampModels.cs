namespace WCMissionCore;

/// <summary>
/// Models for WC1 CAMP campaign files.
/// CAMP files define the branching mission tree, scoring, medals,
/// stellar backgrounds, and bar seating assignments.
/// </summary>

/// <summary>The full contents of a CAMP file</summary>
public class CampFile
{
    public string SourcePath { get; set; } = "";
    public List<StellarBackground> StellarBackgrounds { get; set; } = [];
    public List<SeriesBranch> SeriesBranches { get; set; } = [];
    public List<BarSeating> BarSeatings { get; set; } = [];

    /// <summary>Raw decompressed section bytes for 1:1 round-trip repacking</summary>
    public byte[][]? RawSections { get; set; }
}

/// <summary>Stellar background image and rotation for a sortie</summary>
public class StellarBackground
{
    public int SortieIndex { get; set; }
    public int Image { get; set; }
    public int RotationX { get; set; }
    public int RotationY { get; set; }
    public int RotationZ { get; set; }
}

/// <summary>Campaign branching data for a mission series</summary>
public class SeriesBranch
{
    public int SeriesIndex { get; set; }

    /// <summary>Character index of assigned wingman (0=Paladin..7=Iceman)</summary>
    public int Wingman { get; set; }

    /// <summary>Number of playable missions in this series (2-4)</summary>
    public int MissionsActive { get; set; }

    /// <summary>Total score threshold to take the winning path</summary>
    public int SuccessScore { get; set; }

    /// <summary>Cutscene index (-1=none, 0-3=midgame, 64=final win, 65=final lose)</summary>
    public int Cutscene { get; set; }

    /// <summary>Series index to branch to on win (-1 = campaign end)</summary>
    public int SuccessSeries { get; set; }

    /// <summary>Ship class awarded on winning path (0=Hornet..3=Raptor)</summary>
    public int SuccessShip { get; set; }

    /// <summary>Series index to branch to on loss (-1 = campaign end)</summary>
    public int FailureSeries { get; set; }

    /// <summary>Ship class assigned on losing path</summary>
    public int FailureShip { get; set; }

    /// <summary>Scoring data for each mission slot (always 4 entries)</summary>
    public List<MissionScoring> MissionScorings { get; set; } = [];
}

/// <summary>Scoring and medal thresholds for a single mission within a series</summary>
public class MissionScoring
{
    /// <summary>Medal type (0=none, 1=Bronze Star, 2=Silver Star, 4=Gold Star)</summary>
    public int Medal { get; set; }

    /// <summary>Kill score threshold required for medal</summary>
    public int MedalScore { get; set; }

    /// <summary>Points awarded per ship class kill (16 entries, indexed by ShipClass)</summary>
    public int[] FlightPathScoring { get; set; } = new int[16];
}

/// <summary>Characters seated at the bar for a sortie</summary>
public class BarSeating
{
    public int SortieIndex { get; set; }
    public int LeftSeat { get; set; }
    public int RightSeat { get; set; }
}
