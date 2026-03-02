# WC2 CAMPAIGN File Format (CAMPAIGN.S00 / .S01 / .S02)

## Overview

WC2 replaces WC1's static CAMP branching table with a **bytecode scripting system**. Campaign progression, cutscene dispatch, and inter-mission dialogue are driven by a virtual machine that reads global state variables and executes scene scripts. The actual mission branching is encoded as bytecode rather than a fixed data table.

> **WC2's CAMP.000 is a leftover** — it contains byte-for-byte identical data to WC1's CAMP.000, stored uncompressed (type 0xE0). It is not used by WC2's campaign logic.

**Files:**
| File | Game | Size | Contents |
|------|------|------|----------|
| `CAMPAIGN.S00` | WC2 Vengeance of the Kilrathi | ~20 KB | Campaign dispatch script + cutscene metadata |
| `CAMPAIGN.S01` | Special Operations 1 | ~11 KB | Campaign dispatch script |
| `CAMPAIGN.S02` | Special Operations 2 | ~20 KB | Campaign dispatch script |
| `SERIES.S00` | WC2 base game | ~362 KB | Scene/dialogue scripts (cutscenes, briefings, bar conversations) |
| `SERIES.S01` | Special Operations 1 | ~157 KB | Scene/dialogue scripts |
| `SERIES.S02` | Special Operations 2 | ~147 KB | Scene/dialogue scripts |

## Container Structure

CAMPAIGN files use Origin's standard container format. When extracted with WCToolbox (`WC2ToolsCmd.exe xmlunpack`), each CAMPAIGN.S00 contains:

| Group | Contents | Notes |
|-------|----------|-------|
| **GlobalGroup** | 768 bytes of global state | Variables read/written by bytecode (series, mission, score, flags) |
| **ShapeGroup** | Character sprites | people.v00–v17, helmet, closeup, medium images |
| **FontGroup** | Dialogue fonts | Used by cutscene text rendering |
| **PaletteGroup** | Color palettes | Scene palettes for different locations |
| **FilmGroup** | Film references | Points to `series.s00`, `incident.s00`, `gameflow.s00` |
| **SpriteGroup** | Sprite bytecode | Symbols: `__bluehair`, `__downtown`, `sabre`, `crowd`, etc. |
| **PlaneGroup** | Backdrop definitions | `backdrop`, `wingmanplane`, `body` |
| **SequenceGroup** | Sequence functions | `removeall`, `showslot`, `closeup`, `talking`, `settalker`, `printit`, `narrator`, `animate`, etc. |
| **SceneGroup** | Main campaign script | Single "MajorScript" — the campaign dispatch bytecode |

## Bytecode Virtual Machine

The SceneGroup contains the **MajorScript** — the campaign's core dispatch logic. It runs on Origin's bytecode VM with instructions like:

| Instruction | Description |
|-------------|-------------|
| `load.const1` / `load.const2` | Push constant onto stack |
| `load.global N` | Push value of global variable N |
| `store.global N` | Pop value into global variable N |
| `compare.eq` | Compare top two stack values for equality |
| `branch.false ADDR` | Jump if comparison was false |
| `branch ADDR` | Unconditional jump |
| `run.scene N` | Execute scene script N (from SERIES file) |
| `run.sequence` | Execute a named sequence function |
| `yield` | Pause execution (return control to game engine) |
| `set.style.color` | Set text rendering color |
| `set.palette` | Set display palette |
| `select.font` | Set active font |
| `play.music` | Play music track |
| `discard.font` | Release font resources |

### Key Global Variables

Based on disassembly of CAMPAIGN.S00's MajorScript:

| Global | Purpose | Notes |
|--------|---------|-------|
| 2 | **Current series** | Checked in the main dispatch switch (values 1–12+) |
| 3 | **Current mission** | Checked within each series block (values 0–3) |
| 5 | Unknown | Set per (series, mission) pair — possibly next series or act index |
| 6 | Unknown | Set per (series, mission) pair — possibly ship class or wingman |
| 7 | Unknown | Computed from global 402 — possibly scene/film index |
| 298 | Scene selector | Set to 0 before `run.scene 2` |
| 402 | Computed scene ID | Calculated as `N * 256 + offset` per mission |
| 471–472 | Flags | Initialized to 0 at startup |
| 753–756, 760–761 | State flags | Initialized at startup (-1 or 0) |

## How Campaign Dispatch Works

