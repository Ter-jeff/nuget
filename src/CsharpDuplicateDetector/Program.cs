using System.Globalization;
using System.Text.Json;
using DocoptNet;

namespace CsharpDuplicateDetector;

public static class Program
{
    private const string Usage = @"Near Clone Detector.

    Usage:
      DuplicateCodeDetector [options] <solution-directory> <output-directory>
      DuplicateCodeDetector (-h | --help)
      DuplicateCodeDetector --version

    Options:
      -h --help                      Show this screen.
      -e, --exclude=<directories>          Comma-separate list of directories to exclude from duplicate detection.
      -k, --key-jaccard-threshold=<val>  The Jaccard similarity threshold for token-sets [default: 0.8].
      -j, --jaccard-threshold=<val>      The Jaccard similarity threshold for token multisets [default: 0.7].
      -t, --min-tokens=<val>             The minimum number of tokens in a compared code block [default: 20].
      -b, --block-split=<val>            Whether to split code blocks by file, class, or method. [default: method]
      -d, --only-identifiers             Only use identifier tokens for similarity comparison.

    Examples:
      DuplicateCodeDetector --min-tokens 120 ~/Git/my_repo
      DuplicateCodeDetector -d -b class ~/Git/my_repo

";

    private static readonly string[] DefaultExcludedDirectoryNames = { "bin", "obj", ".git", ".vs" };

    public static int Main(string[] args)
    {
        var arguments = new Docopt().Apply(Usage, args, version: "Near Clone Detector", exit: true);
        if (arguments is null)
        {
            return 1;
        }

        var solutionDirectory = arguments["<solution-directory>"].ToString();
        var outputDirectory = arguments["<output-directory>"].ToString();
        var excludeArg = arguments["--exclude"];
        var excluded = new HashSet<string>(DefaultExcludedDirectoryNames, StringComparer.OrdinalIgnoreCase);
        if (excludeArg is not null && !excludeArg.IsNullOrEmpty)
        {
            foreach (var name in excludeArg.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                excluded.Add(name);
            }
        }

        var keyJaccardThreshold = double.Parse(arguments["--key-jaccard-threshold"].ToString(), CultureInfo.InvariantCulture);
        var jaccardThreshold = double.Parse(arguments["--jaccard-threshold"].ToString(), CultureInfo.InvariantCulture);
        var minTokens = int.Parse(arguments["--min-tokens"].ToString(), CultureInfo.InvariantCulture);
        var blockSplit = arguments["--block-split"].ToString().ToLowerInvariant();
        var onlyIdentifiers = arguments["--only-identifiers"].IsTrue;

        var blockWord = blockSplit switch
        {
            "class" => "classes",
            "file" => "files",
            _ => "methods",
        };

        Console.WriteLine("Duplicate Detector Parameters");
        Console.WriteLine($"  Jaccard Threshold = {jaccardThreshold,5:0.00}");
        Console.WriteLine($"  Key Jaccard Threshold = {keyJaccardThreshold,5:0.00}");
        Console.WriteLine($"  Minimum Tokens = {minTokens}");
        Console.WriteLine($"  Only Identifiers = {onlyIdentifiers}");
        Console.WriteLine($"  Block Split = {blockSplit}");
        Console.WriteLine();
        Console.WriteLine("Searching for near duplicates...");

        var allBlocks = new List<CodeBlock>();
        foreach (var file in FindCsFiles(solutionDirectory, excluded))
        {
            var displayPath = Path.GetRelativePath(solutionDirectory, file);
            allBlocks.AddRange(BlockExtractor.ExtractBlocks(file, displayPath, blockSplit, onlyIdentifiers));
        }

        var qualifying = allBlocks.Where(b => b.Tokens.Count >= minTokens).ToList();
        Console.WriteLine($"Found {qualifying.Count} of {allBlocks.Count} {blockWord} have minimum of {minTokens} tokens");

        var startTime = DateTime.Now;
        var duplicates = new List<DuplicatePair>();
        for (var i = 0; i < qualifying.Count; i++)
        {
            for (var j = i + 1; j < qualifying.Count; j++)
            {
                var a = qualifying[i];
                var b = qualifying[j];
                var keyJaccard = JaccardComparer.SetJaccard(a.TokenSet, b.TokenSet);
                if (keyJaccard < keyJaccardThreshold)
                {
                    continue;
                }

                var jaccard = JaccardComparer.MultisetJaccard(a.Tokens, b.Tokens);
                if (jaccard < jaccardThreshold)
                {
                    continue;
                }

                duplicates.Add(new DuplicatePair(
                    i, j,
                    a.File, a.ClassName, a.MethodName, a.Line, a.Tokens.Count,
                    b.File, b.ClassName, b.MethodName, b.Line, b.Tokens.Count,
                    jaccard, keyJaccard));
            }
        }

        var elapsed = DateTime.Now - startTime;
        Console.WriteLine($"Found {duplicates.Count} duplicates in {allBlocks.Count} {blockWord}.");
        Console.WriteLine($"Duplicate search took {elapsed}.");
        Console.WriteLine();
        Console.WriteLine();

        var unionFind = new UnionFind(qualifying.Count);
        foreach (var d in duplicates)
        {
            unionFind.Union(d.Index1, d.Index2);
        }

        var involvedIndices = duplicates.SelectMany(d => new[] { d.Index1, d.Index2 }).Distinct();
        var clusters = unionFind.GetGroups(involvedIndices);

        Console.WriteLine($"Unique clone clusters: {clusters.Count}");
        if (duplicates.Count == 0)
        {
            Console.WriteLine("No duplicates found.");
        }
        else
        {
            var clusterNumber = 1;
            foreach (var cluster in clusters)
            {
                Console.WriteLine($"Clone cluster {clusterNumber} ({cluster.Count} {blockWord}):");
                foreach (var index in cluster)
                {
                    var block = qualifying[index];
                    var label = string.Join('.', new[] { block.ClassName, block.MethodName }.Where(s => !string.IsNullOrEmpty(s)));
                    Console.WriteLine($"  {block.File}:{block.Line} {label}");
                }

                clusterNumber++;
            }
        }

        Directory.CreateDirectory(outputDirectory);
        var csvPath = Path.Combine(outputDirectory, "duplicates.csv");
        var jsonPath = Path.Combine(outputDirectory, "duplicates.json");
        var sortedForCsv = duplicates
            .OrderBy(d => d.File1, StringComparer.Ordinal)
            .ThenBy(d => d.Method1, StringComparer.Ordinal);
        WriteCsv(csvPath, sortedForCsv);
        WriteJson(jsonPath, duplicates);

        Console.WriteLine($"Results saved to {csvPath} and {jsonPath}");
        return 0;
    }

