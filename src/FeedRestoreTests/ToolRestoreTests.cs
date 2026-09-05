using System.Diagnostics;

namespace FeedRestoreTests;

[TestClass]
public class ToolRestoreTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [TestMethod]
    public void DotnetToolRestore_Succeeds()
    {
        var result = Run("dotnet", "tool restore");
        Assert.AreEqual(0, result.ExitCode, $"dotnet tool restore failed.\nSTDOUT:\n{result.StdOut}\nSTDERR:\n{result.StdErr}");
    }

    [TestMethod]
    public void CsharpDuplicateDetector_ResolvesFromFeedAndReportsVersion()
    {
        Run("dotnet", "tool restore");
        var result = Run("dotnet", "csharp-duplicate-detector --version");
        Assert.AreEqual(0, result.ExitCode, $"STDOUT:\n{result.StdOut}\nSTDERR:\n{result.StdErr}");
        StringAssert.Contains(result.StdOut, "Near Clone Detector");
    }

    [TestMethod]
    public void SltCsharpMetrics_ResolvesFromFeedAndShowsHelp()
    {
        Run("dotnet", "tool restore");
        var result = Run("dotnet", "slt-csharp-metrics /help");
        Assert.AreEqual(0, result.ExitCode, $"STDOUT:\n{result.StdOut}\nSTDERR:\n{result.StdErr}");
        StringAssert.Contains(result.StdOut, "Usage: Metrics.exe");
    }

    private static (int ExitCode, string StdOut, string StdErr) Run(string fileName, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName, arguments)
            {
                WorkingDirectory = RepoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };

        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stdout, stderr);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NuGet.Config")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Could not locate repo root (NuGet.Config not found).");
    }
}
