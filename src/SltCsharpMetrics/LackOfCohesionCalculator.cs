using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SltCsharpMetrics;

// Reimplements CodeAnalysisMetricData.NamedTypeMetricData.CalculateLackOfCohesionOfMethods
// from Microsoft's dotnet/roslyn-analyzers "Metrics" tool (MIT licensed; see
// Vendored/README.md) -- the Microsoft.CodeAnalysis.AnalyzerUtilities package version
// referenced here predates that member being exposed publicly.
internal static class LackOfCohesionCalculator
{
    public static double Calculate(INamedTypeSymbol namedType, Compilation compilation)
    {
        var members = namedType.GetMembers();
        var fields = members.OfType<IFieldSymbol>()
            .Where(f => f.AssociatedSymbol is not IPropertySymbol
                && !f.IsConst
                && (!f.IsStatic || !f.IsReadOnly)
                && f.DeclaredAccessibility != Accessibility.Public)
            .ToArray();
        var methods = members.OfType<IMethodSymbol>()
            .Where(m => !IsImplicitConstructor(m) && !IsAutoPropertyAccessor(m))
            .ToArray();

        if (fields.Length == 0 || methods.Length == 0)
        {
            return double.NaN;
        }

        double accessCount = 0;
        foreach (var field in fields)
        {
            foreach (var method in methods)
            {
                if (IsFieldAccessedByMethod(compilation, field, method))
                {
                    accessCount += 1.0;
                }
            }
        }

        return 1.0 - accessCount / (fields.Length * methods.Length);
    }

    private static bool IsImplicitConstructor(IMethodSymbol method) =>
        method.MethodKind == MethodKind.Constructor && method.IsImplicitlyDeclared;

    private static bool IsAutoPropertyAccessor(IMethodSymbol method)
    {
        if (method.AssociatedSymbol is not IPropertySymbol)
        {
            return false;
        }

        var syntax = method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
        return syntax is AccessorDeclarationSyntax { Body: null, ExpressionBody: null };
    }

    private static bool IsFieldAccessedByMethod(Compilation compilation, IFieldSymbol field, IMethodSymbol method)
    {
        var syntax = method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
        if (syntax is null)
        {
            return false;
        }

        var model = compilation.GetSemanticModel(syntax.SyntaxTree);
        foreach (var identifier in syntax.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier).Symbol, field))
            {
                return true;
            }
        }

        return false;
    }
}