    private static IEnumerable<string> FindCsFiles(string root, HashSet<string> excludedNames)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file);
            var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (segments.Take(segments.Length - 1).Any(excludedNames.Contains))
            {
                continue;
            }

            yield return file;
        }
    }

    private static void WriteCsv(string path, IEnumerable<DuplicatePair> duplicates)
    {
        using var writer = new StreamWriter(path, append: false);
        writer.WriteLine("File1,Class1,Method1,LineNumber1,File2,Class2,Method2,LineNumber2,JaccardSimilarity,KeyJaccardSimilarity");
        foreach (var d in duplicates)
        {
            writer.WriteLine(string.Join(',',
                CsvField(d.File1), CsvField(d.Class1), CsvField(d.Method1), d.LineNumber1,
                CsvField(d.File2), CsvField(d.Class2), CsvField(d.Method2), d.LineNumber2,
                d.JaccardSimilarity.ToString(CultureInfo.InvariantCulture),
                d.KeyJaccardSimilarity.ToString(CultureInfo.InvariantCulture)));
        }
    }

    private static string CsvField(string? value)
    {
        value ??= string.Empty;
        return value.Contains(',') || value.Contains('"')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }

    private static void WriteJson(string path, List<DuplicatePair> duplicates)
    {
        var payload = duplicates.Select(d => new[]
        {
            new
            {
                FileName = d.File1,
                ClassName = d.Class1,
                MethodName = d.Method1,
                LineNumber = d.LineNumber1,
                NumTokens = d.NumTokens1,
            },
            new
            {
                FileName = d.File2,
                ClassName = d.Class2,
                MethodName = d.Method2,
                LineNumber = d.LineNumber2,
                NumTokens = d.NumTokens2,
            },
        });

        File.WriteAllText(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
    }
}
