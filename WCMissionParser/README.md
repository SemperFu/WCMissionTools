# WC Mission Parser

A .NET 10 class library and CLI tool for parsing Wing Commander 1 & 2 binary MODULE, CAMP, and BRIEFING files. Auto-detects the game type and file format from the filename and header, extracting nav points, ship definitions, briefings, wing assignments, system names, campaign branching data, and all dialogue/narrative content.

## What It Parses

**Wing Commander 1:**
- **MODULE.000** -- Vega Campaign: **40 missions** across 13 star systems (Enyo -> Hell's Kitchen)
- **MODULE.001** -- Secret Missions 1: **18 missions**
- **MODULE.002** -- Secret Missions 2: **18 missions**
- **CAMP.000** -- Vega Campaign: **13-series branching tree** with scoring, medals, bar seating
- **CAMP.001** -- Secret Missions 1: **8-series campaign** (+ 5 empty slots)
- **CAMP.002** -- Secret Missions 2: campaign data
- **BRIEFING.000** -- Vega Campaign: **209 conversations** across 56 blocks (briefings, debriefings, bar, funeral, ceremony)
- **BRIEFING.001** -- Secret Missions 1: dialogue and briefings
- **BRIEFING.002** -- Secret Missions 2: dialogue and briefings

**Wing Commander 2:**
- **MODULE.000** -- Vengeance of the Kilrathi: **60 missions** across 15 star systems
- **MODULE.001** -- Special Operations 1: **60 missions** (compressed)
- **MODULE.002** -- Special Operations 2: **60 missions**

Per mission:
- **Nav points** -- names, 3D coordinates, sphere radius, ship assignments, triggers, preloads, briefing notes
- **Ships** -- class, allegiance, leader/wingman chains, AI orders, positions, named pilots (WC1) or characters (WC2)
- **Briefings** -- map objective text (WC1) or flight plan entries with objective icons (WC2)
- **Encounter titles** (WC1) -- difficulty-based encounter flavor text from Section 1 ("12:00 at O.K. Corral", "Solo Flight")

Per campaign (CAMP files):
- **Series branches** -- win/lose branching tree, ship assignments, wingman, score thresholds
- **Mission scoring** -- per-ship-class kill scoring weights, medal thresholds
- **Bar seating** -- which characters appear at the rec room bar per sortie
- **Stellar backgrounds** -- background star images and rotations per sortie

Per briefing (BRIEFING files):
- **Conversations** -- all dialogue text with variable tokens ($C=callsign, $R=rank, $N=name, $S=system)
- **Scene settings** -- background/foreground image indices, text color, timing delays per dialog line
- **Lip sync** -- phonetic text for mouth animation
- **Facial expressions** -- animation frame codes for character faces
- **Conditional commands** -- branching logic for context-sensitive dialogue (wingman death, performance)

Handles both compressed and uncompressed MODULE files automatically via Origin Systems LZW decompression. WC1 compressed files use type byte 0x01; WC2 compressed files use type byte 0x20.

## CLI Usage

```bash
cd WCMissionParser
dotnet run -- <file> [-x | --xml] [--single-file] [-s | --sortie N] [-o | --output <dir>] [-l | --list]
```

> **Note:** The `--` after `dotnet run` is required — it tells the dotnet CLI that everything after it is passed to the application. The file path is the first positional argument (no flag needed). Run with `-h` for built-in help. The tool auto-detects MODULE vs CAMP vs BRIEFING files by filename.

### Options

| Argument | Short | Description |
|----------|-------|-------------|
| `<file>` | | Path to a MODULE or CAMP file (required, first argument) |
| `--json` | `-j` | Export missions as JSON files (default — only needed alongside `--xml` to get both) |
| `--xml` | `-x` | Export missions as XML files |
| `--single-file` | | Combine all missions into one file instead of per-mission files |
| `--output <dir>` | `-o` | Output directory for exported files (defaults to same folder as input file) |
| `--sortie N` | `-s` | Filter to a single sortie index |
| `--list` | `-l` | List available sorties with system and mission info, then exit |

JSON is always exported by default. Use `-xml` to also export XML. Use both `-json -xml` to be explicit about both formats.

### Examples

```bash
# Export all missions as per-mission JSON files (default)
dotnet run -- "C:\Games\Wing Commander\GAMEDAT\MODULE.000"

# Export a single sortie
dotnet run -- MODULE.000 --sortie 1

# Export as XML only
dotnet run -- MODULE.000 --xml

# Export both JSON and XML
dotnet run -- MODULE.000 --json --xml

# Export all missions into a single JSON file
dotnet run -- MODULE.000 --single-file

# Export to a specific output directory
dotnet run -- MODULE.000 --output "C:\MyExports"

# Show built-in help
dotnet run -- -h

# List all sorties in a MODULE file
dotnet run -- MODULE.000 --list

# Parse a CAMP file (campaign branching tree)
dotnet run -- CAMP.000

# List campaign series overview
dotnet run -- CAMP.000 --list

# Parse a BRIEFING file (dialogue, briefings, bar conversations)
dotnet run -- BRIEFING.000

# List all briefing blocks
dotnet run -- BRIEFING.000 --list
```

### Sample `-list` Output (BRIEFING)

```
Briefing Blocks (56):

  [ 0] Funeral      7 conversations, 62 lines
  [ 1] Office       1 conversations, 35 lines
  [ 2] Ceremony     1 conversations, 16 lines
  [ 3] Unused       empty
  [ 4] S01M0        5 conversations, 80 lines
  [ 5] S01M1        5 conversations, 70 lines
  [ 6] S01M2        empty
  ...
  [55] S13M3        5 conversations, 69 lines
```

### Sample `-list` Output (MODULE)

```
40 WC1 missions in MODULE.000:

  Sortie  0  System 0, Mission 0 [Enyo] -- Alpha Wing  (4 navs, 12 ships)
  Sortie  1  System 0, Mission 1 [Enyo] -- Epsilon Wing  (4 navs, 13 ships)
  ...
```

### Sample `-list` Output (CAMP)

```
Campaign Tree (13 series):

  Series  0: 2 missions, wingman=Paladin, score≥10
           Win → Series 2 (Scimitar)  |  Lose → Series 3 (Hornet)
  Series  1: 3 missions, wingman=Spirit, score≥32 cutscene=0
           Win → Series 4 (Raptor)  |  Lose → Series 5 (Scimitar)
  ...
  Series 11: 4 missions, wingman=Angel, score≥-1 cutscene=64
           Win → END  |  Lose → END
  Series 12: 4 missions, wingman=Angel, score≥-1 cutscene=65
           Win → END  |  Lose → END
```

### Output Structure

**Per-mission files (default):**

Each mission is written to its own file in a subfolder named after the MODULE file. Filenames include the sortie index and star system name for easy identification.

```
MODULE.000_export/
  sortie_00_enyo.json
  sortie_01_enyo.json
  sortie_02_mcauliffe.json
  ...
  sortie_39_hellskitchen.json
```

**Single file (`--single-file`):**

All missions are combined into a single file — an array for JSON, a root element for XML.
This mode also includes `RawSections` (base64 section data) for round-trip repacking with [WCMissionPacker](../WCMissionPacker/README.md).

```
MODULE.000.json       (or MODULE.000.xml)
```

### Export Schema

Exports use **game-specific field names** — WC1 and WC2 missions have different schemas reflecting each game's data structures. Enums are serialized as strings (e.g., `"Kilrathi"` not `1`). All fields are always present (zero/default values are included, never omitted).

**WC1 mission fields:** SortieIndex, SystemIndex, MissionIndex, WingName, SystemName, EncounterTitles (4 strings indexed by difficulty: Beginner/Easy/Hard/Ace), NavPoints (Name, NavType, XYZ, Radius, BriefingNote, Triggers, Preloads, ShipIndices), Ships (Class, Allegiance, Leader, Orders, Pilot, XYZ, Rotation, Speed, AiLevel, PrimaryTarget, SecondaryTarget, Formation), MapPoints (IconFormat, TargetIndex, Description)

**WC2 mission fields:** SortieIndex, SystemIndex, MissionIndex, MissionLabel, SystemName, NavPoints (same as WC1), Ships (Class, Allegiance, Leader, Orders, Character, XYZ, Rotation, Speed, AiLevel, PrimaryTarget, SecondaryTarget, FormationSlot), FlightPlans (ObjectiveIcon, TargetNav, Description)

## Sample Output (Sortie 1 -- Enyo Mission 2)

```
=== Sortie 1 (System 0, Mission 1) ===
  Nav Points: 4
    [0] "Tiger's Claw" XYZ=(0,0,0) ships=[0,3,4,5,6]
    [1] "Nav 1" XYZ=(-25000,3000,-25000) ships=[7,8]
    [2] "Nav 2" XYZ=(-10000,5000,-65000) ships=[9,10,11]
    [3] ".Asteroid Field" XYZ=(0,0,-35000) ships=[12]
  Map Points: 4
    [0] "Proceed to Nav 1"
    [1] "Proceed to Nav 2"
    [2] "Defend transport as it prepares for jump"
    [3] "Return to Tiger's Claw at best speed"
  Ships: 13
    [00] TigersClaw     Confed   no-leader    orders=Inactive    XYZ=(0,0,0) spd=0
    [01] Hornet         Confed   no-leader    orders=Patrol      XYZ=(0,0,-1500) spd=150 pilot=Blair
    [02] Hornet         Confed   leader=1     orders=Follow      XYZ=(0,0,0) spd=150 pilot=Spirit
    [06] Drayman        Confed   no-leader    orders=JumpOut     XYZ=(0,0,-3000) spd=150
    [07] Salthi         Kilrathi no-leader    orders=AttackTarget XYZ=(0,0,-5000) spd=150
    [09] Dralthi        Kilrathi no-leader    orders=AttackTarget XYZ=(3000,0,5000) spd=200
    [12] AsteroidField  Neutral  no-leader    orders=Inactive    XYZ=(-5000,1500,0) spd=760
```

## Known Limitations

- **Map point icon codes** -- Some briefing text entries have a leading `.` character (encounter points); shown as-is from the binary data.
- **Encounter title mapping** -- Section 1 encounter titles are read using sortie index modulo 16 within each difficulty pool. The exact mapping from sortie to difficulty pool entry is inferred from the documented 4×16 structure; verified field-by-field reverse-engineering of the pool index per sortie has not been done.
- **WC2 mission headers** -- Fields like InitialSphere, Carrier, TakeoffShip are known from editor output but their binary offsets are not yet mapped.
- **WC2 orders field** -- All tested WC2 missions show orders=0 (Patrol); non-zero order values have not been observed yet.
- **SO1/SO2 hazard class IDs** -- Each expansion uses different ship class IDs for asteroids and mines (base=33/34, SO1=41/42, SO2=51/52). Class 41 is asteroids in SO1 but the MorningStar fighter in SO2. The parser and viewer handle this automatically based on the MODULE file number.

## Further Reading

- [WC1-MODULE-FORMAT.md](../WC1-MODULE-FORMAT.md) -- WC1 binary format documentation
- [WC2-MODULE-FORMAT.md](../WC2-MODULE-FORMAT.md) -- WC2 binary format documentation
- [WC1-CAMP-FORMAT.md](../WC1-CAMP-FORMAT.md) -- WC1 campaign file format documentation
- [WC1-BRIEFING-FORMAT.md](../WC1-BRIEFING-FORMAT.md) -- WC1 briefing file format documentation
- [WCMissionViewer README](../WCMissionViewer/README.md) -- WPF visual mission viewer