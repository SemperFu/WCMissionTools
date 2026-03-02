# WC2 MODULE File Format -- Technical Reference

Binary format documentation for Wing Commander 2 MODULE files, built from community editors and binary analysis.

See [WC1-MODULE-FORMAT.md](WC1-MODULE-FORMAT.md) for the WC1 format.

## WC1 vs WC2 Comparison

| Property | WC1 | WC2 |
|----------|-----|-----|
| Header size | 28 bytes (6 sections) | 32 bytes (7 sections) |
| Type byte | 0x01=compressed, 0x02=uncompressed | 0xE0=uncompressed, 0x20=compressed(?) |
| Navs per mission | 16 | 10 |
| Ships per mission | 32 | 16 |
| Flight plans | Embedded in map descriptions | 8 per mission (icon + target + text) |
| Carrier | Tiger's Claw | Concordia / Caernavon Station |
| Mission labels | Wing names (Alpha Wing, etc.) | Mission A/B/C/D |
| Extra mission fields | -- | TakeoffShip, LandingShip |

## File Header (32 bytes)

| Bytes | Field | Notes |
|-------|-------|-------|
| 0-3 | File size (uint32 LE) | Matches actual file size for uncompressed files |
| 4-31 | 7 section entries x 4 bytes | 3-byte LE offset + 1-byte type (0xE0=uncompressed) |

## Compression

