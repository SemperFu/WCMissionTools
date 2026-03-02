# WC1 MODULE File Format -- Technical Reference

Binary format documentation for Wing Commander 1 MODULE files, built from reverse-engineered binary offsets and community tools.

## File Header (28 bytes)

| Bytes | Field | Notes |
|-------|-------|-------|
| 0-3 | File size (uncompressed) or magic bytes (compressed) | Compressed: first 2 bytes vary per file, bytes 2-3 = 0x0000 |
| 4-27 | 6 section entries x 4 bytes | 3-byte LE offset + 1-byte type (1=compressed, 2=uncompressed) |

## Compression

GOG MODULE files use **Origin Systems LZW** compression (variable-width 9-12 bit codes, code 256=clear dictionary, code 257=end of stream). Each section is compressed independently. The first 4 bytes of each compressed section store the uncompressed size as uint32 LE.

Detection: check byte 7 (first section's type byte). Type 1 = compressed, type 2 = uncompressed.

## Data Sections

| Section | Content | Preamble | Record Size | Layout |
|---------|---------|----------|-------------|--------|
| 0 | Campaign routing | -- | -- | 1,536 bytes of binary routing data |
| 1 | Encounter titles | -- | 77 bytes | 4 difficulty blocks x 16 records; flavor text for encounters |
| 2 | Nav points + briefing text | 4,928 bytes | 77 bytes/nav | 16 navs x 4 missions x 15 systems |
| 3 | Map descriptions | 4,097 bytes | 64 bytes/map | 16 entries per sortie (flat) |
| 4 | Ship definitions | 5,376 bytes | 42 bytes/ship | 32 ships per sortie (flat) |
| 5 | Difficulty names + wing assignments | -- | 40 bytes | First 160 bytes = 4 difficulty names; then 40 wing names (one per sortie) |
| 6 | System/squadron names | -- | 40 bytes | "Squadron" header + system names |

**Important:** Each section has a preamble before mission data starts. The parser uses absolute offsets: navs at 6492, maps at 84509, ships at 151324.

### Section 5 -- Wing Assignments (offset 231964)

First 160 bytes contain 4 difficulty level names at 40-byte intervals: "Beginner", "Easy", "Hard", "Ace".

Followed by 40 wing name entries at 40-byte intervals (one per sortie). Common values include "Alpha Wing", "Epsilon Wing", "Kappa Wing", "Sigma Wing", "Omicron Wing". Some entries are empty (game likely uses a default).

### Section 6 -- System Names (offset 234524)

40-byte null-terminated strings. First entry is "Squadron" (header). Subsequent entries are system names by index (0=Enyo through 12=Hell's Kitchen). Indices 13-14 contain "-Kirov" and "-Cambria" for Secret Missions 1 and 2.

## Nav Record (77 bytes)

| Offset | Size | Field |
|--------|------|-------|
| 0 | 30 | Name (null-terminated ASCII) |
| 30 | 1 | Nav type (0=manipulated/hidden, 1=dominant/visible, 2-5=follow-up waves) |
| 31 | 1 | Sphere radius (x1000, defines encounter trigger distance) |
| 32-42 | 3x4 | X, Y, Z coordinates (3-byte signed integers) |
| 45-52 | 8 | Triggers -- 4 pairs of (type, target nav index). Type: 0=deactivate nav, 1=activate nav, 255=none |
| 53, 55 | 2 | Preload ship class indices |
| 57-75 | 20 | Ship indices -- 10 slots x 2 bytes (255=empty) |

## Map/Flight Plan Record (64 bytes)

| Offset | Size | Field |
|--------|------|-------|
| 0 | 1 | Objective icon (0=square/green, 1=triangle/white/home, 2=cross/purple/friendly, 3=circle/green/friendly, 4=circle/red/enemy) |
| 1 | 1 | Target nav index (nav this objective points to) |
| 2 | 61 | Description text (null-terminated ASCII) |

## Ship Record (42 bytes)

| Offset | Size | Field |
|--------|------|-------|
| 0 | 1 | Ship class (see table below) |
| 2 | 1 | Allegiance (0=Confed, 1=Kilrathi, 2=Neutral) |
| 4 | 1 | Leader ship index (255=none) |
| 6 | 1 | Orders (see note below) |
| 8 | 1 | Formation slot |
| 10-20 | 3x4 | X, Y, Z position (3-byte signed, relative to nav point) |
| 22-26 | 6 | Rotation X, Y, Z (signed int16) |
| 28 | 2 | Speed/Size (1 byte speed x10 for fighters; 2 bytes for asteroid/mine field radius) |
| 30 | 1 | AI level (0-4) |
| 32 | 1 | Pilot ID |
| 39 | 1 | Secondary target ship index |
| 40 | 1 | Formation group |
| 41 | 1 | Primary target ship index |

## Objective Icons

| ID | Icon | Color | Usage |
|----|------|-------|-------|
| 0 | Square | Green | Mission sphere / waypoint |
| 1 | Triangle | White | Home base |
| 2 | Cross | Purple | Friendly ship |
| 3 | Circle | Green | Friendly ship |
| 4 | Circle | Red | Enemy ship |

## Orders

| ID | Order (our enum) | Order (WC Mission Editor 1.2) |
|----|------------------|-------------------------------|
| 0 | Attack | Patrol |
| 1 | Patrol | Escort |
| 2 | AttackTarget | Attack |
| 3 | Escort | Defend |
| 4 | Follow | Wingman |
| 5 | Defend | Flee |
| 6 | JumpOut | Goto warp |
| 7 | JumpIn | Warp arrive |
| 8 | GoHome | Unknown |
| 9 | Autopilot | Rendezvous |
| 10 | Navigate | Come home |
| 255 | Inactive | -- |

The correct mapping needs further verification against in-game behavior.

## Mission Header Fields

Per-mission metadata stored in sections 4/5:
- **InitialSphere** -- starting nav index for the mission
- **Carrier** -- ship index of the carrier (Tiger's Claw), -1 if none
- **YourShip** -- ship index of the player's ship
- **Convoy** -- up to 8 ship indices forming the convoy group (-1 = empty)

## 3-Byte Coordinate Encoding

Little-endian uint16 (low word) + uint8 (high byte). If high byte >= 128, subtract 16,777,216 for signed negative value.

## Ship Classes

| ID | Confed | ID | Kilrathi | ID | Other |
|----|--------|----|----------|----|-------|
| 0 | Hornet | 9 | Salthi | 22 | Asteroid Field |
| 1 | Rapier | 10 | Dralthi | 23 | Mine Field |
| 2 | Scimitar | 11 | Krant | | |
| 3 | Raptor | 12 | Gratha | | |
| 4 | Venture | 13 | Jalthi | | |
| 5 | Diligent | 14 | Hhriss | | |
| 6 | Drayman | 15 | Dorkir | | |
| 7 | Exeter | 16 | Lumbari | | |
| 8 | Tiger's Claw | 17 | Ralari | | |
| | | 18 | Fralthi | | |
| | | 19 | Snakeir | | |
| | | 20 | Sivar | | |
| | | 21 | Starpost | | |

## Named Pilots

| ID | Confed | ID | Kilrathi Ace | ID | Generic |
|----|--------|----|--------------|----|---------|
| 13 | Blair | 14 | Bhurak Starkiller | 0 | Generic 0 (easiest) |
| 5 | Spirit | 15 | Dakhath Deathstroke | 1 | Generic 1 |
| 6 | Hunter | 16 | Khajja the Fang | 2 | Generic 2 |
| 7 | Bossman | 17 | Bakhtosh Redclaw | 3 | Generic 3 |
| 8 | Iceman | | | 4 | Generic 4 (hardest) |
| 9 | Angel | | | | |
| 10 | Paladin | | | | |
| 11 | Maniac | | | | |
| 12 | Knight | | | | |

## Star Systems

| ID | Name | Missions | ID | Name | Missions |
|----|------|----------|----|------|----------|
| 0 | Enyo | 2 | 7 | Port Hedland | 3 |
| 1 | McAuliffe | 3 | 8 | Kurasawa | 3 |
| 2 | Gateway | 3 | 9 | Rostov | 3 |
| 3 | Gimle | 3 | 10 | Hubble's Star | 3 |
| 4 | Brimstone | 3 | 11 | Venice | 4 |
| 5 | Cheng-Du | 3 | 12 | Hell's Kitchen | 4 |
| 6 | Dakota | 3 | | | |

## Known Limitations (Format)

- **Orders mapping uncertain** -- Our enum and the WC Mission Editor 1.2 use different orderings. The correct mapping needs verification against in-game behavior.
- **Section offsets are hardcoded** -- Wing names (section 5) and system names (section 6) use absolute offsets derived from uncompressed MODULE.000; offsets may differ for compressed files or Secret Missions expansions.

## Wing Commander 2

See [WC2-MODULE-FORMAT.md](WC2-MODULE-FORMAT.md) for WC2 binary format notes (not yet implemented).