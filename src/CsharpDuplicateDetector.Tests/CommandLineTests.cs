using System.Text.Json;

namespace CsharpDuplicateDetector.Tests;

[TestClass]
public class CommandLineTests
{
    // Real-world solution used as the command-line tool's input for these tests.
    private const string CommonSlnPath = @"C:\GitHub\test\Automation.sln";

    private string _outputDirectory = null!;
    private string _expectedDirectory = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Output");
        _expectedDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Expected");
    }

    [TestCleanup]
    public void TestCleanup()
    {
        //if (Directory.Exists(_outputDirectory))
        //{
        //    Directory.Delete(_outputDirectory, recursive: true);
        //}
    }

    [TestMethod]
    public void Main_OnCommonSln_ExitsSuccessfullyAndWritesCsvAndJson()
    {
        Assert.IsTrue(File.Exists(CommonSlnPath), $"Expected input solution not found: {CommonSlnPath}");
        var solutionDirectory = Path.GetDirectoryName(CommonSlnPath)!;

        var exitCode = Program.Main(new[]
        {
            "--min-tokens", "100",
            solutionDirectory,
            _outputDirectory,
        });

        Assert.AreEqual(0, exitCode);

        var csvPath = Path.Combine(_outputDirectory, "duplicates.csv");
        var jsonPath = Path.Combine(_outputDirectory, "duplicates.json");
        Assert.IsTrue(File.Exists(csvPath), $"Expected CSV output at {csvPath}");
        Assert.IsTrue(File.Exists(jsonPath), $"Expected JSON output at {jsonPath}");

        var expectedCsvPath = Path.Combine(_expectedDirectory, "duplicates.csv");
        var expectedJsonPath = Path.Combine(_expectedDirectory, "duplicates.json");
        Assert.IsTrue(File.Exists(expectedCsvPath), $"Expected CSV fixture not found: {expectedCsvPath}");
        Assert.IsTrue(File.Exists(expectedJsonPath), $"Expected JSON fixture not found: {expectedJsonPath}");

        Assert.AreEqual(File.ReadAllText(expectedCsvPath), File.ReadAllText(csvPath));

        using var expectedJson = JsonDocument.Parse(File.ReadAllText(expectedJsonPath));
        using var actualJson = JsonDocument.Parse(File.ReadAllText(jsonPath));
        Assert.AreEqual(
            JsonSerializer.Serialize(expectedJson.RootElement),
            JsonSerializer.Serialize(actualJson.RootElement));
    }
}
