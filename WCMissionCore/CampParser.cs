using System.Text;

namespace WCMissionCore;

/// <summary>
/// Parses WC1 CAMP campaign files (CAMP.000, .001, .002).
/// 
/// CAMP files use the standard Origin container format:
///   Header: 4 bytes file size + N×4-byte section entries
///   Section 0: Stellar backgrounds (8 bytes/record)
///   Section 1: Series branches — campaign tree (90 bytes/record)
///   Section 2: Bar seating (2 bytes/record)
/// 
/// WC1 files use type=0x01 (LZW compressed sections).
/// WC2 files use type=0xE0 (uncompressed sections).
/// </summary>
public class CampParser
{
    const int SectionsExpected = 3;
    const int StellarRecordSize = 8;
    const int SeriesRecordSize = 90;
    const int SeriesHeaderSize = 10;
    const int MissionScoringSize = 20;
    const int MissionsPerSeries = 4;
    const int FlightPathSize = 16;
    const int BarSeatingRecordSize = 2;

    public CampFile Parse(string path)
    {
        byte[] raw = File.ReadAllBytes(path);

        // Store raw decompressed sections for round-trip packing
        byte[][] rawSections = OriginContainerWriter.Read(raw);

        byte[] data = Decompress(raw);
        var camp = new CampFile { SourcePath = path, RawSections = rawSections };

        // Read section table
        var sections = ReadSectionTable(data);
        if (sections.Length < SectionsExpected)
            throw new InvalidDataException($"Expected {SectionsExpected} sections, found {sections.Length}");

        // Parse each section
        camp.StellarBackgrounds = ParseStellarBackgrounds(data, sections[0].Offset, sections[0].Length);
        camp.SeriesBranches = ParseSeriesBranches(data, sections[1].Offset, sections[1].Length);
        camp.BarSeatings = ParseBarSeatings(data, sections[2].Offset, sections[2].Length);

        return camp;
    }

    /// <summary>
    /// Decompresses a CAMP file if sections are LZW-compressed (type=0x01).
    /// Returns uncompressed data with section offsets updated.
    /// </summary>
    static byte[] Decompress(byte[] raw)
    {
        if (raw.Length < 8) throw new InvalidDataException("File too small");

        // Read section count from first offset
        int firstOff = raw[4] | (raw[5] << 8) | (raw[6] << 16);
        int sectionCount = (firstOff - 4) / 4;
        byte typeFlag = raw[7];

        // If uncompressed (0xE0 or 0x02), return as-is
        if (typeFlag != 0x01)
            return raw;

        // Read compressed section offsets
        var offsets = new int[sectionCount];
        for (int i = 0; i < sectionCount; i++)
        {
            int p = 4 + i * 4;
            offsets[i] = raw[p] | (raw[p + 1] << 8) | (raw[p + 2] << 16);
        }

        // Determine section boundaries
        var sectionEnds = new int[sectionCount];
        for (int i = 0; i < sectionCount - 1; i++)
            sectionEnds[i] = offsets[i + 1];
        sectionEnds[sectionCount - 1] = raw.Length;

        // Decompress each section
        var decompressed = new byte[sectionCount][];
        int headerSize = 4 + sectionCount * 4;
        int totalSize = headerSize;
        for (int i = 0; i < sectionCount; i++)
        {
            uint uncompSize = BitConverter.ToUInt32(raw, offsets[i]);
            int lzwStart = offsets[i] + 4;
            decompressed[i] = LzwDecompressor.Decompress(raw, lzwStart, (int)uncompSize);
            totalSize += decompressed[i].Length;
        }

        // Reassemble as uncompressed: header + raw section data
        byte[] output = new byte[totalSize];
        BitConverter.GetBytes((uint)totalSize).CopyTo(output, 0);

        int dataOffset = headerSize;
        for (int i = 0; i < sectionCount; i++)
        {
            int entryOff = 4 + i * 4;
            output[entryOff] = (byte)(dataOffset & 0xFF);
            output[entryOff + 1] = (byte)((dataOffset >> 8) & 0xFF);
            output[entryOff + 2] = (byte)((dataOffset >> 16) & 0xFF);
            output[entryOff + 3] = 0xE0; // mark as uncompressed

            decompressed[i].CopyTo(output, dataOffset);
            dataOffset += decompressed[i].Length;
        }

        return output;
    }

