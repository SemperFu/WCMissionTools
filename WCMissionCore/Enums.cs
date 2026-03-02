namespace WCMissionCore;

/// <summary>WC1 ship class identifiers</summary>
public enum ShipClass : byte
{
    Hornet = 0, Rapier = 1, Scimitar = 2, Raptor = 3,
    Venture = 4, Diligent = 5, Drayman = 6, Exeter = 7,
    TigersClaw = 8, Salthi = 9, Dralthi = 10, Krant = 11,
    Gratha = 12, Jalthi = 13, Hhriss = 14, Dorkir = 15,
    Lumbari = 16, Ralari = 17, Fralthi = 18, Snakeir = 19,
    Sivar = 20, Starpost = 21, AsteroidField = 22, MineField = 23,
    Empty = 255
}

/// <summary>Faction allegiance</summary>
public enum Allegiance : byte
{
    Confed = 0, Kilrathi = 1, Neutral = 2
}

/// <summary>Ship AI orders</summary>
public enum ShipOrders : byte
{
    Attack = 0, Patrol = 1, AttackTarget = 2, Escort = 3,
    Follow = 4, Defend = 5, JumpOut = 6, JumpIn = 7,
    GoHome = 8, Autopilot = 9, Navigate = 10,
    Inactive = 255
}

/// <summary>Named WC1 pilots</summary>
public enum Pilot : byte
{
    Generic0 = 0, Generic1 = 1, Generic2 = 2, Generic3 = 3, Generic4 = 4,
    Spirit = 5, Hunter = 6, Bossman = 7, Iceman = 8, Angel = 9,
    Paladin = 10, Maniac = 11, Knight = 12, Blair = 13,
    BhurakStarkiller = 14, DakhathDeathstroke = 15,
    KhajjaTheFang = 16, BakhtoshRedclaw = 17
}
