# WCMissionPacker

Command-line tool that repacks Wing Commander 1/2 binary data files into the Origin Systems container format. Companion to [WCMissionParser](../WCMissionParser/README.md) — the parser reads game files, the packer writes them back.

## What It Does

Reads Origin container files (MODULE, CAMP, BRIEFING) **or JSON files exported by WCMissionParser**, and writes a new binary file. Both input paths produce **byte-for-byte identical** output to the original game files.

**Two input modes:**
- **Binary input**: Reads the original game file directly, decompresses sections, repacks
- **JSON input**: Reads JSON exported by `WCMissionParser -json -single-file`, extracts embedded raw section data (base64), repacks

| Mode | Section Type | Size | Use Case |
|------|-------------|------|----------|
| **Uncompressed** (default) | `0xE0` | Larger (~2-10×) | Quick modding, matches WCToolbox output |
| **Compressed** (`-compress`) | `0x01` | Original size | Safe for DOS game, matches original files exactly |

The LZW compressor produces **byte-for-byte identical** output to Origin's original 1990 compressor — verified across all 9 WC1 game files (109 compressed sections).

## Usage

```
WCMissionPacker <input> [-o | --output <path>] [-c | --compress] [-v | --verify]
```

### Examples

```bash
# Round-trip: parse to JSON, then pack back to identical binary
cd ..\WCMissionParser
dotnet run -- "D:\Games\Wing Commander\GAMEDAT\MODULE.000" --json --single-file --output .\temp
cd ..\WCMissionPacker
dotnet run -- ".\temp\MODULE.json" --compress --verify --output ".\MODULE.000"
# Result: byte-for-byte identical to original (SHA256 verified)

# Repack binary directly (no JSON step)
dotnet run -- "D:\Games\Wing Commander\GAMEDAT\MODULE.000" --compress

# Repack uncompressed (like WCToolbox)
dotnet run -- "D:\Games\Wing Commander\GAMEDAT\MODULE.000"

# Pack from JSON without compression
dotnet run -- ".\CAMP.json" --output ".\CAMP.000"
```

### Options
| Option | Short | Description |
|--------|-------|-------------|
| `--output` | `-o` | Output file path. Defaults to `<name>.packed.<ext>` in the same directory. |
| `--compress` | `-c` | LZW-compress sections (type=0x01). Without this flag, writes uncompressed (type=0xE0). |
| `--verify` | `-v` | After writing, reads back the file and verifies all section data matches. |

## Origin Container Format

All WC1/WC2 data files share the same container structure:

```
[0..3]      int32 LE    Total file size (including this field)
[4..4+N*4]  Section table: N entries × 4 bytes each
              [0..2]  uint24 LE   Byte offset of section data
              [3]     uint8       Format type
[data_start..]        Section data blobs (concatenated)
```

### Section Type Bytes

| Value | Enum | Meaning |
|-------|------|---------|
| `0x01` | Compressed | LZW compressed. First 4 bytes of section data = decompressed size (int32 LE), followed by compressed stream. |
| `0x20` | Compressed2 | Alternate compression (rare). |
| `0xE0` | Uncompressed | Raw data, never compressed. What WCToolbox writes. |
| `0xFF` | Empty | Section has no data (offset points to next section or file end). |

### LZW Compression Details

Origin's LZW variant:
- Variable-width codes: starts at 9 bits, grows to 12 bits max
- Code 256 = clear/reset dictionary
- Code 257 = end of stream
- First valid code after clear = 258
- Max dictionary size: 4096 entries
- Bit packing: LSB-first
- Each compressed section is prefixed with a 4-byte decompressed size (int32 LE)

## Compression vs Uncompressed: When to Use Which

**Use `-compress` (recommended for game modding):**
- WC1 shipped on floppy disks — uncompressed MODULE.000 is 235KB vs 22KB compressed (10.6×), significant for 720KB/1.44MB media
- Guarantees compatibility with the original DOS game engine
- Produces byte-for-byte identical files to the originals

**Use default (uncompressed) for:**
- Quick testing and iteration
- Unity remake (reads both formats)
- Compatibility with WCToolbox workflows

## Verified Game Files

All 9 WC1 game files verified via full round-trip: **Original → Parse → JSON → Pack = byte-for-byte identical** (SHA256 matched). Both direct binary repack and JSON round-trip produce identical output.

| File | Sections | Original | Uncompressed | Compressed | Round-Trip |
|------|----------|----------|--------------|------------|------------|
| CAMP.000 | 3 | 651 B | 1,706 B | 651 B | ✓ 1:1 |
| CAMP.001 | 3 | 456 B | 1,706 B | 456 B | ✓ 1:1 |
| CAMP.002 | 3 | 437 B | 1,706 B | 437 B | ✓ 1:1 |
| MODULE.000 | 6 | 22,085 B | 235,164 B | 22,085 B | ✓ 1:1 |
| MODULE.001 | 6 | 18,595 B | 235,164 B | 18,595 B | ✓ 1:1 |
| MODULE.002 | 6 | 17,765 B | 235,164 B | 17,765 B | ✓ 1:1 |
| BRIEFING.000 | 56 | 222,366 B | 351,761 B | 222,366 B | ✓ 1:1 |
| BRIEFING.001 | 36 | 85,455 B | 153,564 B | 85,455 B | ✓ 1:1 |
| BRIEFING.002 | 40 | 90,407 B | 160,839 B | 90,407 B | ✓ 1:1 |

## Solution Structure

Part of the **WCMissionTools** solution:

```
WCMissionTools/
├── WCMissionCore/          ← Shared library (models, parsers, LZW, container I/O)
│   ├── Models.cs / Enums.cs           Module data models & enums
│   ├── Wc2Models.cs / Wc2Enums.cs     WC2-specific models
│   ├── CampModels.cs / CampParser.cs  Campaign parser
│   ├── BriefingModels.cs / BriefingParser.cs  Briefing parser
│   ├── ModuleParser.cs / Wc2ModuleParser.cs   Module parsers
│   ├── LzwDecompressor.cs             Origin LZW decompression
│   ├── LzwCompressor.cs               Origin LZW compression
│   └── OriginContainerWriter.cs       Container read/write
├── WCMissionParser/        ← CLI tool: binary → text/JSON
├── WCMissionPacker/        ← CLI tool: repack binary files (this project)
└── WCMissionViewer/        ← WPF visual mission viewer
```

## Future Work

- **Edit workflow**: Parse → edit JSON fields → rebuild section bytes from models → repack (currently round-trips raw sections; editing parsed fields requires format-specific binary writers)
- **WC2 support**: WC2 files use both Origin container and IFF/FORM chunk formats
