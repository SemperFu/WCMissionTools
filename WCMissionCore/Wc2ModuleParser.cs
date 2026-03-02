using System.Text;

namespace WCMissionCore;

/// <summary>
/// Parses WC2 MODULE files.
/// 
/// File structure (uncompressed, 177,824 bytes):
///   Header: 4 bytes file size + 7x4-byte section entries = 32 bytes
///   Section 0 (offset 32):       Campaign routing (1,536 bytes)
///   Section 1 (offset 1,568):    Nav points -- 10 per mission (64,640 bytes)
///   Section 2 (offset 66,208):   Map/briefing data (32,768 bytes)
///   Section 3 (offset 98,976):   Ship definitions -- 16 per mission (61,440 bytes)
///   Section 4 (offset 160,416):  Mission labels at 40-byte intervals (2,560 bytes)
///   Section 5 (offset 162,976):  System names at 40-byte intervals (640 bytes)
///   Section 6 (offset 163,616):  Executable code/data (14,208 bytes)
/// </summary>
public class Wc2ModuleParser
{
    const int HeaderSize = 32;
    const int SectionCount = 7;

    // Section offsets (from uncompressed MODULE.000)
    const int NavSectionOffset = 1568;
    const int MapSectionOffset = 66208;
    const int ShipSectionOffset = 98976;
    const int LabelSectionOffset = 160416;
    const int SystemNameSectionOffset = 162976;

    const int NavsPerMission = 10;
    const int ShipsPerMission = 16;
    const int FlightPlansPerMission = 8; // 512/64 = 8 entries fit
    const int FlightPlanRecordSize = 64;

    const int LabelEntrySize = 40;
    const int SystemNameEntrySize = 40;

    // Record sizes -- derived from section sizes / (missions * records)
    // Nav section: 64,640 bytes / 64 missions = 1010 bytes/mission, / 10 navs = 101 bytes/nav
    // Ship section: 61,440 bytes / 64 missions = 960 bytes/mission, / 16 ships = 60 bytes/ship
    const int NavRecordSize = 101;
    const int ShipRecordSize = 60;
    const int NavBlockSize = NavRecordSize * NavsPerMission;   // 1010
    const int ShipBlockSize = ShipRecordSize * ShipsPerMission; // 960

    // Map section: 32,768 bytes / 64 missions = 512 bytes/mission
    const int MapBlockPerMission = 512;

    const int SystemCount = 16;
    const int MissionsPerSystem = 4;
    const int TotalMissions = SystemCount * MissionsPerSystem; // 64

    /// <summary>Detects whether a MODULE file is WC2 format (type byte 0xE0 or 0x20)</summary>
    public static bool IsWc2Module(byte[] data)
        => data.Length >= 8 && (data[7] == 0xE0 || data[7] == 0x20);

    public Wc2ModuleFile Parse(string path)
    {
        byte[] raw = File.ReadAllBytes(path);

        // Store raw decompressed sections for round-trip packing
        byte[][] rawSections = OriginContainerWriter.Read(raw);

        byte[] data = (raw[7] == 0x20) ? DecompressModule(raw) : raw;
        var module = new Wc2ModuleFile { SourcePath = path, RawSections = rawSections };

        for (int sys = 0; sys < SystemCount; sys++)
        {
            for (int mis = 0; mis < MissionsPerSystem; mis++)
            {
                int sortie = sys * MissionsPerSystem + mis;
                if (sortie >= TotalMissions) break;
                var mission = ParseMission(data, sys, mis, sortie);
                if (mission.NavPoints.Count > 0 || mission.Ships.Count > 0)
                    module.Missions.Add(mission);
            }
        }

        return module;
    }

    Wc2Mission ParseMission(byte[] data, int sys, int mis, int sortie)
    {
        // Read mission label from section 4
        string label = "";
        int labelOff = LabelSectionOffset + sortie * LabelEntrySize;
        if (labelOff + LabelEntrySize <= data.Length)
            label = ReadString(data, labelOff, LabelEntrySize);

        // Read system name from section 5
        string sysName = "";
        int sysOff = SystemNameSectionOffset + sys * SystemNameEntrySize;
        if (sysOff + SystemNameEntrySize <= data.Length)
            sysName = ReadString(data, sysOff, SystemNameEntrySize);

        var mission = new Wc2Mission
        {
            SortieIndex = sortie,
            SystemIndex = sys,
            MissionIndex = mis,
            MissionLabel = label,
            SystemName = sysName
        };

        ParseNavPoints(data, sortie, mission);
        ParseFlightPlans(data, sortie, mission);
        ParseShips(data, sortie, mission);

        // Link flight plan briefing text to nav points
        foreach (var fp in mission.FlightPlans)
        {
            if (fp.TargetNav >= 0 && fp.TargetNav < mission.NavPoints.Count)
            {
                var nav = mission.NavPoints.FirstOrDefault(n => n.Index == fp.TargetNav);
                if (nav != null && string.IsNullOrEmpty(nav.BriefingNote))
                    nav.BriefingNote = fp.Description.TrimStart('?', '.');
            }
        }

        return mission;
    }

