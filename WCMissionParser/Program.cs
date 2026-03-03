using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Serialization;
using WCMissionCore;

// ─── Define CLI arguments and options ───────────────────────────────────────────

Argument<FileInfo> moduleArg = new("file")
{
    Description = "Path to a Wing Commander data file (MODULE.000/.001/.002 or CAMP.000/.001/.002)"
};

Option<int?> sortieOption = new("--sortie", "-s")
{
    Description = "Filter output to a single sortie index"
};

Option<bool> jsonOption = new("--json", "-j")
{
    Description = "Export missions as JSON files (default if no format specified)"
};

Option<bool> xmlOption = new("--xml", "-x")
{
    Description = "Export missions as XML files (use with -json for both formats)"
};

Option<bool> singleFileOption = new("--single-file")
{
    Description = "Combine all missions into one file instead of per-mission files"
};

Option<DirectoryInfo?> outputOption = new("--output", "-o")
{
    Description = "Output directory for exported files (defaults to same folder as input file)"
};

Option<bool> listOption = new("--list", "-l")
{
    Description = "List available sorties with system and mission info, then exit"
};

var jsonExportOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter() },
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
};

// ─── Build command and set action ───────────────────────────────────────────────

var rootCommand = new RootCommand("WC Mission Parser — parses Wing Commander 1 & 2 binary MODULE, CAMP, and BRIEFING files");
rootCommand.Arguments.Add(moduleArg);
rootCommand.Options.Add(sortieOption);
rootCommand.Options.Add(jsonOption);
rootCommand.Options.Add(xmlOption);
rootCommand.Options.Add(singleFileOption);
rootCommand.Options.Add(outputOption);
rootCommand.Options.Add(listOption);

rootCommand.SetAction(result =>
{
    var moduleFile = result.GetValue(moduleArg)!;
    var sortie = result.GetValue(sortieOption);
    var json = result.GetValue(jsonOption);
    var xml = result.GetValue(xmlOption);
    var singleFile = result.GetValue(singleFileOption);
    var outputDir = result.GetValue(outputOption);
    var list = result.GetValue(listOption);

    if (!moduleFile.Exists)
    {
        Console.Error.WriteLine($"File not found: {moduleFile.FullName}");
        return 1;
    }

    string modulePath = moduleFile.FullName;
    string outputBase = outputDir?.FullName ?? Path.GetDirectoryName(modulePath)!;

    // Default to JSON if neither format is specified
    bool exportJson = json || !xml;
    bool exportXml = xml;

    // Detect file type: BRIEFING vs CAMP vs MODULE
    string baseName = Path.GetFileNameWithoutExtension(modulePath).ToUpperInvariant();
    bool isBriefing = baseName == "BRIEFING";
    bool isCamp = baseName == "CAMP";

    if (isBriefing)
    {
        RunBriefing(modulePath, exportJson, exportXml, singleFile, outputBase, list);
        return 0;
    }

    if (isCamp)
    {
        RunCamp(modulePath, exportJson, exportXml, singleFile, outputBase, list);
        return 0;
    }

    // Detect WC1 vs WC2 by reading the type byte
    byte[] rawHeader = new byte[8];
    using (var fs = File.OpenRead(modulePath))
        fs.ReadExactly(rawHeader, 0, 8);
    bool isWc2 = Wc2ModuleParser.IsWc2Module(rawHeader);

    if (list)
    {
        if (isWc2)
        {
            var module = new Wc2ModuleParser().Parse(modulePath);
            Console.WriteLine($"{module.Missions.Count} WC2 missions in {Path.GetFileName(modulePath)}:\n");
            foreach (var m in module.Missions)
            {
                string label = string.IsNullOrEmpty(m.MissionLabel) ? "" : $" -- {m.MissionLabel}";
                string sys = string.IsNullOrEmpty(m.SystemName) ? "" : $" [{m.SystemName}]";
                Console.WriteLine($"  Sortie {m.SortieIndex,2}  System {m.SystemIndex}, Mission {m.MissionIndex}{sys}{label}  ({m.NavPoints.Count} navs, {m.Ships.Count} ships)");
            }
        }
        else
        {
            var module = new ModuleParser().Parse(modulePath);
            Console.WriteLine($"{module.Missions.Count} WC1 missions in {Path.GetFileName(modulePath)}:\n");
            foreach (var m in module.Missions)
            {
                string wing = string.IsNullOrEmpty(m.WingName) ? "" : $" -- {m.WingName}";
                string sys = string.IsNullOrEmpty(m.SystemName) ? "" : $" [{m.SystemName}]";
                Console.WriteLine($"  Sortie {m.SortieIndex,2}  System {m.SystemIndex}, Mission {m.MissionIndex}{sys}{wing}  ({m.NavPoints.Count} navs, {m.Ships.Count} ships)");
            }
        }
        return 0;
    }

    if (isWc2)
        RunWc2(modulePath, sortie, exportJson, exportXml, singleFile, outputBase);
    else
        RunWc1(modulePath, sortie, exportJson, exportXml, singleFile, outputBase);

    return 0;
});

