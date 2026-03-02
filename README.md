# WC Mission Tools

A .NET 10 solution for reading and writing **Wing Commander 1 & 2** binary MODULE, CAMP, and BRIEFING files. Includes a **console parser** (CLI + JSON export), a **binary packer** (repack with optional LZW compression), and a **WPF visual mission viewer**.

## What It Does

Wing Commander stores all mission data in MODULE files -- binary files containing nav point coordinates, ship loadouts, AI orders, pilot/character assignments, and briefing text packed into fixed-size records. Campaign branching (win/lose paths between star systems, medals, scoring) is stored in separate CAMP files. All dialogue and narrative content (mission briefings, debriefings, bar conversations, funerals, medal ceremonies) is stored in BRIEFING files. This tool parses and visualizes all three formats for WC1 and WC2, auto-detecting the game type from the file header. Both compressed (GOG) and uncompressed files are handled automatically via Origin Systems LZW decompression.

### Parsed Data

**Wing Commander 1:**
- **MODULE.000** -- Vega Campaign: **40 missions** across 13 star systems (Enyo -> Hell's Kitchen)
- **MODULE.001** -- Secret Missions 1: **18 missions**
- **MODULE.002** -- Secret Missions 2: **18 missions**
- **CAMP.000/.001/.002** -- Campaign branching trees (win/lose paths, scoring, medals, bar seating)
- **BRIEFING.000/.001/.002** -- All dialogue (briefings, debriefings, bar conversations, funerals, ceremonies)

**Wing Commander 2:**
- **MODULE.000** -- Vengeance of the Kilrathi: **60 missions** across 15 star systems (Gwynedd -> K'Tithrak Mang)
- **MODULE.001** -- Special Operations 1: **60 missions** (compressed)
- **MODULE.002** -- Special Operations 2: **60 missions**

Per mission:
- **Nav points** -- names, 3D coordinates, ship assignments, triggers, preloads, briefing notes
- **Ships** -- class, allegiance, leader/wingman chains, AI orders, positions, named pilots (WC1) or characters (WC2); expansion-specific hazard class IDs auto-resolved per MODULE file
- **Briefings** -- map objective text (WC1) or flight plan entries with objective icons (WC2)

## Solution Structure

```
WCMissionTools.slnx
+-- WCMissionCore/             Shared class library
|   +-- ModuleParser.cs        WC1 binary parser
|   +-- Wc2ModuleParser.cs     WC2 binary parser (with LZW decompression)
|   +-- CampParser.cs          CAMP campaign file parser
|   +-- BriefingParser.cs      BRIEFING dialogue file parser
|   +-- LzwDecompressor.cs     Origin Systems LZW decompression (9-12 bit)
|   +-- LzwCompressor.cs       Origin Systems LZW compression (9-12 bit)
|   +-- OriginContainerWriter.cs  Origin container format read/write
|   +-- Models.cs              WC1 data classes (NavPoint, MapPoint, Ship, Mission)
|   +-- Wc2Models.cs           WC2 data classes (Wc2NavPoint, Wc2FlightPlan, Wc2Ship, Wc2Mission)
|   +-- CampModels.cs          Campaign data classes (SeriesBranch, MissionScoring, BarSeating)
|   +-- BriefingModels.cs      Briefing data classes (ConversationBlock, DialogLine, DialogSetting)
|   +-- Enums.cs               WC1 enums (ShipClass, Allegiance, ShipOrders, Pilot)
|   +-- Wc2Enums.cs            WC2 enums (Wc2ShipClass, Wc2Character, Wc2Orders)
+-- WCMissionParser/           CLI tool: parse binary → text/JSON
|   +-- Program.cs             CLI entry point, auto-detects WC1/WC2/CAMP/BRIEFING
+-- WCMissionPacker/           CLI tool: repack binary files
|   +-- Program.cs             Repack with optional LZW compression and verification
+-- WCMissionViewer/           WPF desktop application
    +-- MainWindow.xaml        Dark-themed UI layout
    +-- MainWindow.xaml.cs     Mission viewer logic (nav map, ships, details)
    +-- ViewerModels.cs        Unified viewer models (adapts WC1/WC2 to common types)
```

## Build & Run

```bash
# Build the entire solution
dotnet build WCMissionTools.slnx

# Run the WPF viewer
cd WCMissionViewer && dotnet run
# Then click "Open MODULE..." and select any MODULE file.

# Run the console parser
cd WCMissionParser
dotnet run -- {path-to-MODULE} [-s | --sortie N] [-j | --json] [--single-file] [-o | --output <dir>]

# Run the binary packer
cd WCMissionPacker
dotnet run -- {path-to-file} [-c | --compress] [-v | --verify] [-o | --output <path>]

# Full round-trip: parse to JSON, then pack back to identical binary
cd WCMissionParser
dotnet run -- "D:\Games\Wing Commander\GAMEDAT\MODULE.000" --json --single-file --output .\temp
cd ..\WCMissionPacker
dotnet run -- .\temp\MODULE.json --compress --verify --output .\MODULE.000
```

See the [Parser README](WCMissionParser/README.md) for CLI usage details, the [Packer README](WCMissionPacker/README.md) for packing options, and the [Viewer README](WCMissionViewer/README.md) for viewer features.

## Binary Format Documentation

The MODULE and CAMP binary formats are fully documented:

| Document | Description |
|----------|-------------|
| [WC1-MODULE-FORMAT.md](WC1-MODULE-FORMAT.md) | WC1 format (headers, compression, record layouts, ship classes, pilots, star systems) |
| [WC2-MODULE-FORMAT.md](WC2-MODULE-FORMAT.md) | WC2 format (7-section layout, 60-byte ship records, 101-byte nav records, flight plans, named characters) |
| [WC1-CAMP-FORMAT.md](WC1-CAMP-FORMAT.md) | WC1 campaign file format (series branches, scoring, medals, bar seating, stellar backgrounds) |
| [WC2-CAMPAIGN-FORMAT.md](WC2-CAMPAIGN-FORMAT.md) | WC2 campaign scripting system (bytecode VM, dispatch table, SERIES files, comparison with WC1 CAMP) |
| [WC1-BRIEFING-FORMAT.md](WC1-BRIEFING-FORMAT.md) | WC1 briefing file format (dialogue, lip-sync, facial animation, scene composition, per-mission narrative) |

## References & Acknowledgments

This project would not be possible without the Wing Commander reverse-engineering community:

| Author | Tool | Tech | Description |
|--------|------|------|-------------|
| **HCl** | [WC1 Mission Editor](http://www.wcnews.com/files/) | QBasic | Foundational binary format offsets |
| **Michael Gillman** | [WCE Mission Editor](https://github.com/delMar43/wcmodtoolsources/blob/master/wce/Main.BAS) | PureBasic | Nav type meanings, trigger semantics, structural validation |
| **Rehsin** | [WC Mission Editor 1.2](https://wing-commander.weebly.com/) | ModDB | Comprehensive WC1/WC2 editor with decoded ship fields, objective icons, flight plans, character IDs |
| **UnnamedCharacter** | [WCToolbox](https://www.wcnews.com/chatzone/threads/wing-commander-toolbox.27769/) | .NET 4 | Used to verify Origin container format, LZW algorithm, and section type bytes |
| **Wing Commander CIC** | [WC News](https://www.wcnews.com/) | | Community resources, file hosting, and decades of WC preservation |

Built with .NET 10

## Disclaimer

Wing Commander is a trademark of Electronic Arts Inc. This project is an unofficial fan-made tool for reading and writing Wing Commander game data files. It is not affiliated with, endorsed by, or associated with Electronic Arts. If you don't own the games, they are available at [GOG](https://www.gog.com/en/game/wing_commander_1_2) and through EA's store ([WC1](https://www.ea.com/en/games/wing-commander/wing-commander), [WC2](https://www.ea.com/games/wing-commander/wing-commander-2-vengeance-of-the-kilrathi)).