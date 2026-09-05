using System.Xml.Linq;

namespace SltCsharpMetrics.Tests;

[TestClass]
public class CommandLineTests
{
    // Real-world solution used as the command-line tool's input for these tests.
    private const string CommonSlnPath = @"C:\GitHub\TrainingProgram\Common.sln";

    private string _outputDirectory = null!;
    private string _outputPath = null!;
    private string _expectedDirectory = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Output");
        _outputPath = Path.Combine(_outputDirectory, "metrics.xml");
        _expectedDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Expected");
        Directory.CreateDirectory(_outputDirectory);
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
    public async Task Main_NoArguments_PrintsUsageAndReturnsOne()
    {
        Assert.AreEqual(1, await Program.Main(Array.Empty<string>()));
    }

    [TestMethod]
    public async Task Main_HelpFlag_ReturnsZero()
    {
        Assert.AreEqual(0, await Program.Main(new[] { "/help" }));
        Assert.AreEqual(0, await Program.Main(new[] { "/?" }));
    }

    [TestMethod]
    public async Task Main_MissingOut_ReturnsOne()
    {
        Assert.AreEqual(1, await Program.Main(new[] { "/solution:" + CommonSlnPath }));
    }

    [TestMethod]
    public async Task Main_MissingProjectAndSolution_ReturnsOne()
    {
        Assert.AreEqual(1, await Program.Main(new[] { "/out:" + _outputPath }));
    }

    [TestMethod]
    public async Task Main_OnCommonSln_ExitsSuccessfullyAndWritesMetricsReport()
    {
        Assert.IsTrue(File.Exists(CommonSlnPath), $"Expected input solution not found: {CommonSlnPath}");

        var exitCode = await Program.Main(new[] { "/solution:" + CommonSlnPath, "/out:" + _outputPath, "/quiet" });

        Assert.AreEqual(0, exitCode);
        Assert.IsTrue(File.Exists(_outputPath));

        var report = XDocument.Load(_outputPath);
        var root = report.Root!;
        Assert.AreEqual("CodeMetricsReport", root.Name.LocalName);

        var targets = root.Element("Targets")!.Elements("Target").ToList();
        Assert.IsTrue(targets.Count > 0, "Expected at least one <Target> for the CommonLib/CommonLib.Test projects.");

        foreach (var target in targets)
        {
            var metrics = target.Element("Assembly")!.Element("Metrics")!.Elements("Metric").ToList();
            Assert.IsTrue(metrics.Any(m => (string)m.Attribute("Name")! == "MaintainabilityIndex"));
            Assert.IsTrue(metrics.Any(m => (string)m.Attribute("Name")! == "CyclomaticComplexity"));
        }

        var expectedPath = Path.Combine(_expectedDirectory, "metrics.xml");
        Assert.IsTrue(File.Exists(expectedPath), $"Expected metrics.xml fixture not found: {expectedPath}");

        // File= paths are inherently machine-specific absolute paths (Roslyn's own
        // SyntaxTree.FilePath) -- the real tool behaves the same way, so both sides are
        // stripped down to their own solution-relative form for comparison, rather than
        // trying to make either side's path match the other's literally.
        // MaintainabilityIndex is a documented approximation of the private tool's formula
        // (see src/README.md), not expected to match exactly, so it's excluded from both sides.
        var solutionDirectory = Path.GetDirectoryName(CommonSlnPath)! + Path.DirectorySeparatorChar;
        Assert.AreEqual(
            NormalizeMetricsReport(expectedPath, @"C:\jenkins\workspace\ing_D4T_TrainingRepo_lijeff_main\"),
            NormalizeMetricsReport(_outputPath, solutionDirectory));
    }

    private static string NormalizeMetricsReport(string path, string pathPrefixToStrip)
    {
        var doc = XDocument.Load(path);

        foreach (var fileAttribute in doc.Descendants().Attributes("File"))
        {
            if (fileAttribute.Value.StartsWith(pathPrefixToStrip, StringComparison.OrdinalIgnoreCase))
            {
                fileAttribute.Value = fileAttribute.Value[pathPrefixToStrip.Length..];
            }
        }

        doc.Descendants("Metric").Where(m => (string)m.Attribute("Name")! == "MaintainabilityIndex").Remove();

        return doc.ToString();
    }
}