return rootCommand.Parse(args).Invoke();

// ─── WC2 ───────────────────────────────────────────────────────────────────────

void RunWc2(string modulePath, int? filterSortie, bool exportJson, bool exportXml, bool singleFile, string outputBase)
{
    var parser = new Wc2ModuleParser();
    var module = parser.Parse(modulePath);
    Console.Error.WriteLine($"Parsed {module.Missions.Count} WC2 missions from {Path.GetFileName(modulePath)}");

    var missions = filterSortie.HasValue
        ? module.Missions.Where(m => m.SortieIndex == filterSortie.Value).ToList()
        : module.Missions;

    if (missions.Count == 0)
    {
        var valid = module.Missions.Select(m => m.SortieIndex).OrderBy(i => i).ToList();
        Console.Error.WriteLine($"Error: Sortie {filterSortie} not found. {valid.Count} missions available.");
        Console.Error.WriteLine($"  Valid sorties: {string.Join(", ", valid)}");
        return;
    }

    // Console output
    foreach (var mission in missions)
    {
        string label = string.IsNullOrEmpty(mission.MissionLabel)? "" : $" -- {mission.MissionLabel}";
        string sys = string.IsNullOrEmpty(mission.SystemName) ? "" : $" [{mission.SystemName}]";
        Console.WriteLine($"\n=== Sortie {mission.SortieIndex} (System {mission.SystemIndex}, Mission {mission.MissionIndex}){sys}{label} ===");
        Console.WriteLine($"  Nav Points: {mission.NavPoints.Count}");
        foreach (var nav in mission.NavPoints)
        {
            string shipList = nav.ShipIndices.Length > 0
                ? string.Join(",", nav.ShipIndices)
                : "none";
            Console.WriteLine($"    [{nav.Index}] \"{nav.Name}\" XYZ=({nav.X},{nav.Y},{nav.Z}) ships=[{shipList}]");
        }

        Console.WriteLine($"  Flight Plans: {mission.FlightPlans.Count}");
        foreach (var fp in mission.FlightPlans)
            Console.WriteLine($"    [{fp.Index}] icon={fp.ObjectiveIcon} nav={fp.TargetNav} \"{fp.Description}\"");

        Console.WriteLine($"  Ships: {mission.Ships.Count}");
        foreach (var ship in mission.Ships)
        {
            string leader = ship.Leader >= 0 ? $"leader={ship.Leader}" : "no-leader";
            string charName = ship.Character != Wc2Character.None ? $" char={ship.Character}" : "";
            Console.WriteLine($"    [{ship.Index:D2}] {ship.Class,-14} {ship.Allegiance,-8} {leader,-12} orders={ship.Orders,-10} XYZ=({ship.X},{ship.Y},{ship.Z}) spd={ship.Speed}{charName}");
        }
    }

    // Export
    if (exportJson)
    {
        if (singleFile)
            ExportJson(new[] { module }, modulePath, outputBase, true, _ => 0, _ => "module");
        else
            ExportJson(missions, modulePath, outputBase, false, m => m.SortieIndex, m => m.SystemName);
    }
    if (exportXml)
        ExportXml(missions, modulePath, outputBase, singleFile, m => m.SortieIndex, m => m.SystemName);
}

// ─── WC1 ───────────────────────────────────────────────────────────────────────

