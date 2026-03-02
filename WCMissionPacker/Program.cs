using System.CommandLine;
using System.Text.Json;
using WCMissionCore;

// ─── Define CLI arguments and options ───────────────────────────────────────────

Argument<FileInfo> inputArg = new("input")
{
    Description = "Binary file (MODULE/CAMP/BRIEFING) or JSON file exported by WCMissionParser"
};

Option<FileInfo?> outputOpt = new("--output", "-o")
{
    Description = "Output binary file path (defaults to <input-dir>/<input-name>.packed)"
};

Option<bool> compressOpt = new("--compress", "-c")
{
    Description = "LZW-compress sections (type=0x01) to match original game format. Default writes uncompressed (type=0xE0)."
};

Option<bool> verifyOpt = new("--verify", "-v")
{
    Description = "After packing, read back the output and verify all sections match the input."
};

var rootCommand = new RootCommand("WCMissionPacker — Packs WC1/WC2 mission data back into Origin binary container format")
{
    inputArg, outputOpt, compressOpt, verifyOpt
};

rootCommand.SetAction((parseResult) =>
{
    var input = parseResult.GetValue(inputArg)!;
    var output = parseResult.GetValue(outputOpt);
    var compress = parseResult.GetValue(compressOpt);
    var verify = parseResult.GetValue(verifyOpt);

    if (!input.Exists)
    {
        Console.Error.WriteLine($"File not found: {input.FullName}");
        return;
    }

    string ext = input.Extension.ToLowerInvariant();
    bool isJson = ext == ".json";

    // Determine output path
    string outputPath;
    if (output != null)
    {
        outputPath = output.FullName;
    }
    else if (isJson)
    {
        // JSON input → binary output: strip .json, add original extension based on content
        outputPath = Path.ChangeExtension(input.FullName, null); // removes .json
        if (!Path.HasExtension(outputPath))
            outputPath += ".bin";
    }
    else
    {
        outputPath = Path.Combine(input.DirectoryName!,
            Path.GetFileNameWithoutExtension(input.Name) + ".packed" + input.Extension);
    }

    string mode = compress ? "compressed (0x01)" : "uncompressed (0xE0)";
    byte[][] sections;

    if (isJson)
    {
        // Read JSON exported by WCMissionParser — extract RawSections
        sections = ReadSectionsFromJson(input.FullName);
        Console.WriteLine($"Read {sections.Length} sections from JSON: {input.Name}");
    }
    else
    {
        // Read binary file directly
        byte[] original = File.ReadAllBytes(input.FullName);
        sections = OriginContainerWriter.Read(original);
        Console.WriteLine($"Read {sections.Length} sections from {input.Name}");
    }

    // Write repacked file
    byte[] packed = OriginContainerWriter.Write(sections, compress);
    File.WriteAllBytes(outputPath, packed);

    Console.WriteLine($"Wrote {packed.Length} bytes to {Path.GetFileName(outputPath)} ({mode})");

    if (verify)
    {
        byte[][] reread = OriginContainerWriter.Read(packed);
        bool allMatch = sections.Length == reread.Length;
        if (allMatch)
        {
            for (int i = 0; i < sections.Length; i++)
            {
                if (!sections[i].SequenceEqual(reread[i]))
                {
                    Console.Error.WriteLine($"  VERIFY FAILED: section {i} mismatch!");
                    allMatch = false;
                }
            }
        }
        else
        {
            Console.Error.WriteLine($"  VERIFY FAILED: section count {sections.Length} vs {reread.Length}");
        }

        if (allMatch)
            Console.WriteLine($"  Verified: all {sections.Length} sections match ✓");
    }
});

return rootCommand.Parse(args).Invoke();

// ─── JSON section extraction ───────────────────────────────────────────────────

/// <summary>
/// Reads RawSections from a JSON file exported by WCMissionParser.
/// Supports all formats: the JSON may be a single object or an array (CAMP exports as array).
/// </summary>
static byte[][] ReadSectionsFromJson(string jsonPath)
{
    string json = File.ReadAllText(jsonPath);
    using var doc = JsonDocument.Parse(json);
    var root = doc.RootElement;

    // If root is an array, look in the first element
    JsonElement target = root.ValueKind == JsonValueKind.Array ? root[0] : root;

    if (!target.TryGetProperty("RawSections", out JsonElement rawProp))
        throw new InvalidDataException("JSON file does not contain RawSections. Re-export with the latest WCMissionParser.");

    var sections = new List<byte[]>();
    foreach (var section in rawProp.EnumerateArray())
    {
        string base64 = section.GetString() ?? "";
        sections.Add(base64.Length > 0 ? Convert.FromBase64String(base64) : []);
    }

    return [.. sections];
}
