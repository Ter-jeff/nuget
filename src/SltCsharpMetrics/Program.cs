using System.Xml.Linq;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace SltCsharpMetrics;

public static class Program
{
    private static readonly SymbolDisplayFormat ShortTypeFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

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

            var report = await BuildReportAsync(projects, solutions, quiet);
            if (!quiet)
            {
                Console.WriteLine($"Writing output to '{outPath}'...");
            }

            report.Save(outPath);

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

    private static async Task<XDocument> BuildReportAsync(List<string> projectPaths, List<string> solutionPaths, bool quiet)
    {
        using var workspace = MSBuildWorkspace.Create();
        workspace.WorkspaceFailed += (_, e) => Console.Error.WriteLine($"Warning: {e.Diagnostic.Message}");

        var projects = new List<Project>();
        foreach (var path in projectPaths)
        {
            if (!quiet)
            {
                Console.WriteLine($"Loading {Path.GetFileName(path)}...");
            }

            projects.Add(await workspace.OpenProjectAsync(path));
        }

        foreach (var path in solutionPaths)
        {
            if (!quiet)
            {
                Console.WriteLine($"Loading {Path.GetFileName(path)}...");
            }

            var solution = await workspace.OpenSolutionAsync(path);
            projects.AddRange(solution.Projects);
        }

        var targets = new List<XElement>();
        foreach (var project in projects)
        {
            if (!quiet)
            {
                Console.WriteLine($"Computing code metrics for {Path.GetFileName(project.FilePath)}...");
            }

            targets.Add(await BuildTargetAsync(project));
        }

        return new XDocument(
            new XElement("CodeMetricsReport",
                new XAttribute("Version", "1.0"),
                new XElement("Targets", targets)));
    }

    private static string BuildDisplayName(INamedTypeSymbol symbol)
    {
        var names = new List<string>();
        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            names.Insert(0, current.ToDisplayString(ShortTypeFormat));
        }

