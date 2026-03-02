namespace WCMissionCore;

/// <summary>
/// Writes Origin Systems container format files (used by WC1/WC2 MODULE, CAMP, BRIEFING, etc.)
///
/// Container layout:
///   [0..3]  int32 LE — total file size (including this field)
///   [4..4+N*4-1] section table — N entries of 4 bytes each:
///     [0..2] uint24 LE — byte offset of section data from start of file
///     [3]    uint8    — format type (0x01=compressed, 0xE0=uncompressed, 0xFF=empty)
///   [header_end..] section data blobs (concatenated)
///
/// When compress=true, each section is prefixed with a 4-byte decompressed size
/// then LZW-compressed data (matching original game format).
/// When compress=false, sections are written raw with type=0xE0 (like WCToolbox).
/// </summary>
public static class OriginContainerWriter
{
    const byte TypeCompressed = 0x01;
    const byte TypeUncompressed = 0xE0;
    const byte TypeEmpty = 0xFF;

    /// <summary>
    /// Writes an Origin container file from decompressed section data.
    /// </summary>
    /// <param name="sections">Decompressed section data. Null or empty byte[] entries become empty sections (type=0xFF).</param>
    /// <param name="compress">If true, LZW-compress each section (type=0x01 with 4-byte size prefix). If false, write raw (type=0xE0).</param>
    /// <returns>Complete binary file contents ready to write to disk.</returns>
    public static byte[] Write(byte[][] sections, bool compress = false)
    {
        int sectionCount = sections.Length;
        int headerSize = 4 + sectionCount * 4; // file size + section table

        // Build section blobs
        var blobs = new byte[sectionCount][];
        var types = new byte[sectionCount];

        for (int i = 0; i < sectionCount; i++)
        {
            byte[]? sectionData = sections[i];
            if (sectionData == null || sectionData.Length == 0)
            {
                blobs[i] = [];
                types[i] = TypeEmpty;
            }
            else if (compress)
            {
                byte[] compressed = LzwCompressor.Compress(sectionData);
                // Prepend 4-byte decompressed size
                blobs[i] = new byte[4 + compressed.Length];
                BitConverter.GetBytes(sectionData.Length).CopyTo(blobs[i], 0);
                compressed.CopyTo(blobs[i], 4);
                types[i] = TypeCompressed;
            }
            else
            {
                blobs[i] = sectionData;
                types[i] = TypeUncompressed;
            }
        }

        // Calculate offsets
        int[] offsets = new int[sectionCount];
        int dataStart = headerSize;
        int currentOffset = dataStart;
        for (int i = 0; i < sectionCount; i++)
        {
            offsets[i] = currentOffset;
            currentOffset += blobs[i].Length;
        }
        int totalSize = currentOffset;

        // Write output
        using var ms = new MemoryStream(totalSize);
        var writer = new BinaryWriter(ms);

        // File size header
        writer.Write(totalSize);

        // Section table
        for (int i = 0; i < sectionCount; i++)
        {
            // For empty sections, use the same offset as next section (or file end)
            int off = offsets[i];
            // 3-byte LE offset + 1-byte type
            writer.Write((byte)(off & 0xFF));
            writer.Write((byte)((off >> 8) & 0xFF));
            writer.Write((byte)((off >> 16) & 0xFF));
            writer.Write(types[i]);
        }

        // Section data
        for (int i = 0; i < sectionCount; i++)
        {
            if (blobs[i].Length > 0)
                writer.Write(blobs[i]);
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Reads an Origin container and returns the decompressed section data.
    /// Useful for round-trip verification: Read → Write → compare.
    /// </summary>
    public static byte[][] Read(byte[] fileData)
    {
        int fileSize = BitConverter.ToInt32(fileData, 0);

        // First section table entry tells us where data starts,
        // which tells us how many entries are in the table
        int firstOffset = fileData[4] | (fileData[5] << 8) | (fileData[6] << 16);
        int sectionCount = (firstOffset - 4) / 4;

        // Read section table
        var entries = new (int offset, byte type)[sectionCount];
        for (int i = 0; i < sectionCount; i++)
        {
            int pos = 4 + i * 4;
            entries[i] = (
                fileData[pos] | (fileData[pos + 1] << 8) | (fileData[pos + 2] << 16),
                fileData[pos + 3]
            );
        }

        // Decompress each section
        byte[][] sections = new byte[sectionCount][];
        for (int i = 0; i < sectionCount; i++)
        {
            int start = entries[i].offset;
            int end = (i + 1 < sectionCount) ? entries[i + 1].offset : fileSize;
            int length = end - start;

            if (entries[i].type == TypeEmpty || length <= 0)
            {
                sections[i] = [];
            }
            else if (entries[i].type == TypeCompressed)
            {
                int decompSize = BitConverter.ToInt32(fileData, start);
                sections[i] = LzwDecompressor.Decompress(fileData, start + 4, decompSize);
            }
            else
            {
                // Uncompressed (0xE0) or other — copy raw
                sections[i] = new byte[length];
                Array.Copy(fileData, start, sections[i], 0, length);
            }
        }

        return sections;
    }
}
