# WC1 BRIEFING File Format (BRIEFING.000 / .001 / .002)

## Overview

BRIEFING files contain **all dialogue and cutscene scripting** for Wing Commander 1 — mission briefings, debriefings, bar conversations, funeral speeches, medal ceremonies, and Colonel Halcyon's office scenes. Each line of dialogue includes text, lip-sync phonetics, facial expression animation codes, and scene composition (background/foreground image indices, timing).

For a WC1 remake, this is the **primary narrative data file** — it drives every non-combat scene in the game.

**Files:**
| File | Game | Size | Contents |
|------|------|------|----------|
| `BRIEFING.000` | WC1 Vega Campaign | ~222 KB | 56 conversation blocks, ~209 conversations |
| `BRIEFING.001` | Secret Missions 1 | ~85 KB | SM1 conversations |
| `BRIEFING.002` | Secret Missions 2 | ~90 KB | SM2 conversations |
| `BRIEFING.VGA` | Image set | ~517 KB | Background/foreground images referenced by index |

## Container Format

BRIEFING files use the same Origin Systems container format as MODULE and CAMP files — 4-byte file size header followed by a section table with 3-byte offsets and 1-byte type flags. Sections are LZW compressed (type 0x01) in WC1.

## High-Level Structure

The file is organized into **conversation blocks**, each containing multiple **conversations**. WCToolbox identifies these block types:

| Block | Purpose | Count (base game) |
|-------|---------|-------------------|
| **Funeral** | Pilot death eulogies (varies by career length) | 1 block, 7 conversations |
| **Office** | Colonel Halcyon's office (promotions, ejection warnings, transfers) | 1 block, 1+ conversations |
| **Ceremony** | Medal award ceremonies | 1 block, 1 conversation |
| **Unused** | Empty/reserved | 1 block, 0 conversations |
| **S01M0–S13M3** | Per-sortie dialogue (52 blocks = 13 series × 4 missions) | 52 blocks, 5 conversations each |

### Per-Mission Blocks (S##M#)

Each mission block contains **5 conversations** that play at different points during the sortie:

| Conversation | Scene | Content |
|-------------|-------|---------|
| 0 | **Mission Briefing** | Colonel addresses squadron, assigns wingman, describes patrol route, shows nav map |
| 1 | **Mission Debriefing** | Colonel reviews performance, kill scores, wingman status (alive/dead), conditional dialogue paths |
| 2 | **Bar — Shotglass** | Bartender conversation (tips, stories, intel on wingman) |
| 3 | **Bar — Character 1** | Named pilot conversation (e.g., Angel's combat analysis, Paladin's war stories) |
| 4 | **Bar — Character 2** | Second named pilot conversation |

## Conversation Structure

Each conversation consists of:

### Dialog Settings (per line)

Each dialog line has a corresponding **setting** that controls scene composition:

| Field | Type | Description |
|-------|------|-------------|
| Background | int8 | Background image index (-1 = keep previous, -2 = fade/clear) |
| Foreground | int8 | Foreground character image index (-1 = keep previous, -2 = fade/clear) |
| TextColor | int8 | Text rendering color index (-1 = keep previous) |
| Delay | int16 | Timing delay (in game ticks, roughly centiseconds) |

Image indices reference sprites in `BRIEFING.VGA` / `.V00`.

### Dialog Lines

Each dialog line contains:

| Field | Description |
|-------|-------------|
| **Text** | Displayed dialogue with variable tokens |
| **LipSyncText** | Phonetic representation for mouth animation |
| **FacialExpressions** | Animation command string (e.g., `RA45,81,01,A35,81,01,A50,81,02,`) |
| **Commands** | Conditional display commands (e.g., `[30]:01,01;`) |

### Text Variable Tokens

Dialogue text uses these placeholder tokens, substituted at runtime:

| Token | Meaning |
|-------|---------|
| `$C` | Player callsign |
| `$N` | Player last name |
| `$R` | Player rank (e.g., "Captain") |
| `$S` | Current star system name |
| `$T` | Current time |
| `$D` | Current date |
| `$K` | Player's kill count for the mission |
| `$L` | Wingman's kill count |
| `$E` | Date of event (ceremonies) |
| `$A` | Award/medal name |

### Facial Expression Codes

Animation strings like `RA45,81,01,A35,81,01,A50,81,02,` encode mouth/face animation frames:
- **R** = Reset animation
- **A** = Animation frame
- Numbers encode frame index, duration, and expression type
- These sync with the `LipSyncText` phonetic string to create lip movement

### Conditional Commands

Commands like `[30]:01,01;` control conditional dialogue branching:
- Used for context-sensitive lines (e.g., different funeral speeches per character)
- `[N]:X,Y;` format — likely references game state variable N with parameters X, Y
- Some commands trigger jumps within the conversation (e.g., `[16]:10;` skips to dialog 10 under certain conditions)