    void ParseNavPoints(byte[] data, int sortie, Wc2Mission mission)
    {
        int baseOffset = NavSectionOffset + sortie * NavBlockSize;

        for (int n = 0; n < NavsPerMission; n++)
        {
            int off = baseOffset + n * NavRecordSize;
            if (off + NavRecordSize > data.Length) break;

            string name = ReadString(data, off, 30);
            int navType = data[off + 30];

            if (string.IsNullOrEmpty(name) && navType == 0) continue;

            // Coordinates: 3-byte signed at 4-byte stride
            int x = ReadSigned24(data, off + 32);
            int y = ReadSigned24(data, off + 36);
            int z = ReadSigned24(data, off + 40);

            // Radius: int16 LE at bytes 43-44
            int radius = data[off + 43] | (data[off + 44] << 8);

            // Triggers: 4 pairs at bytes 47-54 (signed bytes, -1 = none)
            var triggers = new int[4][];
            for (int t = 0; t < 4; t++)
            {
                int tType = (sbyte)data[off + 47 + t * 2];
                int tVal = (sbyte)data[off + 48 + t * 2];
                triggers[t] = tType == -1 ? [] : [tType, tVal];
            }

            // 8 unknown bytes at 55-62

            // 3 preloads at bytes 63-68 (int16 LE each, -1 = none)
            var preloads = new List<int>();
            for (int p = 0; p < 3; p++)
            {
                int pre = ReadSigned16(data, off + 63 + p * 2);
                if (pre != -1) preloads.Add(pre);
            }

            // 3+3 unknown int16 values at bytes 69-80

            // 10 ship indices at bytes 81-100 (int16 LE each, -1 = empty)
            var shipIndices = new List<int>();
            for (int s = 0; s < 10; s++)
            {
                int si = ReadSigned16(data, off + 81 + s * 2);
                if (si != -1) shipIndices.Add(si);
            }

            mission.NavPoints.Add(new Wc2NavPoint
            {
                Index = n,
                Name = name,
                NavType = navType,
                X = x, Y = y, Z = z,
                Radius = radius,
                Triggers = triggers,
                Preloads = [.. preloads],
                ShipIndices = [.. shipIndices]
            });
        }
    }

    void ParseFlightPlans(byte[] data, int sortie, Wc2Mission mission)
    {
        int baseOffset = MapSectionOffset + sortie * FlightPlansPerMission * FlightPlanRecordSize;

        for (int f = 0; f < FlightPlansPerMission; f++)
        {
            int off = baseOffset + f * FlightPlanRecordSize;
            if (off + FlightPlanRecordSize > data.Length) break;

            int icon = (sbyte)data[off];
            // byte 1 = pad
            int targetNav = (sbyte)data[off + 2];
            // byte 3 = pad
            string desc = ReadString(data, off + 4, 60);

            // Skip empty/unused entries
            if (icon == -1 && targetNav == -1) continue;
            if (string.IsNullOrEmpty(desc) || desc == "empty") continue;

            mission.FlightPlans.Add(new Wc2FlightPlan
            {
                Index = f,
                ObjectiveIcon = icon,
                TargetNav = targetNav,
                Description = desc
            });
        }
    }