WC2 MODULE files may use compression (type byte 0x20), though all analyzed files so far are uncompressed (type 0xE0). The compression algorithm is likely the same **Origin Systems LZW** used in WC1 (variable-width 9-12 bit codes, code 256=clear dictionary, code 257=end of stream). Detection: check byte 7 (first section's type byte). Type 0xE0 = uncompressed, type 0x20 = compressed.

## Data Sections

| Section | Offset | Size | Content |
|---------|--------|------|---------|
| 0 | 32 | 1,536 | Campaign routing (same structure as WC1) |
| 1 | 1,568 | 64,640 | Nav points -- 64 missions x 10 navs x 101 bytes |
| 2 | 66,208 | 32,768 | Flight plans -- 64 missions x 8 entries x 64 bytes |
| 3 | 98,976 | 61,440 | Ship definitions -- 64 missions x 16 ships x 60 bytes |
| 4 | 160,416 | 2,560 | Mission labels at 40-byte intervals ("Mission A", "Mission B", etc.) |
| 5 | 162,976 | 640 | System names at 40-byte intervals |
| 6 | 163,616 | 14,208 | Executable code/data (error strings, embedded ship type names) |

**Note:** Offsets are from uncompressed MODULE.000 (177,824 bytes).

## Mission Header Fields

Per-mission metadata (not yet located in binary -- derived from editor output):

| Field | Type | Notes |
|-------|------|-------|
| InitialSphere | int | Starting nav point index |
| Carrier | int | Ship index of the carrier (-1 = none) |
| YourShip | int | Ship index of the player's ship (-1 = not assigned) |
| Convoy | int[8] | Up to 8 ship indices forming convoy group (-1 = empty) |
| WingName | string | Mission label from section 4 (e.g., "Mission A") |
| TakeoffShip | int | Ship index for takeoff cinematic (WC2-specific) |
| LandingShip | int | Ship index for landing cinematic (WC2-specific) |

## Nav Record (101 bytes)

Confirmed record size: 101 bytes. 10 navs per mission.

| Offset | Size | Field |
|--------|------|-------|
| 0 | 30 | Name (null-terminated ASCII) |
| 30 | 1 | Nav type (0=manipulated, 1=dominant, 2+=follow-up) |
| 31 | 1 | (padding) |
| 32-34 | 3 | X coordinate (3-byte signed LE, same as WC1) |
| 35 | 1 | (padding) |
| 36-38 | 3 | Y coordinate |
| 39 | 1 | (padding) |
| 40-42 | 3 | Z coordinate |
| 43-44 | 2 | Radius (int16 LE, encounter sphere trigger distance) |
| 45-46 | 2 | Unknown (2 bytes) |
| 47-54 | 8 | Triggers -- 4 pairs of (type, target nav). Signed bytes, -1=none |
| 55-62 | 8 | Unknown (8 bytes) |
| 63-68 | 6 | 3 preload ship class indices (int16 LE each, -1=none) |
| 69-74 | 6 | 3 unknown values (int16 LE each) |
| 75-80 | 6 | 3 unknown values (int16 LE each) |
| 81-100 | 20 | 10 ship indices (int16 LE each, -1=empty) |

## Ship Record (60 bytes)

Confirmed record size: 60 bytes. 16 ships per mission.

| Offset | Size | Field |
|--------|------|-------|
| 0-19 | 20 | Name (null-terminated ASCII, embedded in record) |
| 20 | 1 | Ship class (see ship class table below) |
| 21 | 1 | Unknown |
| 22 | 1 | (padding) |
| 23 | 1 | Allegiance (0=Confed, 1=Kilrathi, 2=Neutral) |
| 24 | 1 | Orders (see orders table below) |
| 25 | 1 | Unknown |
| 26 | 1 | (padding) |
| 27-28 | 2 | Leader ship index (signed int16 LE, -1=none) |
| 29 | 1 | Formation slot |
| 30 | 1 | (padding) |
| 31-33 | 3 | X coordinate (3-byte signed LE) |
| 34 | 1 | (padding) |
| 35-37 | 3 | Y coordinate |
| 38 | 1 | (padding) |
| 39-41 | 3 | Z coordinate |
| 42-43 | 2 | Rotation X (signed int16 LE) |
| 44-45 | 2 | Rotation Y |
| 46-47 | 2 | Rotation Z |
| 48 | 1 | Unknown |
| 49 | 1 | Speed |
| 50 | 1 | (padding) |
| 51 | 1 | AI level (0-4+) |
| 52 | 1 | (padding) |
| 53 | 1 | Pilot ID (generic pilot index) |
| 54 | 1 | (padding) |
| 55 | 1 | Unknown |
| 56 | 1 | Primary target ship index (signed, -1=none) |
| 57 | 1 | Secondary target ship index (signed, -1=none) |
| 58 | 1 | Unknown |
| 59 | 1 | Character ID (named character for dialogue, see table) |

## Flight Plan Record (64 bytes)

8 flight plans per mission (512 bytes / 64 = 8). Same record layout as WC1 map records.

| Offset | Size | Field |
|--------|------|-------|
| 0 | 1 | Objective icon (signed, -1=unused entry) |
| 1 | 1 | (padding) |
| 2 | 1 | Target nav index (signed, -1=unused) |
| 3 | 1 | (padding) |
| 4-63 | 60 | Description text (null-terminated ASCII) |

**Objective icons:**

| ID | Icon | Color | Usage |
|----|------|-------|-------|
| 0 | Square | Green | Mission sphere / waypoint |
| 1 | Triangle | White | Home base |
| 2 | Cross | Purple | Friendly ship |
| 3 | Circle | Green | Friendly ship |
| 4 | Circle | Red | Enemy ship |
| 5 | Square(?) | ??? | Hidden/secondary objective |

**Example (Gwynedd Mission 1):**

| Icon | Target | Description |
|------|--------|-------------|
| 0 | 1 | Proceed to Nav 1 |
| 5 | 1 | .Proceed to Nav 1 (hidden/secondary) |
| 0 | 2 | Proceed to Nav 2 |
| 0 | 3 | Proceed to Nav 3 |
| 5 | 3 | .Proceed to Nav 3 (hidden/secondary) |
| 1 | 0 | Return to Caerarvon Station |

## Ship Classes

| ID | Confed | ID | Kilrathi | ID | Other |
|----|--------|----|----------|----|-------|
| 0 | Ferret | 5 | Sartha | 33 | Asteroids |
| 1 | Rapier | 6 | Drakhri | 34 | Mines |
| 2 | Broadsword | 7 | Jalkehi | 23 | Human Starbase |
| 3 | Epee | 8 | Grikath | 24 | Human Supply Depot |
| 4 | Sabre | 9 | Strakha | 25 | Kilrathi Supply Depot |
| 11 | Clydesdale | 10 | Bloodfang | 26 | K'Tithrak Mang |
| 12 | Free Trader | 13 | Dorkathi | 48 | Ayer's Rock |
| 15 | Crossbow | 16 | Kamekh | | |
| 17 | Waterloo | 20 | Ralatha | | |
| 18 | Concordia | 21 | Fralthra | | |
| 19 | Gilgamesh | | | | |

**Expansion ships:**

| ID | Ship | Expansion |
|----|------|-----------|
| 36 | Gothri | Special Operations 1 |
| 41 | Asteroids* | Special Operations 1 |
| 42 | Mines | Special Operations 1 |
| 48 | Ayer's Rock | Special Operations 2 |
| 50 | Gothri | Special Operations 2 |
| 51 | Asteroids | Special Operations 2 |
| 52 | Mines | Special Operations 2 |

*Class 41 is asteroids in SO1 but the MorningStar fighter in SO2.

IDs 14, 22, 27-32, 35, 37-40, 43-47, 49, 53-55 are unknown/unused.

## Named Characters

| ID | Confed | ID | Kilrathi | ID | Other |
|----|--------|----|----------|----|-------|
| 1 | Angel | 12 | Prince Thrakhath | 0 | None |
| 3 | Hobbes | 22 | Khasra Redclaw | 18 | Male Freighter Pilot |
| 4 | Stingray | 23 | Rakti Blood-Drinker | 19 | Female Freighter Pilot |
| 6 | Jazz | 24 | Kur Human-Killer | 27 | Kilrathi Comm Officer |
| 8 | Paladin | 25 | Drakhai Pilot | 28 | Confed Thrakhath (SO1) |
| 9 | Doomsday | 26 | Regular Kilrathi Pilot | 29 | Pirate |
| 10 | Bear (SO1) | | | 36 | Maniac (SO2) |
| 11 | Shadow | | | 37 | Mandarin Pilot (SO2) |
| 14 | Spirit | | | | |
| 15 | Major Edmond | | | | |
| 16 | Male Comm Officer | | | | |
| 17 | Female Comm Officer | | | | |
| 20 | Male Terran Pilot | | | | |
| 21 | Female Terran Pilot | | | | |

## Star Systems

| ID | Name | Notes | ID | Name | Notes |
|----|------|-------|----|------|-------|
| 0 | (null series) | Header | 8 | K'Tithrak Mang | |
| 1 | Gwynedd | Act 1 | 9 | Ghorah Khar | Act 3 (repeat) |
| 2 | Niven | | 10 | Novaya Kiev | Act 3 (repeat) |
| 3 | Ghorah Khar | Act 1 | 11 | Tesla | Act 3 (repeat) |
| 4 | Novaya Kiev | Act 1 | 12 | Gwynedd | Act 3 (repeat) |
| 5 | Heaven's Gate | | 13 | Series 13 | Unused |
| 6 | Tesla | Act 2 | 14 | Series 14 | Unused |
| 7 | Enigma | Act 2 | 15 | Series 15 | Unused |

Systems 9-12 repeat earlier names for later campaign acts that return to the same star system.

## 3-Byte Coordinate Encoding

Same as WC1: read 3 bytes little-endian as a 24-bit unsigned integer, then subtract 16,777,216 if the value >= 8,388,608 (sign bit in bit 23). Used for both nav point positions and ship positions, with a padding byte after each 3-byte coordinate. **Confirmed** by matching parser output to editor reference data.

## Orders

| ID | Order | Notes |
|----|-------|-------|
| 0 | Patrol | Same as WC1 |
| 1 | Escort | |
| 2 | Attack | |
| 3 | Defend | |
| 4 | Wingman | Follow formation leader |
| 5 | Flee | |
| 6 | Goto warp | Jump out of system |
| 7 | Warp arrive | Jump into system |
| 8 | Unknown | |
| 9 | Rendezvous | |
| 10 | Come home | Return to carrier |
| 11-14 | Unknown | WC2-specific, purpose TBD |

## Known Limitations (Format)

- **Compression untested** -- Type 0x20 files have not been encountered or tested.
- **Mission header location unknown** -- Fields like InitialSphere, Carrier, TakeoffShip are known from editor output but their binary offsets within the sections are not yet mapped.
- **Some ship record bytes unknown** -- Bytes 19, 21, 25, 48, 55, 58 have been observed to contain non-zero values but their purpose is not confirmed.
- **Orders field** -- All tested missions show 0 (Patrol); non-zero order values have not been observed yet.
- **SO1/SO2 ship class reuse** -- The expansion packs reuse ship class IDs for different purposes. Class 41 (MorningStar in SO2) functions as asteroids in SO1 missions (confirmed by in-game nav scan). SO2 introduces new hazard IDs: class 51 (asteroids) and 52 (mines), replacing the base game's class 33/34. Each MODULE file has its own set of class ID meanings.