    struct SectionInfo
    {
        public int Offset;
        public int Length;
    }

    static SectionInfo[] ReadSectionTable(byte[] data)
    {
        int firstOff = data[4] | (data[5] << 8) | (data[6] << 16);
        int count = (firstOff - 4) / 4;
        var sections = new SectionInfo[count];

        for (int i = 0; i < count; i++)
        {
            int p = 4 + i * 4;
            sections[i].Offset = data[p] | (data[p + 1] << 8) | (data[p + 2] << 16);
        }

        // Calculate lengths
        int fileLen = (int)BitConverter.ToUInt32(data, 0);
        for (int i = 0; i < count - 1; i++)
            sections[i].Length = sections[i + 1].Offset - sections[i].Offset;
        sections[count - 1].Length = fileLen - sections[count - 1].Offset;

        return sections;
    }

    static List<StellarBackground> ParseStellarBackgrounds(byte[] data, int offset, int length)
    {
        int count = length / StellarRecordSize;
        var items = new List<StellarBackground>(count);

        for (int i = 0; i < count; i++)
        {
            int pos = offset + i * StellarRecordSize;
            items.Add(new StellarBackground
            {
                SortieIndex = i,
                Image = BitConverter.ToInt16(data, pos),
                RotationX = BitConverter.ToInt16(data, pos + 2),
                RotationY = BitConverter.ToInt16(data, pos + 4),
                RotationZ = BitConverter.ToInt16(data, pos + 6)
            });
        }

        return items;
    }

    static List<SeriesBranch> ParseSeriesBranches(byte[] data, int offset, int length)
    {
        int count = length / SeriesRecordSize;
        var items = new List<SeriesBranch>(count);

        for (int i = 0; i < count; i++)
        {
            int pos = offset + i * SeriesRecordSize;
            var series = new SeriesBranch
            {
                SeriesIndex = i,
                Wingman = data[pos],
                // data[pos + 1] is reserved
                MissionsActive = data[pos + 2],
                SuccessScore = BitConverter.ToInt16(data, pos + 3),
                Cutscene = (sbyte)data[pos + 5],
                SuccessSeries = (sbyte)data[pos + 6],
                SuccessShip = data[pos + 7],
                FailureSeries = (sbyte)data[pos + 8],
                FailureShip = data[pos + 9]
            };

            // Parse 4 mission scoring entries
            for (int m = 0; m < MissionsPerSeries; m++)
            {
                int mpos = pos + SeriesHeaderSize + m * MissionScoringSize;
                var scoring = new MissionScoring
                {
                    Medal = data[mpos],
                    // data[mpos + 1] is reserved
                    MedalScore = BitConverter.ToUInt16(data, mpos + 2)
                };

                for (int f = 0; f < FlightPathSize; f++)
                    scoring.FlightPathScoring[f] = data[mpos + 4 + f];

                series.MissionScorings.Add(scoring);
            }

            items.Add(series);
        }

        return items;
    }

    static List<BarSeating> ParseBarSeatings(byte[] data, int offset, int length)
    {
        int count = length / BarSeatingRecordSize;
        var items = new List<BarSeating>(count);

        for (int i = 0; i < count; i++)
        {
            int pos = offset + i * BarSeatingRecordSize;
            items.Add(new BarSeating
            {
                SortieIndex = i,
                LeftSeat = (sbyte)data[pos],
                RightSeat = (sbyte)data[pos + 1]
            });
        }

        return items;
    }
}