The MajorScript is essentially a **giant switch statement**:

```
loop:
    yield                          // wait for game engine to set globals (series/mission result)
    
    if global[2] == 1:             // series 1
        if global[3] == 0:         // mission 0
            global[402] = 0 + 256  // scene ID
            global[5] = 1          // (next series?)
            global[6] = 2          // (ship/wingman?)
            global[7] = global[402]
        elif global[3] == 1:       // mission 1
            ...
        elif global[3] == 2:       // mission 2
            ...
        elif global[3] == 3:       // mission 3
            ...
    
    elif global[2] == 2:           // series 2
        ...
    
    elif global[2] == 3:           // series 3
        ...
    
    ... (12 series total for base game)
    
    global[298] = 0
    run.scene 2                    // hand off to SERIES.S00 to play the cutscene
    discard.font
    goto loop
```

**Key observations:**
- The `yield` at the top gives control back to the **game engine** (C code), which runs the actual flight missions and updates globals 2 and 3 based on results
- The bytecode only dispatches **which cutscene/dialogue to play** after each mission
- Each `(series, mission)` pair maps to a specific scene in SERIES.S00 via global[402]
- The actual **win/lose branching** (which series comes next) is likely handled by the game engine itself or by the scene scripts in SERIES.S00
- CAMPAIGN.S00 has **12 series checks** with **~4 missions each** (47 mission checks total)

## SERIES Files

The SERIES.S00/S01/S02 files are much larger (150–362 KB) and contain the actual cutscene/dialogue content:
- Character dialogue (bar conversations, briefings, debriefings)
- Cutscene choreography (character positioning, animations, camera)
- Narrative text (narrator blocks, mission objectives)
- Scene sequencing (setup backdrops, show characters, play animations)

These are referenced by CAMPAIGN's `FilmGroup` and executed via `run.scene`.

## Comparison: WC1 CAMP vs WC2 CAMPAIGN

| Aspect | WC1 CAMP | WC2 CAMPAIGN |
|--------|----------|--------------|
| **Format** | Static data table (binary records) | Bytecode script (virtual machine) |
| **Branching** | Explicit: `SuccessSeries`/`FailureSeries` fields per series | Implicit: game engine updates globals, bytecode dispatches scenes |
| **Scoring** | `SuccessScore` threshold + `FlightPathScoring` weights | Not in CAMPAIGN — likely in game engine or SERIES scripts |
| **Medals** | Per-mission medal thresholds in CAMP | Not in CAMPAIGN — likely elsewhere |
| **Size** | ~450–650 bytes compressed | ~11–20 KB bytecode |
| **Parseable to JSON?** | Yes — direct binary-to-data mapping | Partially — the dispatch table can be extracted, but full logic requires bytecode interpretation |
| **Cutscene control** | None (CAMP is pure data) | Primary purpose — maps missions to cutscene scenes |

## Extracting Data for a Remake

For a WC2 Unity remake, the CAMPAIGN bytecode's dispatch table **can** be reduced to a JSON-like mapping:

```json
{
  "series_1": {
    "mission_0": { "scene_id": 256, "global_5": 1, "global_6": 2 },
    "mission_1": { "scene_id": 256, "global_5": 1, "global_6": 2 },
    "mission_2": { "scene_id": 512, "global_5": 2, "global_6": 3 },
    "mission_3": { "scene_id": 512, "global_5": 1, "global_6": 4 }
  },
  "series_2": { ... }
}
```

However, to fully reconstruct WC2's campaign tree (which series follows which), additional work is needed:
1. **Decode globals 5, 6, 7** — determine what these control (next series, ship assignment, wingman?)
2. **Analyze SERIES.S00** — the scene scripts may contain branching logic or narrative context
3. **Cross-reference the WC2 executable** — the game engine sets globals 2/3 after each mission; the branching rules may be hardcoded

> **Future parser note:** A CAMPAIGN parser would disassemble the bytecode and extract the dispatch table. The WCToolbox disassembler (`WC2ToolsCmd.exe disassemble`) produces readable .asm output that can be parsed programmatically.

## Tools

- **WCToolbox** `WC2ToolsCmd.exe xmlunpack` — extracts CAMPAIGN to XML + binary chunks
- **WCToolbox** `WC2ToolsCmd.exe disassemble` — disassembles bytecode to .asm text
- Disassembly output location: same directory as input, with `.asm` extension appended to chunk filename
