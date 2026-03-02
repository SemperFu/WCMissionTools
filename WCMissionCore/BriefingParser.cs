using System.Text;

namespace WCMissionCore;

/// <summary>
/// Parses WC1 BRIEFING files (BRIEFING.000, .001, .002).
/// 
/// BRIEFING files use the standard Origin container format:
///   Header: 4 bytes file size + N×4-byte section entries
///   Each section = one ConversationBlock (LZW compressed, type=0x01)
///   Empty sections have type=0xFF.
///
/// Decompressed section layout:
///   Offset table: C×2 uint32 entries (pairs: settings_offset, strings_offset)
///   Per conversation: M×13-byte setting records + null-terminated string blob
///
/// Setting record (13 bytes):
///   [0]    int8   Foreground image index
///   [1]    int8   TextColor index
///   [2]    int8   Background image index
///   [3-4]  int16  Delay (game ticks)
///   [5-6]  uint16 Commands string offset (relative to strings blob)
///   [7-8]  uint16 Text string offset
///   [9-10] uint16 LipSync string offset
///   [11-12] uint16 FacialExpressions string offset
/// </summary>
public class BriefingParser
{
    const int SettingRecordSize = 13;

    // Block type names: first 4 are special, then 13 series × 4 missions
    static readonly string[] BlockTypeNames = BuildBlockTypeNames();

    static string[] BuildBlockTypeNames()
    {
        var names = new List<string> { "Funeral", "Office", "Ceremony", "Unused" };
        for (int s = 1; s <= 13; s++)
            for (int m = 0; m <= 3; m++)
                names.Add($"S{s:D2}M{m}");
        return names.ToArray();
    }

    public BriefingFile Parse(string path)
    {
        byte[] raw = File.ReadAllBytes(path);

        // Store raw decompressed sections for round-trip packing
        byte[][] rawSections = OriginContainerWriter.Read(raw);

        var briefing = new BriefingFile { SourcePath = path, RawSections = rawSections };

        // Read section table from container header
        var sections = ReadSectionTable(raw);

        for (int i = 0; i < sections.Length; i++)
        {
            string blockType = i < BlockTypeNames.Length ? BlockTypeNames[i] : $"Block{i}";

            if (sections[i].Type == 0xFF || sections[i].Length <= 0)
            {
                briefing.Blocks.Add(new ConversationBlock
                {
                    BlockIndex = i,
                    BlockType = blockType
                });
                continue;
            }

            byte[] sectionData = DecompressSection(raw, sections[i]);
            var block = ParseConversationBlock(sectionData, i, blockType);
            briefing.Blocks.Add(block);
        }

        return briefing;
    }

    struct SectionEntry
    {
        public int Offset;
        public int Length;
        public byte Type;
    }

    static SectionEntry[] ReadSectionTable(byte[] raw)
    {
        if (raw.Length < 8) throw new InvalidDataException("File too small");

        int firstOff = raw[4] | (raw[5] << 8) | (raw[6] << 16);
        int count = (firstOff - 4) / 4;
        var sections = new SectionEntry[count];

        for (int i = 0; i < count; i++)
        {
            int p = 4 + i * 4;
            sections[i].Offset = raw[p] | (raw[p + 1] << 8) | (raw[p + 2] << 16);
            sections[i].Type = raw[p + 3];
        }

        // Calculate lengths
        int fileLen = raw.Length;
        for (int i = 0; i < count - 1; i++)
        {
            // Find next section with a different offset
            int nextOff = fileLen;
            for (int j = i + 1; j < count; j++)
            {
                if (sections[j].Offset > sections[i].Offset)
                {
                    nextOff = sections[j].Offset;
                    break;
                }
            }
            sections[i].Length = nextOff - sections[i].Offset;
        }
        if (count > 0)
            sections[count - 1].Length = fileLen - sections[count - 1].Offset;

        return sections;
    }

    static byte[] DecompressSection(byte[] raw, SectionEntry section)
    {
        if (section.Type == 0x01)
        {
            uint decompSize = BitConverter.ToUInt32(raw, section.Offset);
            return LzwDecompressor.Decompress(raw, section.Offset + 4, (int)decompSize);
        }
        // Uncompressed — copy raw section data
        byte[] data = new byte[section.Length];
        Array.Copy(raw, section.Offset, data, 0, section.Length);
        return data;
    }

