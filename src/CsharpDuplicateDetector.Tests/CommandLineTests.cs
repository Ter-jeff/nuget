using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsharpDuplicateDetector.Tests;

[TestClass]
public class CommandLineTests
{
    // Real-world solution used as the command-line tool's input for these tests.
    private const string CommonSlnPath = @"C:\GitHub\TrainingProgram\Common.sln";

    private string _outputDirectory = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "CsharpDuplicateDetectorTests");
    }

    [TestCleanup]
    public void TestCleanup()
    {
        if (Directory.Exists(_outputDirectory))
        {
            Directory.Delete(_outputDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void Main_OnCommonSln_ExitsSuccessfullyAndWritesCsvAndJson()
    {
        Assert.IsTrue(File.Exists(CommonSlnPath), $"Expected input solution not found: {CommonSlnPath}");
        var solutionDirectory = Path.GetDirectoryName(CommonSlnPath)!;

        var exitCode = Program.Main(new[]
        {
            "--min-tokens", "10",
            solutionDirectory,
            _outputDirectory,
        });

        Assert.AreEqual(0, exitCode);

        var csvPath = Path.Combine(_outputDirectory, "duplicates.csv");
        var jsonPath = Path.Combine(_outputDirectory, "duplicates.json");
        Assert.IsTrue(File.Exists(csvPath), $"Expected CSV output at {csvPath}");
        Assert.IsTrue(File.Exists(jsonPath), $"Expected JSON output at {jsonPath}");

        var csvLines = File.ReadAllLines(csvPath);
        Assert.IsTrue(csvLines.Length >= 1, "CSV output should at least contain the header row.");
        Assert.AreEqual(
            "File1,Class1,Method1,LineNumber1,File2,Class2,Method2,LineNumber2,JaccardSimilarity,KeyJaccardSimilarity",
            csvLines[0]);

        using var json = JsonDocument.Parse(File.ReadAllText(jsonPath));
        Assert.AreEqual(JsonValueKind.Array, json.RootElement.ValueKind);
        Assert.AreEqual(csvLines.Length - 1, json.RootElement.GetArrayLength());

        foreach (var entry in json.RootElement.EnumerateArray())
        {
            Assert.IsTrue(entry.TryGetProperty("File1", out _));
            Assert.IsTrue(entry.TryGetProperty("File2", out _));
            Assert.IsTrue(entry.TryGetProperty("JaccardSimilarity", out var jaccard));
            Assert.IsTrue(jaccard.GetDouble() is >= 0.7 and <= 1.0);
        }
    }

    [TestMethod]
    public void Main_OnCommonSlnWithClassBlockSplit_ExitsSuccessfully()
    {
        Assert.IsTrue(File.Exists(CommonSlnPath), $"Expected input solution not found: {CommonSlnPath}");
        var solutionDirectory = Path.GetDirectoryName(CommonSlnPath)!;

        var exitCode = Program.Main(new[]
        {
            "--block-split", "class",
            "--min-tokens", "20",
            solutionDirectory,
            _outputDirectory,
        });

        Assert.AreEqual(0, exitCode);
        Assert.IsTrue(File.Exists(Path.Combine(_outputDirectory, "duplicates.csv")));
        Assert.IsTrue(File.Exists(Path.Combine(_outputDirectory, "duplicates.json")));
    }
}