    void ParseShips(byte[] data, int sortie, Wc2Mission mission)
    {
        int baseOffset = ShipSectionOffset + sortie * ShipBlockSize;

        for (int s = 0; s < ShipsPerMission; s++)
        {
            int off = baseOffset + s * ShipRecordSize;
            if (off + ShipRecordSize > data.Length) break;

            // Ship name: 20 bytes (0-19), null-terminated
            string name = ReadString(data, off, 20).Trim();

            byte cls = data[off + 20];        // Ship class
            // byte 21 = unknown, byte 22 = pad
            byte allegiance = data[off + 23];  // Allegiance
            byte orders = data[off + 24];      // Orders
            // byte 25 = unknown, byte 26 = pad
            int leader = ReadSigned16(data, off + 27); // Leader (signed int16)
            int formSlot = data[off + 29];     // Formation slot
            // byte 30 = pad

            // Coordinates: 3-byte signed with 1-byte gaps
            int x = ReadSigned24(data, off + 31);
            int y = ReadSigned24(data, off + 35);
            int z = ReadSigned24(data, off + 39);

            // Rotation: 3x int16 LE
            int rotX = ReadSigned16(data, off + 42);
            int rotY = ReadSigned16(data, off + 44);
            int rotZ = ReadSigned16(data, off + 46);

            // byte 48 = unknown
            int speed = ReadSigned16(data, off + 49);
            int aiLevel = data[off + 51];
            // byte 52 = pad
            int pilotId = data[off + 53];
            // byte 54 = pad, byte 55 = unknown
            int priTarget = (sbyte)data[off + 56];
            int secTarget = (sbyte)data[off + 57];
            // byte 58 = unknown
            Wc2Character character = (Wc2Character)data[off + 59];

            // Skip empty slots: class 0 + no name + allegiance 0 + all zeros
            if (cls == 0 && string.IsNullOrEmpty(name) && x == 0 && y == 0 && z == 0 && speed == 0)
                continue;

            mission.Ships.Add(new Wc2Ship
            {
                Index = s,
                Name = name,
                Class = (Wc2ShipClass)cls,
                Allegiance = (Allegiance)allegiance,
                Orders = (Wc2Orders)orders,
                FormationSlot = formSlot,
                X = x, Y = y, Z = z,
                RotationX = rotX, RotationY = rotY, RotationZ = rotZ,
                Speed = speed,
                AiLevel = aiLevel,
                PilotId = pilotId,
                Leader = leader,
                PrimaryTarget = priTarget,
                SecondaryTarget = secTarget,
                Character = character
            });
        }
    }

    static string ReadString(byte[] data, int offset, int maxLen)
    {
        int end = offset;
        while (end < offset + maxLen && end < data.Length && data[end] != 0)
            end++;
        return Encoding.Latin1.GetString(data, offset, end - offset);
    }

    static int ReadSigned24(byte[] data, int offset)
    {
        int lo = data[offset] | (data[offset + 1] << 8);
        int hi = data[offset + 2];
        int val = hi * 65536 + lo;
        return hi >= 128 ? val - 16777216 : val;
    }

    static int ReadSigned16(byte[] data, int offset)
    {
        int val = data[offset] | (data[offset + 1] << 8);
        return val >= 32768 ? val - 65536 : val;
    }

    /// <summary>
    /// Decompresses a WC2 MODULE file (type 0x20).
    /// Same Origin LZW compression as WC1, but with 7 sections instead of 6.
    /// </summary>
    static byte[] DecompressModule(byte[] compressed)
    {
        var sectionOffsets = new int[SectionCount];
        for (int i = 0; i < SectionCount; i++)
        {
            int off = 4 + i * 4;
            sectionOffsets[i] = compressed[off] | (compressed[off + 1] << 8) | (compressed[off + 2] << 16);
        }

        var sections = new byte[SectionCount][];
        int totalSize = HeaderSize;
        for (int i = 0; i < SectionCount; i++)
        {
            int secStart = sectionOffsets[i];
            uint uncompSize = BitConverter.ToUInt32(compressed, secStart);
            int lzwStart = secStart + 4;
            sections[i] = LzwDecompressor.Decompress(compressed, lzwStart, (int)uncompSize);
            totalSize += sections[i].Length;
        }

        byte[] output = new byte[totalSize];
        BitConverter.GetBytes((uint)totalSize).CopyTo(output, 0);

        int dataOffset = HeaderSize;
        for (int i = 0; i < SectionCount; i++)
        {
            int entryOff = 4 + i * 4;
            output[entryOff] = (byte)(dataOffset & 0xFF);
            output[entryOff + 1] = (byte)((dataOffset >> 8) & 0xFF);
            output[entryOff + 2] = (byte)((dataOffset >> 16) & 0xFF);
            output[entryOff + 3] = 0xE0; // mark as uncompressed

            sections[i].CopyTo(output, dataOffset);
            dataOffset += sections[i].Length;
        }

        return output;
    }
}
