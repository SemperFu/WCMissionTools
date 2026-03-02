using System.Text;

namespace WCMissionCore;

/// <summary>
/// Parses uncompressed WC1 MODULE files.
/// 
/// File structure (uncompressed):
///   Header: 4 bytes file size + 6x4-byte section entries + sentinel
///   Section 0 (offset 28):     Campaign routing data
///   Section 1 (offset ~1564):  Briefing text references  
///   Nav Points (offset 6492):  15 systems x 4928 bytes/system
///     Each system: 4 missions x 1232 bytes/mission
///     Each mission: 16 nav slots x 77 bytes/nav
///   Map Points (offset 84509): 64-byte entries per sortie
///   Ships (offset 145948):     32 ships x 42 bytes = 1344 bytes/sortie
///   Section 4 (offset ~231964): Difficulty names
///   Section 5 (offset ~234524): Squadron names
/// </summary>
public class ModuleParser
{
    // Structural constants derived from HCl's QBasic editor and WCE mission editor
    const int NavSectionOffset = 6492;
    const int MapSectionOffset = 84509;
    const int ShipSectionOffset = 151324;     // section 3 start (145948) + 5376 byte preamble

    const int SystemBlockSize = 4928;   // bytes per star system
    const int MissionBlockSize = 1232;  // bytes per mission within a system
    const int NavRecordSize = 77;       // bytes per nav point
    const int NavsPerMission = 16;      // max nav slots per mission
    const int MapRecordSize = 64;       // bytes per map description entry
    const int ShipRecordSize = 42;      // bytes per ship
    const int ShipsPerMission = 32;     // max ship slots per mission
    const int ShipBlockSize = ShipRecordSize * ShipsPerMission; // 1344

    const int WingSectionOffset = 231964;  // section 4: difficulty names (160 bytes) + wing names (40 bytes each)
    const int WingEntryOffset = 160;       // wing names start after 4×40 byte difficulty names
    const int WingEntrySize = 40;
    const int SystemNameSectionOffset = 234524; // section 5: system names (40 bytes each)
    const int SystemNameEntrySize = 40;

    const int SystemCount = 15;
    const int MissionsPerSystem = 4;

    public ModuleFile Parse(string path)
    {
        byte[] raw = File.ReadAllBytes(path);

        // Store raw decompressed sections for round-trip packing
        byte[][] rawSections = OriginContainerWriter.Read(raw);

        byte[] data = IsCompressed(raw) ? DecompressModule(raw) : raw;
        var module = new ModuleFile { SourcePath = path, RawSections = rawSections };

        for (int sys = 0; sys < SystemCount; sys++)
        {
            for (int mis = 0; mis < MissionsPerSystem; mis++)
            {
                int sortie = sys * MissionsPerSystem + mis;
                var mission = ParseMission(data, sys, mis, sortie);
                if (mission.NavPoints.Count > 0 || mission.Ships.Count > 0)
                    module.Missions.Add(mission);
            }
        }

        return module;
    }

    /// <summary>Checks section type byte — type 1 = compressed, type 2 = uncompressed</summary>
    static bool IsCompressed(byte[] data)
        => data.Length >= 8 && data[7] == 1;

    /// <summary>
    /// Decompresses a compressed MODULE file.
    /// Header: 2-byte magic "EV" + 2 padding + 6 section entries (3-byte offset + 1-byte type=1).
    /// Each compressed section starts with uint32 uncompressed size, followed by LZW data.
    /// Returns data identical in layout to an uncompressed MODULE file.
    /// </summary>
    static byte[] DecompressModule(byte[] compressed)
    {
        const int sectionCount = 6;
        const int headerSize = 4 + sectionCount * 4; // filesize(4) + 6 entries(24) = 28

        // Read section offsets from compressed header
        var sectionOffsets = new int[sectionCount];
        for (int i = 0; i < sectionCount; i++)
        {
            int off = 4 + i * 4;
            sectionOffsets[i] = compressed[off] | (compressed[off + 1] << 8) | (compressed[off + 2] << 16);
        }

        // Decompress each section
        var sections = new byte[sectionCount][];
        int totalSize = headerSize;
        for (int i = 0; i < sectionCount; i++)
        {
            int secStart = sectionOffsets[i];
            uint uncompSize = BitConverter.ToUInt32(compressed, secStart);
            int lzwStart = secStart + 4;
            sections[i] = LzwDecompressor.Decompress(compressed, lzwStart, (int)uncompSize);
            totalSize += sections[i].Length;
        }

        // Reassemble as uncompressed MODULE: filesize(4) + 6 section entries(type=2) + section data
        byte[] output = new byte[totalSize];
        BitConverter.GetBytes((uint)totalSize).CopyTo(output, 0);

        int dataOffset = headerSize;
        for (int i = 0; i < sectionCount; i++)
        {
            int entryOff = 4 + i * 4;
            output[entryOff] = (byte)(dataOffset & 0xFF);
            output[entryOff + 1] = (byte)((dataOffset >> 8) & 0xFF);
            output[entryOff + 2] = (byte)((dataOffset >> 16) & 0xFF);
            output[entryOff + 3] = 2;

            sections[i].CopyTo(output, dataOffset);
            dataOffset += sections[i].Length;
        }

        return output;
    }