void RunWc1(string modulePath, int? filterSortie, bool exportJson, bool exportXml, bool singleFile, string outputBase)
{
    var parser = new ModuleParser();
    var module = parser.Parse(modulePath);
    Console.Error.WriteLine($"Parsed {module.Missions.Count} WC1 missions from {Path.GetFileName(modulePath)}");

    var missions = filterSortie.HasValue
        ? module.Missions.Where(m => m.SortieIndex == filterSortie.Value).ToList()
        : module.Missions;

    if (missions.Count == 0)
    {
        var valid = module.Missions.Select(m => m.SortieIndex).OrderBy(i => i).ToList();
        Console.Error.WriteLine($"Error: Sortie {filterSortie} not found. {valid.Count} missions available.");
        Console.Error.WriteLine($"  Valid sorties: {string.Join(", ", valid)}");
        return;
    }

    // Console output
    foreach (var mission in missions)
    {
        string wing = string.IsNullOrEmpty(mission.WingName)? "" : $" -- {mission.WingName}";
        string sys = string.IsNullOrEmpty(mission.SystemName) ? "" : $" [{mission.SystemName}]";
        Console.WriteLine($"\n=== Sortie {mission.SortieIndex} (System {mission.SystemIndex}, Mission {mission.MissionIndex}){sys}{wing} ===");

        // Show encounter titles if any non-empty values exist
        var titles = mission.EncounterTitles;
        string[] diffNames = ["Beginner", "Easy", "Hard", "Ace"];
        bool hasTitle = titles.Any(t => !string.IsNullOrEmpty(t));
        if (hasTitle)
        {
            string titleStr = string.Join(" / ", titles.Select((t, i) => string.IsNullOrEmpty(t) ? null : $"{diffNames[i]}: \"{t}\"").Where(s => s != null));
            Console.WriteLine($"  Encounter: {titleStr}");
        }

        Console.WriteLine($"  Nav Points: {mission.NavPoints.Count}");
        foreach (var nav in mission.NavPoints)
        {
            string shipList = nav.ShipIndices.Length > 0
                ? string.Join(",", nav.ShipIndices)
                : "none";
            string radiusStr = nav.Radius > 0 ? $" radius={nav.Radius}" : "";
            Console.WriteLine($"    [{nav.Index}] \"{nav.Name}\" XYZ=({nav.X},{nav.Y},{nav.Z}){radiusStr} ships=[{shipList}]");
        }

        Console.WriteLine($"  Map Points: {mission.MapPoints.Count}");
        foreach (var mp in mission.MapPoints)
            Console.WriteLine($"    [{mp.Index}] icon={mp.IconFormat} target={mp.TargetIndex} \"{mp.Description}\"");

        Console.WriteLine($"  Ships: {mission.Ships.Count}");
        foreach (var ship in mission.Ships)
        {
            string leader = ship.Leader >= 0 ? $"leader={ship.Leader}" : "no-leader";
            Console.WriteLine($"    [{ship.Index:D2}] {ship.Class,-14} {ship.Allegiance,-8} {leader,-12} orders={ship.Orders,-10} XYZ=({ship.X},{ship.Y},{ship.Z}) spd={ship.Speed} pilot={ship.Pilot}");
        }
    }

    // Export
    if (exportJson)
    {
        if (singleFile)
            ExportJson(new[] { module }, modulePath, outputBase, true, _ => 0, _ => "module");
        else
            ExportJson(missions, modulePath, outputBase, false, m => m.SortieIndex, m => m.SystemName);
    }
    if (exportXml)
        ExportXml(missions, modulePath, outputBase, singleFile, m => m.SortieIndex, m => m.SystemName);
}

// ─── CAMP ──────────────────────────────────────────────────────────────────────

