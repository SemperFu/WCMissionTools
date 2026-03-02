namespace WCMissionCore;

/// <summary>
/// Models for WC1 BRIEFING files.
/// BRIEFING files contain all dialogue, briefings, debriefings,
/// bar conversations, funerals, and medal ceremonies.
/// </summary>

/// <summary>The full contents of a BRIEFING file</summary>
public class BriefingFile
{
    public string SourcePath { get; set; } = "";
    public List<ConversationBlock> Blocks { get; set; } = [];

    /// <summary>Raw decompressed section bytes for 1:1 round-trip repacking</summary>
    public byte[][]? RawSections { get; set; }
}

/// <summary>A conversation block (one per section: Funeral, Office, Ceremony, S##M#)</summary>
public class ConversationBlock
{
    public int BlockIndex { get; set; }

    /// <summary>Block type name: Funeral, Office, Ceremony, Unused, or S##M# (e.g. S01M0)</summary>
    public string BlockType { get; set; } = "";

    public List<Conversation> Conversations { get; set; } = [];
}

/// <summary>A single conversation within a block</summary>
public class Conversation
{
    public int ConversationIndex { get; set; }
    public List<DialogSetting> Settings { get; set; } = [];
    public List<DialogLine> Dialogs { get; set; } = [];
}

/// <summary>Scene composition settings for a dialog line</summary>
public class DialogSetting
{
    /// <summary>Background image index (-1 = keep previous, -2 = fade/clear)</summary>
    public int Background { get; set; }

    /// <summary>Foreground character image index (-1 = keep previous, -2 = fade/clear)</summary>
    public int Foreground { get; set; }

    /// <summary>Text color index (-1 = keep previous)</summary>
    public int TextColor { get; set; }

    /// <summary>Timing delay in game ticks</summary>
    public int Delay { get; set; }
}

/// <summary>A single line of dialogue</summary>
public class DialogLine
{
    public int DialogIndex { get; set; }

    /// <summary>Display text with variable tokens ($C=callsign, $R=rank, $N=name, etc.)</summary>
    public string Text { get; set; } = "";

    /// <summary>Phonetic text for lip-sync animation</summary>
    public string LipSyncText { get; set; } = "";

    /// <summary>Facial expression animation codes (e.g. "RA45,81,01,A35,81,01,A50,81,02,")</summary>
    public string FacialExpressions { get; set; } = "";

    /// <summary>Conditional display commands (e.g. "[30]:01,01;")</summary>
    public string Commands { get; set; } = "";
}