        return string.Join(".", names);
    }

    private static async Task<XElement> BuildTargetAsync(Project project)
    {
        var compilation = await project.GetCompilationAsync()
            ?? throw new InvalidOperationException($"Unable to compile project '{project.FilePath}'.");

        var typesByNamespace = new Dictionary<string, List<(INamedTypeSymbol Symbol, SyntaxNode Syntax, SemanticModel Model, string DisplayName)>>();

        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            var root = await tree.GetRootAsync();
            foreach (var typeDecl in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            {
                if (typeDecl is not (TypeDeclarationSyntax or EnumDeclarationSyntax))
                {
                    continue;
                }

                if (model.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol symbol)
                {
                    continue;
                }

                var ns = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
                if (!typesByNamespace.TryGetValue(ns, out var list))
                {
                    typesByNamespace[ns] = list = new List<(INamedTypeSymbol, SyntaxNode, SemanticModel, string)>();
                }

                list.Add((symbol, typeDecl, model, BuildDisplayName(symbol)));
            }
        }

        var namespaceElements = new List<XElement>();
        var assemblyAgg = new Aggregate();

        foreach (var (ns, types) in typesByNamespace)
        {
            var namespaceAgg = new Aggregate();
            var typeElements = new List<XElement>();

            foreach (var (symbol, syntax, model, displayName) in types)
            {
                var typeElement = syntax is EnumDeclarationSyntax enumSyntax
                    ? BuildEnumElement(symbol, enumSyntax, model, displayName)
                    : BuildTypeElement(symbol, (TypeDeclarationSyntax)syntax, model, displayName);
                typeElements.Add(typeElement);
                namespaceAgg.Add(ReadMetric(typeElement, "MaintainabilityIndex"), ReadMetric(typeElement, "CyclomaticComplexity"),
                    ReadMetric(typeElement, "ClassCoupling"), ReadMetric(typeElement, "SourceLines"), ReadMetric(typeElement, "ExecutableLines"));
            }

            var namespaceElement = new XElement("Namespace",
                new XAttribute("Name", string.IsNullOrEmpty(ns) ? "<global namespace>" : ns),
                BuildMetricsElement(namespaceAgg),
                new XElement("Types", typeElements));

            namespaceElements.Add(namespaceElement);
            assemblyAgg.Add(namespaceAgg.MaintainabilityIndex, namespaceAgg.CyclomaticComplexity,
                namespaceAgg.ClassCoupling, namespaceAgg.SourceLines, namespaceAgg.ExecutableLines);
        }

        var assemblyElement = new XElement("Assembly",
            new XAttribute("Name", compilation.Assembly.Identity.ToString()),
            BuildMetricsElement(assemblyAgg),
            new XElement("Namespaces", namespaceElements));

        return new XElement("Target", new XAttribute("Name", Path.GetFileName(project.FilePath) ?? project.Name), assemblyElement);
    }

    private static XElement BuildTypeElement(INamedTypeSymbol symbol, TypeDeclarationSyntax syntax, SemanticModel model, string displayName)
    {
        var memberElements = new List<XElement>();
        var typeAgg = new Aggregate();
        var hasMethod = false;

        foreach (var member in syntax.Members)
        {
            if (member is FieldDeclarationSyntax fieldDecl)
            {
                foreach (var variable in fieldDecl.Declaration.Variables)
                {
                    if (model.GetDeclaredSymbol(variable) is not IFieldSymbol fieldSymbol)
                    {
                        continue;
                    }

                    var fieldCoupled = MetricsCalculator.ComputeCoupledTypes(model, fieldDecl, symbol);
                    const int cc = 0;
                    var executableLines = variable.Initializer is not null ? 1 : 0;
                    var sourceLines = MetricsCalculator.ComputeSourceLines(fieldDecl);
                    var mi = MetricsCalculator.ComputeMaintainabilityIndex(fieldDecl, cc, executableLines);

                    memberElements.Add(new XElement("Field",
                        new XAttribute("Name", $"{fieldSymbol.Type.ToDisplayString(ShortTypeFormat)} {symbol.Name}.{fieldSymbol.Name}"),
                        new XAttribute("File", syntax.SyntaxTree.FilePath),
                        new XAttribute("Line", variable.GetLocation().GetLineSpan().StartLinePosition.Line + 1),
                        new XAttribute("Private", fieldSymbol.DeclaredAccessibility == Accessibility.Private),
                        new XAttribute("Constant", fieldSymbol.IsConst),
                        new XElement("Metrics",
                            Metric("MaintainabilityIndex", mi),
                            Metric("CyclomaticComplexity", cc),
                            Metric("ClassCoupling", fieldCoupled.Count),
                            Metric("SourceLines", sourceLines),
                            Metric("ExecutableLines", executableLines))));

                    typeAgg.Add(mi, cc, fieldCoupled.Count, sourceLines, executableLines);
                }

                continue;
            }

            if (member is BaseMethodDeclarationSyntax methodDecl)
            {
                if (methodDecl.Body is null && methodDecl.ExpressionBody is null)
                {
                    continue;
                }

                if (model.GetDeclaredSymbol(methodDecl) is not IMethodSymbol methodSymbol)
                {
                    continue;
                }

                hasMethod = true;
                var cc = MetricsCalculator.ComputeCyclomaticComplexity(methodDecl);
                var executableLines = MetricsCalculator.ComputeExecutableLines(methodDecl);
                var sourceLines = MetricsCalculator.ComputeSourceLines(methodDecl);
                var methodCoupled = MetricsCalculator.ComputeCoupledTypes(model, methodDecl, symbol);
                var mi = MetricsCalculator.ComputeMaintainabilityIndex(methodDecl, cc, executableLines);

                memberElements.Add(new XElement("Method",
                    new XAttribute("Name", DescribeMethod(methodSymbol, symbol)),
                    new XAttribute("File", syntax.SyntaxTree.FilePath),
                    new XAttribute("Line", methodDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1),
                    new XAttribute("Private", methodSymbol.DeclaredAccessibility == Accessibility.Private),
                    new XElement("Metrics",
                        Metric("MaintainabilityIndex", mi),
                        Metric("CyclomaticComplexity", cc),
                        Metric("ClassCoupling", methodCoupled.Count),
                        Metric("SourceLines", sourceLines),
                        Metric("ExecutableLines", executableLines))));

                typeAgg.Add(mi, cc, methodCoupled.Count, sourceLines, executableLines);
            }
        }

        // Computed once over the whole type (not unioned from per-member scans) so that
        // attributes and the base-type list -- which no single member's syntax covers -- count too.
        var coupled = MetricsCalculator.ComputeCoupledTypes(model, syntax, symbol);
        var projectCoupled = coupled
            .Where(t => t.DeclaringSyntaxReferences.Length > 0)
            .OrderBy(t => t.ToDisplayString(), StringComparer.Ordinal)
            .ToList();

        var typeSourceLines = MetricsCalculator.ComputeSourceLines(syntax);
        // A type with no methods (a plain data holder) reports a fixed CC/ExecutableLines
        // baseline instead of summing its fields' own metrics.
        var typeExecutableLines = hasMethod ? typeAgg.ExecutableLines : 2;
        var typeCc = hasMethod ? typeAgg.CyclomaticComplexity : 1;
        var typeMi = MetricsCalculator.ComputeMaintainabilityIndex(syntax, typeCc, typeExecutableLines);
        var depthOfInheritance = MetricsCalculator.ComputeDepthOfInheritance(symbol);

        return new XElement("NamedType",
            new XAttribute("Name", displayName),
            new XAttribute("File", syntax.SyntaxTree.FilePath),
            new XAttribute("Line", syntax.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1),
            new XElement("Metrics",
                Metric("MaintainabilityIndex", typeMi),
                Metric("CyclomaticComplexity", typeCc),
                Metric("ClassCoupling", coupled.Count),
                Metric("DepthOfInheritance", depthOfInheritance),
                Metric("LackOfCohesionOfMethods", "NaN"),
                Metric("ProjectClassCoupling", projectCoupled.Count),
                Metric("ProjectClassCouplingList", string.Join("%", projectCoupled.Select(t => t.ToDisplayString()))),
                Metric("SourceLines", typeSourceLines),
                Metric("ExecutableLines", typeExecutableLines)),
            new XElement("Members", memberElements));
    }

    private static XElement BuildEnumElement(INamedTypeSymbol symbol, EnumDeclarationSyntax syntax, SemanticModel model, string displayName)
    {
        var memberElements = new List<XElement>();

        foreach (var member in syntax.Members)
        {
            if (model.GetDeclaredSymbol(member) is not IFieldSymbol fieldSymbol)
            {
                continue;
            }

            const int cc = 0;
            var executableLines = member.EqualsValue is not null ? 1 : 0;
            var sourceLines = MetricsCalculator.ComputeSourceLines(member);
            var memberCoupled = MetricsCalculator.ComputeCoupledTypes(model, member, symbol);
            var mi = MetricsCalculator.ComputeMaintainabilityIndex(member, cc, executableLines);

            memberElements.Add(new XElement("Field",
                new XAttribute("Name", $"{displayName}.{fieldSymbol.Name}"),
                new XAttribute("File", syntax.SyntaxTree.FilePath),
                new XAttribute("Line", member.GetLocation().GetLineSpan().StartLinePosition.Line + 1),
                new XAttribute("Private", fieldSymbol.DeclaredAccessibility == Accessibility.Private),
                new XAttribute("Constant", true),
                new XElement("Metrics",
                    Metric("MaintainabilityIndex", mi),
                    Metric("CyclomaticComplexity", cc),
                    Metric("ClassCoupling", memberCoupled.Count),
                    Metric("SourceLines", sourceLines),
                    Metric("ExecutableLines", executableLines))));
        }

        var coupled = MetricsCalculator.ComputeCoupledTypes(model, syntax, symbol);
        var projectCoupled = coupled
            .Where(t => t.DeclaringSyntaxReferences.Length > 0)
            .OrderBy(t => t.ToDisplayString(), StringComparer.Ordinal)
            .ToList();

        var typeSourceLines = MetricsCalculator.ComputeSourceLines(syntax);
        const int typeExecutableLines = 2;
        const int typeCc = 1;
        var typeMi = MetricsCalculator.ComputeMaintainabilityIndex(syntax, typeCc, typeExecutableLines);

        return new XElement("NamedType",
            new XAttribute("Name", displayName),
            new XAttribute("File", syntax.SyntaxTree.FilePath),
            new XAttribute("Line", syntax.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1),
            new XElement("Metrics",
                Metric("MaintainabilityIndex", typeMi),
                Metric("CyclomaticComplexity", typeCc),
                Metric("ClassCoupling", coupled.Count),
                Metric("DepthOfInheritance", 1),
                Metric("LackOfCohesionOfMethods", "NaN"),
                Metric("ProjectClassCoupling", projectCoupled.Count),
                Metric("ProjectClassCouplingList", string.Join("%", projectCoupled.Select(t => t.ToDisplayString()))),
                Metric("SourceLines", typeSourceLines),
                Metric("ExecutableLines", typeExecutableLines)),
            new XElement("Members", memberElements));
    }

    private static string DescribeMethod(IMethodSymbol method, INamedTypeSymbol containingType)
    {
        var parameters = string.Join(", ", method.Parameters.Select(FormatParameter));
        if (method.MethodKind == MethodKind.Constructor)
        {
            return $"{containingType.Name}.{containingType.Name}({parameters})";
        }

        return $"{method.ReturnType.ToDisplayString(ShortTypeFormat)} {containingType.Name}.{method.Name}({parameters})";
    }

    private static string FormatParameter(IParameterSymbol parameter)
    {
        var modifier = parameter.RefKind switch
        {
            RefKind.Out => "out ",
            RefKind.Ref => "ref ",
            RefKind.In => "in ",
            _ => "",
        };
        var paramsPrefix = parameter.IsParams ? "params " : "";
        return $"{modifier}{paramsPrefix}{parameter.Type.ToDisplayString(ShortTypeFormat)} {parameter.Name}";
    }

    private static XElement BuildMetricsElement(Aggregate agg) => new(
        "Metrics",
        Metric("MaintainabilityIndex", agg.MaintainabilityIndex),
        Metric("CyclomaticComplexity", agg.CyclomaticComplexity),
        Metric("ClassCoupling", agg.ClassCoupling),
        Metric("DepthOfInheritance", 1),
        Metric("SourceLines", agg.SourceLines),
        Metric("ExecutableLines", agg.ExecutableLines));

    private static XElement Metric(string name, object value) =>
        new("Metric", new XAttribute("Name", name), new XAttribute("Value", value));

    private static int ReadMetric(XElement typeElement, string name) =>
        int.Parse(typeElement.Element("Metrics")!.Elements("Metric").First(e => (string)e.Attribute("Name")! == name).Attribute("Value")!.Value);

    private sealed class Aggregate
    {
        private readonly List<int> _mi = new();
        public int CyclomaticComplexity { get; private set; }
        public int ClassCoupling { get; private set; }
        public int SourceLines { get; private set; }
        public int ExecutableLines { get; private set; }
        public int MaintainabilityIndex => _mi.Count == 0 ? 100 : (int)Math.Round(_mi.Average());

        public void Add(int mi, int cc, int classCoupling, int sourceLines, int executableLines)
        {
            _mi.Add(mi);
            CyclomaticComplexity += cc;
            ClassCoupling = Math.Max(ClassCoupling, classCoupling);
            SourceLines += sourceLines;
            ExecutableLines += executableLines;
        }
    }
}
