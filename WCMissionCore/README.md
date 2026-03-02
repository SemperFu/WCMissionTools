# WCMissionCore

Shared class library containing all parsers, models, enums, and binary I/O for Wing Commander 1 & 2 data files. Used by [WCMissionParser](../WCMissionParser/README.md), [WCMissionPacker](../WCMissionPacker/README.md), and [WCMissionViewer](../WCMissionViewer/README.md).

## Parsers

| Parser | File Types | Description |
|--------|-----------|-------------|
| `ModuleParser` | MODULE.000/.001/.002 | WC1 mission data (nav points, ships, map points) |
| `Wc2ModuleParser` | MODULE.000/.001/.002 | WC2 mission data (nav points, ships, flight plans) |
| `CampParser` | CAMP.000/.001/.002 | WC1 campaign branching (series, scoring, medals, bar seating) |
| `BriefingParser` | BRIEFING.000/.001/.002 | WC1 briefing dialogue (conversations, lip-sync, facial animation) |

All parsers auto-detect compressed vs uncompressed sections and handle LZW decompression transparently.

## Binary I/O

| Component | Description |
|-----------|-------------|
| `LzwDecompressor` | Origin Systems LZW decompression (9→12 bit variable-width codes) |
| `LzwCompressor` | Origin Systems LZW compression — produces byte-for-byte identical output to Origin's 1990 compressor |
| `OriginContainerWriter` | Reads and writes the Origin container format (section table + typed section blobs) |

## Models

| File | Contents |
|------|----------|
| `Models.cs` | WC1 types: `ModuleFile`, `Mission`, `NavPoint`, `Ship`, `MapPoint` |
| `Wc2Models.cs` | WC2 types: `Wc2ModuleFile`, `Wc2Mission`, `Wc2NavPoint`, `Wc2Ship`, `Wc2FlightPlan` |
| `CampModels.cs` | Campaign types: `CampFile`, `SeriesBranch`, `MissionScoring`, `BarSeating` |
| `BriefingModels.cs` | Briefing types: `BriefingFile`, `ConversationBlock`, `DialogLine`, `DialogSetting` |
| `Enums.cs` | WC1 enums: `ShipClass`, `Allegiance`, `ShipOrders`, `Pilot` |
| `Wc2Enums.cs` | WC2 enums: `Wc2ShipClass`, `Wc2Character`, `Wc2Orders` |

All models include an optional `byte[][]? RawSections` property for lossless round-trip repacking through JSON.