## Special Conversation Blocks

### Funeral Block

Contains multiple eulogy variants based on the dead pilot's career:
- **Conv 0**: Generic short farewell ("Farewell, $C. You'll be missed.")
- **Conv 1**: Rookie death ("He died without even a chance to prove himself.")
- **Conv 2**: Short career ("In just a few missions, $C began what would surely have been a brilliant career.")
- **Conv 3**: Veteran ("$C was one of the Confederation's finest pilots.")
- **Conv 4**: Long career ("No one fought harder to hold back the advancing Kilrathi horde than $C.")
- **Conv 5**: Player's own farewell — character-specific lines for each wingman (Spirit, Hunter, Bossman, Iceman, Angel, Paladin, Maniac, Knight)
- **Conv 6**: Named pilot funerals — full eulogies for Spirit (Mariko Tanaka), Maniac (Todd Marshall), Bossman (Kien Chen), Iceman (Michael Casey), Angel (Jeannette Devereaux), Paladin (James Taggart), Knight (Joseph Khumalo)

### Office Block

Colonel Halcyon's office scenes, including:
- **Promotions**: "The brass have been reviewing your record, and I have good news..."
- **Ejection warnings**: "That ship you just bailed out of cost over a hundred million credits."
- **Golden Sun award**: For ejecting and surviving
- **Ship/squadron transfers**: Reassignment to Hornets/Scimitars/Raptors/Rapiers with named squadrons (Killer Bees, Blue Devils, Star Slayers, Black Lions)

### Ceremony Block

Medal award ceremonies on the hangar deck:
- Formal citation text with variable tokens for medal name (`$A`), rank, date
- Conditional text based on medal type (Bronze/Silver/Gold Star, Golden Sun)
- Applause and congratulations sequence

## Example: Enyo Mission 1 Briefing (S01M0, Conv 0)

```
"Mission Briefing, Enyo System, $T hours, $D."

"We've got a lot of work to do, people, so let's get to it."
"The Tiger's Claw dropped from jumpspace seven hours ago, at 08:00."
"Blue Devil squadron had first patrol. You Killer Bees have the next shift."
"You rookies'll be flying with experienced pilots on your first missions."
"I want the rookies to fly as wingleaders.
 You vets keep an eye on the kids out there."
"Here are the assignments."
"$C, you're leading Alpha wing."
"Spirit will fly on your wing. She's quiet, but she knows the ropes."
"You're the wingleader, but if Spirit talks, you be sure and listen. Got it?"
[Player]: "Yes, sir."
"Good. Here's your patrol plan, then."
"Computer, display Alpha."
"You'll check three possible jump points, at about 20,000 klicks out."
"There are asteroids near Nav Points 2 and 3, so stay on course."
"Any questions?"
[Spirit]: "Yes, commander. What are we to do if we encounter the enemy?"
"Engage, if the odds look good. Let $C make the call."
"Next is Beta wing..."
[Narrator]: "Your thoughts wander as the commander makes the rest of the assignments."
"...and back to the Tiger's Claw."
"Remember ... this is no trainsim. If you see the enemy, he'll be out to kill you."
"Be sure you do it to him before he does it to you."
"Squadron dismissed."
```

## Relationship to Other Files

| File | Relationship |
|------|-------------|
| **MODULE.000** | Mission nav points/ships — BRIEFING provides the narrative around each mission |
| **CAMP.000** | Campaign tree — determines which series/mission block to play from BRIEFING |
| **BRIEFING.VGA** | Image sprites — Background/Foreground indices in DialogSettings reference these |
| **PAL files** | Color palettes — TextColor indices reference palette entries |

## Data Value for Remake

The BRIEFING file provides everything needed to recreate WC1's non-combat scenes:

| Data | Use in Unity Remake |
|------|-------------------|
| Dialog text + tokens | Subtitle/text display system |
| LipSync phonetics | Mouth animation driver |
| Facial expressions | Character animation sequences |
| Background/Foreground indices | Scene composition (which sprites to show) |
| Delay timing | Pacing and auto-advance timing |
| Conditional commands | Branching dialogue (wingman alive/dead, performance) |
| Per-mission structure | Mapping briefings/debriefings to the correct sortie |

## Future Parser Notes

A BRIEFING parser should:
1. Decompress sections (LZW, same as MODULE/CAMP)
2. Parse conversation blocks and identify block type (Funeral, Office, Ceremony, S##M#)
3. For each conversation, extract dialog settings and dialog lines
4. Decode variable tokens, facial expression codes, and conditional commands
5. Output structured JSON with all text, timing, animation, and scene data

WCToolbox (`WC1ToolsCmd.exe extract` and `xmlunpack`) can extract BRIEFING files to both human-readable text and XML, which can be used to validate parser output.