    Mission ParseMission(byte[] data, int sys, int mis, int sortie)
    {
        // Read wing name from section 4
        string wingName = "";
        int wingOff = WingSectionOffset + WingEntryOffset + sortie * WingEntrySize;
        if (wingOff + WingEntrySize <= data.Length)
            wingName = ReadString(data, wingOff, WingEntrySize);

        // Read system name from section 5
        string sysName = "";
        int sysOff = SystemNameSectionOffset + (sys + 1) * SystemNameEntrySize; // +1 to skip "Squadron" header
        if (sysOff + SystemNameEntrySize <= data.Length)
            sysName = ReadString(data, sysOff, SystemNameEntrySize);

        var mission = new Mission
        {
            SortieIndex = sortie,
            SystemIndex = sys,
            MissionIndex = mis,
            WingName = wingName,
            SystemName = sysName
        };

        ParseNavPoints(data, sys, mis, mission);
        ParseMapPoints(data, sortie, mission);
        ParseShips(data, sortie, mission);

        // Link map briefing text to corresponding nav points
        foreach (var mp in mission.MapPoints)
        {
            var nav = mission.NavPoints.FirstOrDefault(n => n.Index == mp.Index);
            if (nav != null)
                nav.BriefingNote = mp.Description.TrimStart('?', '.');
        }

        return mission;
    }

    void ParseNavPoints(byte[] data, int sys, int mis, Mission mission)
    {
        int baseOffset = NavSectionOffset + sys * SystemBlockSize + mis * MissionBlockSize;

        for (int n = 0; n < NavsPerMission; n++)
        {
            int off = baseOffset + n * NavRecordSize;
            if (off + NavRecordSize > data.Length) break;

            string name = ReadString(data, off, 30);
            int navType = data[off + 30];

            // Skip empty nav slots
            if (string.IsNullOrEmpty(name) && navType == 0) continue;

            int x = ReadSigned24(data, off + 32);
            int y = ReadSigned24(data, off + 36);
            int z = ReadSigned24(data, off + 40);

            var triggers = new int[4][];
            for (int t = 0; t < 4; t++)
            {
                int tType = data[off + 45 + t * 2];
                int tVal = data[off + 46 + t * 2];
                triggers[t] = (tType == 0 && tVal == 0) || tType == 255 ? [] : [tType, tVal];
            }

            // Preloads at offsets 53, 55
            var preloads = new List<int>();
            int pre1 = data[off + 53];
            int pre2 = data[off + 55];
            if (pre1 != 255) preloads.Add(pre1);
            if (pre2 != 255) preloads.Add(pre2);

            // Ship indices at offsets 57, 59, 61, ..., 75 (10 slots, 2 bytes each)
            var shipIndices = new List<int>();
            for (int s = 0; s < 10; s++)
            {
                int si = data[off + 57 + s * 2];
                if (si != 255) shipIndices.Add(si);
            }

            mission.NavPoints.Add(new NavPoint
            {
                Index = n,
                Name = name,
                NavType = navType,
                X = x, Y = y, Z = z,
                Triggers = triggers,
                Preloads = [.. preloads],
                ShipIndices = [.. shipIndices]
            });
        }
    }

    void ParseMapPoints(byte[] data, int sortie, Mission mission)
    {
        // Map descriptions: 64-byte entries, starting at MapSectionOffset
        // Addressing: sortie-based, up to 16 entries per sortie
        int baseOffset = MapSectionOffset + sortie * MapRecordSize * NavsPerMission;

        for (int m = 0; m < NavsPerMission; m++)
        {
            int off = baseOffset + m * MapRecordSize;
            if (off + MapRecordSize > data.Length) break;

            int iconFormat = data[off];
            int targetIdx = data[off + 2];
            string desc = ReadString(data, off + 3, 61);

            // Skip empty entries
            if (iconFormat == 255 && targetIdx == 255) continue;
            if (string.IsNullOrEmpty(desc)) continue;

            mission.MapPoints.Add(new MapPoint
            {
                Index = m,
                IconFormat = iconFormat,
                TargetIndex = targetIdx,
                Description = desc
            });
        }
    }

    void ParseShips(byte[] data, int sortie, Mission mission)
    {
        int baseOffset = ShipSectionOffset + sortie * ShipBlockSize;

        for (int s = 0; s < ShipsPerMission; s++)
        {
            int off = baseOffset + s * ShipRecordSize;
            if (off + ShipRecordSize > data.Length) break;

            byte cls = data[off];
            if (cls == 255) continue; // empty slot

            byte allegiance = data[off + 2];
            byte leader = data[off + 4];
            byte orders = data[off + 6];

            int x = ReadSigned24(data, off + 10);
            int y = ReadSigned24(data, off + 14);
            int z = ReadSigned24(data, off + 18);

            int rotX = ReadSigned16(data, off + 22);
            int rotY = ReadSigned16(data, off + 24);
            int rotZ = ReadSigned16(data, off + 26);

            int speed = BitConverter.ToInt16(data, off + 28);
            int pilot = data[off + 32];
            int aiLevel = data[off + 30]; // field between size and pilot area
            int secTarget = data[off + 39];
            int formation = data[off + 40];
            int priTarget = data[off + 41];

            mission.Ships.Add(new Ship
            {
                Index = s,
                Class = (ShipClass)cls,
                Allegiance = (Allegiance)allegiance,
                Leader = leader == 255 ? -1 : leader,
                Orders = (ShipOrders)orders,
                X = x, Y = y, Z = z,
                RotationX = rotX, RotationY = rotY, RotationZ = rotZ,
                Speed = speed * 10,
                Size = 0,
                Pilot = (Pilot)pilot,
                AiLevel = aiLevel,
                PrimaryTarget = priTarget == 255 ? -1 : priTarget,
                SecondaryTarget = secTarget == 255 ? -1 : secTarget,
                Formation = formation == 255 ? -1 : formation
            });
        }
    }

    static string ReadString(byte[] data, int offset, int maxLen)
    {
        int end = offset;
        while (end < offset + maxLen && end < data.Length && data[end] != 0)
            end++;
        return Encoding.ASCII.GetString(data, offset, end - offset);
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
}
