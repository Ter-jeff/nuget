using System.Collections.Immutable;
using System.Text;
using System.Xml;
using Metrics;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeMetrics;
using Microsoft.CodeAnalysis.MSBuild;

namespace SltCsharpMetrics;

public static class Program
{
    private const string Usage = @"
Usage: Metrics.exe <arguments>

Help for command-line arguments:

/project:<project-file>  [Short form: /p:<project-file>]
Project(s) to analyze.

/solution:<solution-file>  [Short form: /s:<solution-file>]
Solution(s) to analyze.

/out:<file>  [Short form: /o:<file>]
Metrics results XML output file.

/quiet  [Short form: /q]
Silence all console output other than error reporting.

/help  [Short form: /?]
Display this help message.
";

    public static async Task<int> Main(string[] args)
    {
        var projects = new List<string>();
        var solutions = new List<string>();
        string? outPath = null;
        var quiet = false;
        var help = args.Length == 0;

        foreach (var arg in args)
        {
            if (TryGetValue(arg, "/project:", "/p:", out var projectValue))
            {
                projects.AddRange(projectValue.Split(',', StringSplitOptions.RemoveEmptyEntries));
            }
            else if (TryGetValue(arg, "/solution:", "/s:", out var solutionValue))
            {
                solutions.AddRange(solutionValue.Split(',', StringSplitOptions.RemoveEmptyEntries));
            }
            else if (TryGetValue(arg, "/out:", "/o:", out var outValue))
            {
                outPath = outValue;
            }
            else if (IsFlag(arg, "/quiet", "/q"))
            {
                quiet = true;
            }
            else if (IsFlag(arg, "/help", "/?"))
            {
                help = true;
            }
        }

        if (help)
        {
            Console.WriteLine(Usage);
            return args.Length == 0 || !IsFlag(args[0], "/help", "/?") ? 1 : 0;
        }

        if (projects.Count == 0 && solutions.Count == 0)
        {
            Console.Error.WriteLine("Error: at least one /project or /solution must be specified.");
            Console.WriteLine(Usage);
            return 1;
        }

        if (outPath is null)
        {
            Console.Error.WriteLine("Error: /out must be specified.");
            Console.WriteLine(Usage);
            return 1;
        }

        try
        {
            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterDefaults();
            }

            var metricDatas = await ComputeMetricDatasAsync(projects, solutions, quiet);

            if (!quiet)
            {
                Console.WriteLine($"Writing output to '{outPath}'...");
            }

            using (var writer = new XmlTextWriter(outPath, Encoding.UTF8))
            {
                MetricsOutputWriter.WriteMetricFile(metricDatas, writer);
            }

            if (!quiet)
            {
                Console.WriteLine("Completed Successfully.");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static bool TryGetValue(string arg, string longPrefix, string shortPrefix, out string value)
    {
        if (arg.StartsWith(longPrefix, StringComparison.OrdinalIgnoreCase))
        {
            value = arg[longPrefix.Length..];
            return true;
        }

        if (arg.StartsWith(shortPrefix, StringComparison.OrdinalIgnoreCase))
        {
            value = arg[shortPrefix.Length..];
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool IsFlag(string arg, string longForm, string shortForm) =>
        string.Equals(arg, longForm, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, shortForm, StringComparison.OrdinalIgnoreCase);

    // Mirrors Metrics.Program.GetMetricDatasAsync from the real tool: compute
    // CodeAnalysisMetricData for each project (whether named directly or
    // discovered via a solution), keyed by that project's own file path.
    private static async Task<ImmutableArray<(string, CodeAnalysisMetricData)>> ComputeMetricDatasAsync(
        List<string> projectPaths, List<string> solutionPaths, bool quiet)
    {
        var builder = ImmutableArray.CreateBuilder<(string, CodeAnalysisMetricData)>();
        using var workspace = MSBuildWorkspace.Create();
        workspace.WorkspaceFailed += (_, e) => Console.Error.WriteLine($"Warning: {e.Diagnostic.Message}");

        foreach (var path in projectPaths)
        {
            if (!quiet)
            {
                Console.WriteLine($"Loading {Path.GetFileName(path)}...");
            }

            var project = await workspace.OpenProjectAsync(path);
            await AddProjectMetricDataAsync(project, quiet, builder);
        }

        foreach (var path in solutionPaths)
        {
            if (!quiet)
            {
                Console.WriteLine($"Loading {Path.GetFileName(path)}...");
            }

            var solution = await workspace.OpenSolutionAsync(path);
            foreach (var project in solution.Projects)
            {
                await AddProjectMetricDataAsync(project, quiet, builder);
            }
        }

        return builder.ToImmutable();
    }

    private static async Task AddProjectMetricDataAsync(
        Project project,
        bool quiet,
        ImmutableArray<(string, CodeAnalysisMetricData)>.Builder builder)
    {
        if (!quiet)
        {
            Console.WriteLine($"Computing code metrics for {Path.GetFileName(project.FilePath)}...");
        }

        var compilation = await project.GetCompilationAsync()
            ?? throw new InvalidOperationException($"Unable to compile project '{project.FilePath}'.");
        var data = await CodeAnalysisMetricData.ComputeAsync(
            compilation.Assembly, new CodeMetricsAnalysisContext(compilation, CancellationToken.None));
        builder.Add((project.FilePath!, data));
    }
}
