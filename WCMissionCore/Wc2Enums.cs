namespace WCMissionCore;

/// <summary>WC2 ship class identifiers</summary>
public enum Wc2ShipClass : byte
{
    // Confed fighters
    Ferret = 0, Rapier = 1, Broadsword = 2, Epee = 3, Sabre = 4,
    // Kilrathi fighters
    Sartha = 5, Drakhri = 6, Jalkehi = 7, Grikath = 8, Strakha = 9,
    Bloodfang = 10,
    // Confed capital/transport
    Clydesdale = 11, FreeTrader = 12,
    // Kilrathi capital
    Dorkathi = 13,
    // Confed capital
    Crossbow = 15, Kamekh = 16,
    Waterloo = 17, Concordia = 18, Gilgamesh = 19,
    // Kilrathi capital
    Ralatha = 20, Fralthra = 21,
    // Stations and objects
    HumanStarbase = 23, HumanSupplyDepot = 24,
    KilrathiSupplyDepot = 25, KTithrakMang = 26,
    // Hazards
    Asteroids = 33, Mines = 34,
    // Expansion ships/hazards (SO1) — class 41 is asteroids in SO1, MorningStar in SO2
    GothriSO1 = 36, MorningStarOrAsteroids = 41, MinesSO1 = 42,
    // Misc
    AyersRock = 48,
    // Expansion ships/hazards (SO2)
    GothriSO2 = 50, AsteroidsSO2 = 51, MinesSO2 = 52,
    Empty = 255
}

/// <summary>WC2 named character IDs for dialogue</summary>
public enum Wc2Character : byte
{
    None = 0,
    // Confed
    Angel = 1, Hobbes = 3, Stingray = 4, Jazz = 6,
    Paladin = 8, Doomsday = 9, Bear = 10, Shadow = 11,
    Spirit = 14, MajorEdmond = 15,
    MaleCommOfficer = 16, FemaleCommOfficer = 17,
    MaleTerranPilot = 20, FemaleTerranPilot = 21,
    // Kilrathi
    PrinceThrakhath = 12, KhasraRedclaw = 22,
    RaktiBloodDrinker = 23, KurHumanKiller = 24,
    DrakhaiPilot = 25, RegularKilrathiPilot = 26,
    // Other
    MaleFreighterPilot = 18, FemaleFreighterPilot = 19,
    KilrathiCommOfficer = 27,
    ConfedThrakhathSO1 = 28, Pirate = 29,
    ManiacSO2 = 36, MandarinPilotSO2 = 37
}

/// <summary>WC2 ship orders</summary>
public enum Wc2Orders : byte
{
    Patrol = 0, Escort = 1, Attack = 2, Defend = 3,
    Wingman = 4, Flee = 5, GotoWarp = 6, WarpArrive = 7,
    Unknown8 = 8, Rendezvous = 9, ComeHome = 10,
    Unknown11 = 11, Unknown12 = 12, Unknown13 = 13, Unknown14 = 14,
    Inactive = 255
}