    static ConversationBlock ParseConversationBlock(byte[] data, int blockIndex, string blockType)
    {
        var block = new ConversationBlock
        {
            BlockIndex = blockIndex,
            BlockType = blockType
        };

        if (data.Length < 8) return block;

        // Read offset table: pairs of uint32 (settings_offset, strings_offset)
        // Find how many entries by checking the first offset value
        uint firstOffset = BitConverter.ToUInt32(data, 0);
        int offsetTableSize = (int)firstOffset;
        int entryCount = offsetTableSize / 4;

        if (entryCount < 2 || entryCount % 2 != 0) return block;

        int conversationCount = entryCount / 2;
        var offsets = new uint[entryCount];
        for (int i = 0; i < entryCount; i++)
            offsets[i] = BitConverter.ToUInt32(data, i * 4);

        for (int ci = 0; ci < conversationCount; ci++)
        {
            int settingsStart = (int)offsets[ci * 2];
            int stringsStart = (int)offsets[ci * 2 + 1];

            // Determine settings end (= strings start for this conversation)
            int settingsLength = stringsStart - settingsStart;
            if (settingsLength < 0 || settingsLength % SettingRecordSize != 0) continue;

            int settingCount = settingsLength / SettingRecordSize;

            // Determine strings blob end
            int stringsEnd;
            if (ci + 1 < conversationCount)
                stringsEnd = (int)offsets[(ci + 1) * 2];
            else
                stringsEnd = data.Length;

            var conversation = ParseConversation(data, ci, settingsStart, settingCount,
                stringsStart, stringsEnd);
            block.Conversations.Add(conversation);
        }

        return block;
    }

    static Conversation ParseConversation(byte[] data, int index,
        int settingsStart, int settingCount, int stringsStart, int stringsEnd)
    {
        var conv = new Conversation { ConversationIndex = index };

        // Track unique dialog lines to avoid duplicates
        var seenTextOffsets = new HashSet<int>();
        int dialogIndex = 0;

        for (int i = 0; i < settingCount; i++)
        {
            int pos = settingsStart + i * SettingRecordSize;
            if (pos + SettingRecordSize > data.Length) break;

            var setting = new DialogSetting
            {
                Foreground = (sbyte)data[pos],
                TextColor = (sbyte)data[pos + 1],
                Background = (sbyte)data[pos + 2],
                Delay = BitConverter.ToInt16(data, pos + 3)
            };
            conv.Settings.Add(setting);

            // Read string offsets (relative to strings blob start)
            int cmdOffset = BitConverter.ToUInt16(data, pos + 5);
            int textOffset = BitConverter.ToUInt16(data, pos + 7);
            int lipOffset = BitConverter.ToUInt16(data, pos + 9);
            int faceOffset = BitConverter.ToUInt16(data, pos + 11);

            // Each unique text offset = one dialog line
            if (!seenTextOffsets.Contains(textOffset))
            {
                seenTextOffsets.Add(textOffset);
                var dialog = new DialogLine
                {
                    DialogIndex = dialogIndex++,
                    Commands = ReadNullTermString(data, stringsStart + cmdOffset, stringsEnd),
                    Text = ReadNullTermString(data, stringsStart + textOffset, stringsEnd),
                    LipSyncText = ReadNullTermString(data, stringsStart + lipOffset, stringsEnd),
                    FacialExpressions = ReadNullTermString(data, stringsStart + faceOffset, stringsEnd)
                };
                conv.Dialogs.Add(dialog);
            }
        }

        return conv;
    }

    static string ReadNullTermString(byte[] data, int offset, int maxEnd)
    {
        if (offset < 0 || offset >= data.Length || offset >= maxEnd)
            return "";

        int end = offset;
        while (end < data.Length && end < maxEnd && data[end] != 0)
            end++;

        if (end == offset) return "";
        return Encoding.Latin1.GetString(data, offset, end - offset);
    }
}