void RunCamp(string campPath, bool exportJson, bool exportXml, bool singleFile, string outputBase, bool list)
{
    var parser = new CampParser();
    var camp = parser.Parse(campPath);
    string fileName = Path.GetFileName(campPath);

    Console.Error.WriteLine($"Parsed CAMP file: {fileName}");
    Console.Error.WriteLine($"  {camp.SeriesBranches.Count} series, {camp.StellarBackgrounds.Count} backgrounds, {camp.BarSeatings.Count} bar seatings");

    // Character names for display
    string[] characters = ["Paladin", "Angel", "Bossman", "Knight", "Maniac", "Spirit", "Hunter", "Iceman"];
    string[] ships = ["Hornet", "Rapier", "Scimitar", "Raptor"];
    string[] medals = ["None", "Bronze Star", "Silver Star", "?", "Gold Star"];

    string CharName(int idx) => idx >= 0 && idx < characters.Length ? characters[idx] : idx.ToString();
    string ShipName(int idx) => idx >= 0 && idx < ships.Length ? ships[idx] : idx.ToString();
    string MedalName(int idx) => idx >= 0 && idx < medals.Length ? medals[idx] : idx.ToString();

    if (list)
    {
        Console.WriteLine($"\nCampaign Tree ({camp.SeriesBranches.Count} series):\n");
        foreach (var s in camp.SeriesBranches)
        {
            string winTo = s.SuccessSeries >= 0 ? $"→ Series {s.SuccessSeries} ({ShipName(s.SuccessShip)})" : "→ END";
            string loseTo = s.FailureSeries >= 0 ? $"→ Series {s.FailureSeries} ({ShipName(s.FailureShip)})" : "→ END";
            string cutscene = s.Cutscene >= 0 ? $" cutscene={s.Cutscene}" : "";
            Console.WriteLine($"  Series {s.SeriesIndex,2}: {s.MissionsActive} missions, wingman={CharName(s.Wingman)}, score≥{s.SuccessScore}{cutscene}");
            Console.WriteLine($"           Win {winTo}  |  Lose {loseTo}");
        }
        return;
    }

    // Full output
    Console.WriteLine($"\n=== Campaign Tree ===");
    foreach (var s in camp.SeriesBranches)
    {
        string winTo = s.SuccessSeries >= 0 ? $"Series {s.SuccessSeries} ({ShipName(s.SuccessShip)})" : "END";
        string loseTo = s.FailureSeries >= 0 ? $"Series {s.FailureSeries} ({ShipName(s.FailureShip)})" : "END";
        string cutscene = s.Cutscene >= 0 ? $", cutscene={s.Cutscene}" : "";
        Console.WriteLine($"\nSeries {s.SeriesIndex}: {s.MissionsActive} missions, wingman={CharName(s.Wingman)}, score≥{s.SuccessScore}{cutscene}");
        Console.WriteLine($"  Win → {winTo}  |  Lose → {loseTo}");
        for (int m = 0; m < s.MissionsActive; m++)
        {
            var sc = s.MissionScorings[m];
            string flightPath = string.Join(" ", sc.FlightPathScoring);
            string medal = sc.Medal > 0 ? $" medal={MedalName(sc.Medal)} (score≥{sc.MedalScore})" : "";
            Console.WriteLine($"  Mission {m}: scoring=[{flightPath}]{medal}");
        }
    }

    Console.WriteLine($"\n=== Bar Seating ===");
    foreach (var b in camp.BarSeatings)
    {
        if (b.LeftSeat >= 0 || b.RightSeat >= 0)
            Console.WriteLine($"  Sortie {b.SortieIndex,2}: Left={CharName(b.LeftSeat),-8} Right={CharName(b.RightSeat)}");
    }

    // Export
    if (exportJson)
        ExportJson(new[] { camp }, campPath, outputBase, true, _ => 0, _ => "campaign");
    if (exportXml)
        ExportXml(new[] { camp }, campPath, outputBase, true, _ => 0, _ => "campaign");
}

// ─── BRIEFING ──────────────────────────────────────────────────────────────────

