# WC1 CAMP File Format (CAMP.000 / .001 / .002)

## Overview

CAMP files define the campaign branching structure for Wing Commander 1 (and Secret Missions 1 & 2). They control which system the player visits next based on mission performance, what medals are awarded, which characters sit in the bar, and what stellar backgrounds are displayed.

**Files:**
| File | Game | Size (raw) | Sections |
|------|------|------------|----------|
| `CAMP.000` | WC1 Vega Campaign | 651 bytes | 3 (compressed) |
| `CAMP.001` | Secret Missions 1 | 456 bytes | 3 (compressed) |
| `CAMP.002` | Secret Missions 2 | 437 bytes | 3 (compressed) |

## Container Format

CAMP files use the standard Origin Systems container format, identical to MODULE files:

```
Offset  Size  Description
------  ----  -----------
0x00    4     File size (uint32 LE)
0x04    N×4   Section table (N entries, each 4 bytes)
              - 3 bytes: section offset (little-endian)
              - 1 byte:  type flag (0x01 = compressed, 0xE0 = uncompressed)
```

The first section's offset determines the number of entries: `N = (firstOffset - 4) / 4`.

### Compression

WC1 CAMP files use **type 0x01** (LZW compressed). Each section starts with a 4-byte decompressed size (uint32 LE) followed by LZW-compressed data. This is the same Origin LZW format used in WC1 MODULE files.

WC2 CAMP.000 stores the same data using **type 0xE0** (uncompressed) — section data is raw bytes with no decompressed-size header.

## Sections

| Section | Name | Description |
|---------|------|-------------|
| 0 | Stellar Background | Background star imagery per sortie (cosmetic) |
| 1 | Series Branch | **Campaign branching tree** (critical game logic) |
| 2 | Bar Seating | Characters at bar tables per sortie (cosmetic) |

---

## Section 0: Stellar Background

Defines which stellar background images and rotations to use for each sortie.

**Record count:** `sectionSize / 8` (typically 52 = 13 series × 4 sorties)

Each record is 8 bytes:

```
Offset  Size  Type    Field
------  ----  ----    -----
0       2     int16   Image index (-1 = none)
2       2     int16   Rotation X (degrees)
4       2     int16   Rotation Y (degrees)
6       2     int16   Rotation Z (degrees)
```

**Layout:** Records are grouped by series, 4 per series (one per sortie slot). Records at index `series * 4 + sortie` define that sortie's stellar background.

---

## Section 1: Series Branch (Campaign Tree)

This is the critical section — it defines the campaign's branching mission tree. Each "series" is a set of missions at a star system. Performance determines whether the player follows the winning or losing path.

**Record count:** `sectionSize / 90` (13 for base game, varies for expansions)

Each record is **90 bytes**: a 10-byte header followed by 4 mission scoring entries (20 bytes each).

### Series Header (10 bytes)

```
Offset  Size  Type    Field
------  ----  ----    -----
0       1     byte    Wingman (character index for this series)
1       1     byte    Reserved (always 0)
2       1     byte    MissionsActive (number of playable missions, 2-4)
3       2     uint16  SuccessScore (total score threshold to take winning path)
5       1     int8    Cutscene (-1 = none, 0-3 = midgame cutscene, 64/65 = final win/lose)
6       1     byte    SuccessSeries (next series index on win, -1 = campaign end)
7       1     byte    SuccessShip (ship class assigned on winning path)
8       1     byte    FailureSeries (next series index on loss, -1 = campaign end)
9       1     byte    FailureShip (ship class assigned on losing path)
```

**Ship class values** (for SuccessShip / FailureShip):
| Value | Ship |
|-------|------|
| 0 | Hornet |
| 1 | Rapier |
| 2 | Scimitar |
| 3 | Raptor |

### Mission Scoring Entry (20 bytes × 4)

Each series has 4 mission scoring slots (even if fewer missions are active — unused slots are zeroed).

```
Offset  Size  Type    Field
------  ----  ----    -----
0       1     byte    Medal (0=none, 1=Bronze Star, 2=Silver Star, 4=Gold Star)
1       1     byte    Reserved
2       2     uint16  MedalScore (kill score threshold for medal)
4       16    byte[]  FlightPathScoring (points per ship class killed)
```

**FlightPathScoring array** — 16 bytes, indexed by ship class:
| Index | Ship Class | Notes |
|-------|-----------|-------|
| 0 | Hornet | Friendly fire penalty context |
| 1 | Rapier | |
| 2 | Scimitar | |
| 3 | Raptor | |
| 4 | Venture | |
| 5 | Diligent | |
| 6 | Drayman | |
| 7 | Exeter | |
| 8 | Tiger's Claw | |
| 9 | Salthi | |
| 10 | Dralthi | |
| 11 | Krant | |
| 12 | Gratha | |
| 13 | Jalthi | |
| 14 | Hhriss | |
| 15 | Dorkir | |

> Note: Values represent mission-completion scoring weights per ship class, used to calculate the player's total series score. If the total score across all missions in the series meets or exceeds `SuccessScore`, the player follows the winning path.

---

## Section 2: Bar Seating

Defines which characters are sitting at the bar's left and right seats for each sortie.

**Record count:** `sectionSize / 2` (typically 52 = 13 series × 4 sorties)

Each record is 2 bytes:

```
Offset  Size  Type    Field
------  ----  ----    -----
0       1     int8    LeftSeat (character index, -1 = empty)
1       1     int8    RightSeat (character index, -1 = empty)
```

**Character indices** (for Wingman, LeftSeat, RightSeat):
| Index | Character |
|-------|-----------|
| 0 | Paladin |
| 1 | Angel |
| 2 | Bossman |
| 3 | Knight |
| 4 | Maniac |
| 5 | Spirit |
| 6 | Hunter |
| 7 | Iceman |

---

## WC1 Vega Campaign Mission Tree

The base game has 13 series (indices 0-12) forming a branching tree across 4 acts:

```
                          [0] Enyo (start)
                         /              \
                   Win→ [1]           [2] ←Lose
                  McAuliffe       Gimle/Gateway
                 /        \         /        \
           Win→ [3]      [4]    [4]        [5]
           Brimstone  Chengdu  Chengdu  Dakota/Port Hedland
              /    \    /   \    ...       ...
         Win→[6]  [7] [7]  [8]
          Kurasawa  Venice   Rostov
            ...    ...      ...
              \   /   \   /
         Win→ [9]    [10]
          Hubble's   Hell's Kitchen
            /    \    /    \
      Win→[11]  [12] ←Lose (final)
      (Victory)  (Defeat)
```

**Series 11:** Cutscene=64, winning ending (MissionsActive=4 — final 4-mission gauntlet)
**Series 12:** Cutscene=65, losing ending (MissionsActive=4 — final losing missions)

Both terminal series have SuccessSeries=-1 and FailureSeries=-1 (no further branching).

---

## WC2 Note

WC2's `GAMEDAT\CAMP.000` stores the same 3-section format but with **type 0xE0** (uncompressed). The actual WC2 campaign scripting uses separate files:
- `CAMPAIGN.S00/S01/S02` — Campaign scripts with embedded bytecode
- `SERIES.S00/S01/S02` — Scene/dialogue scripts (150-360KB)

The WC2 CAMP.000 contains what appears to be WC1-format placeholder/template data.

---