void RunBriefing(string briefPath, bool exportJson, bool exportXml, bool singleFile, string outputBase, bool list)
{
    var parser = new BriefingParser();
    var briefing = parser.Parse(briefPath);
    string fileName = Path.GetFileName(briefPath);

    int totalConversations = briefing.Blocks.Sum(b => b.Conversations.Count);
    int totalDialogs = briefing.Blocks.Sum(b => b.Conversations.Sum(c => c.Dialogs.Count));
    Console.Error.WriteLine($"Parsed BRIEFING file: {fileName}");
    Console.Error.WriteLine($"  {briefing.Blocks.Count} blocks, {totalConversations} conversations, {totalDialogs} dialog lines");

    if (list)
    {
        Console.WriteLine($"\nBriefing Blocks ({briefing.Blocks.Count}):\n");
        foreach (var block in briefing.Blocks)
        {
            string convInfo = block.Conversations.Count > 0
                ? $"{block.Conversations.Count} conversations, {block.Conversations.Sum(c => c.Dialogs.Count)} lines"
                : "empty";
            Console.WriteLine($"  [{block.BlockIndex,2}] {block.BlockType,-12} {convInfo}");
        }
        return;
    }

    // Full output
    foreach (var block in briefing.Blocks)
    {
        if (block.Conversations.Count == 0) continue;
        Console.WriteLine($"\n=== {block.BlockType} (Block {block.BlockIndex}) ===");

        for (int ci = 0; ci < block.Conversations.Count; ci++)
        {
            var conv = block.Conversations[ci];
            Console.WriteLine($"\n  --- Conversation {ci} ({conv.Dialogs.Count} lines) ---");
            foreach (var dialog in conv.Dialogs)
            {
                string cmd = !string.IsNullOrEmpty(dialog.Commands) ? $" cmd=[{dialog.Commands}]" : "";
                Console.WriteLine($"    \"{dialog.Text}\"{cmd}");
            }
        }
    }

    // Export
    if (exportJson)
        ExportJson(new[] { briefing }, briefPath, outputBase, true, _ => 0, _ => "briefing");
    if (exportXml)
        ExportXml(new[] { briefing }, briefPath, outputBase, true, _ => 0, _ => "briefing");
}

// ─── Export helpers ─────────────────────────────────────────────────────────────

void ExportJson<T>(IList<T> missions, string modulePath, string outputBase, bool singleFile,
    Func<T, int> getSortie, Func<T, string?> getSystem)
{
    string moduleFileName = Path.GetFileName(modulePath);
    if (singleFile)
    {
        string jsonPath = Path.Combine(outputBase, Path.ChangeExtension(moduleFileName, ".json"));
        string json = JsonSerializer.Serialize(missions, jsonExportOptions);
        File.WriteAllText(jsonPath, json);
        Console.Error.WriteLine($"JSON written to {jsonPath}");
    }
    else
    {
        string exportDir = Path.Combine(outputBase, moduleFileName + "_export");
        Directory.CreateDirectory(exportDir);
        foreach (var mission in missions)
        {
            string sysName = SanitizeName(getSystem(mission));
            string fileName = $"sortie_{getSortie(mission):D2}_{sysName}.json";
            string json = JsonSerializer.Serialize(mission, jsonExportOptions);
            File.WriteAllText(Path.Combine(exportDir, fileName), json);
        }
        Console.Error.WriteLine($"JSON files written to {exportDir}\\  ({missions.Count} files)");
    }
}

void ExportXml<T>(IList<T> missions, string modulePath, string outputBase, bool singleFile,
    Func<T, int> getSortie, Func<T, string?> getSystem)
{
    string moduleFileName = Path.GetFileName(modulePath);
    var serializer = new XmlSerializer(typeof(List<T>));
    var itemSerializer = new XmlSerializer(typeof(T));

    if (singleFile)
    {
        string xmlPath = Path.Combine(outputBase, Path.ChangeExtension(moduleFileName, ".xml"));
        using var writer = new StreamWriter(xmlPath);
        serializer.Serialize(writer, missions.ToList());
        Console.Error.WriteLine($"XML written to {xmlPath}");
    }
    else
    {
        string exportDir = Path.Combine(outputBase, moduleFileName + "_export");
        Directory.CreateDirectory(exportDir);
        foreach (var mission in missions)
        {
            string sysName = SanitizeName(getSystem(mission));
            string fileName = $"sortie_{getSortie(mission):D2}_{sysName}.xml";
            using var writer = new StreamWriter(Path.Combine(exportDir, fileName));
            itemSerializer.Serialize(writer, mission);
        }
        Console.Error.WriteLine($"XML files written to {exportDir}\\  ({missions.Count} files)");
    }
}

static string SanitizeName(string? name)
{
    if (string.IsNullOrWhiteSpace(name)) return "unknown";
    return name.Trim().ToLowerInvariant()
        .Replace(" ", "").Replace("'", "").Replace(".", "");
}
