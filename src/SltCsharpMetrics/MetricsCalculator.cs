using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SltCsharpMetrics;

public sealed record MemberMetrics(
    int MaintainabilityIndex,
    int CyclomaticComplexity,
    int ClassCoupling,
    int SourceLines,
    int ExecutableLines);

public sealed record TypeMetrics(MemberMetrics Metrics, int DepthOfInheritance);

public static class MetricsCalculator
{
    private static readonly HashSet<SyntaxKind> DecisionKinds = new()
    {
        SyntaxKind.IfStatement,
        SyntaxKind.ForStatement,
        SyntaxKind.ForEachStatement,
        SyntaxKind.ForEachVariableStatement,
        SyntaxKind.WhileStatement,
        SyntaxKind.DoStatement,
        SyntaxKind.CaseSwitchLabel,
        SyntaxKind.CasePatternSwitchLabel,
        SyntaxKind.CatchClause,
        SyntaxKind.ConditionalExpression,
        SyntaxKind.CoalesceExpression,
        SyntaxKind.LogicalAndExpression,
        SyntaxKind.LogicalOrExpression,
        SyntaxKind.SwitchExpressionArm,
    };

    public static int ComputeCyclomaticComplexity(SyntaxNode node)
    {
        var complexity = 1;
        foreach (var descendant in node.DescendantNodesAndSelf())
        {
            if (DecisionKinds.Contains(descendant.Kind()))
            {
                complexity++;
            }
        }

        return complexity;
    }

    public static int ComputeExecutableLines(SyntaxNode node)
    {
        var lines = new HashSet<int>();
        foreach (var statement in node.DescendantNodesAndSelf().OfType<StatementSyntax>())
        {
            if (statement is BlockSyntax)
            {
                continue;
            }

            lines.Add(statement.GetLocation().GetLineSpan().StartLinePosition.Line);
        }

        return lines.Count;
    }

    public static int ComputeSourceLines(SyntaxNode node)
    {
        var span = node.GetLocation().GetLineSpan();
        return span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
    }

    public static int ComputeDepthOfInheritance(ITypeSymbol? type)
    {
        var depth = 0;
        var current = type?.BaseType;
        while (current is not null)
        {
            depth++;
            current = current.BaseType;
        }

        return depth;
    }

    private static readonly HashSet<SpecialType> PrimitiveSpecialTypes = new()
    {
        SpecialType.System_Boolean, SpecialType.System_Byte, SpecialType.System_SByte,
        SpecialType.System_Char, SpecialType.System_Decimal, SpecialType.System_Double,
        SpecialType.System_Single, SpecialType.System_Int16, SpecialType.System_Int32,
        SpecialType.System_Int64, SpecialType.System_UInt16, SpecialType.System_UInt32,
        SpecialType.System_UInt64, SpecialType.System_String, SpecialType.System_Object,
        SpecialType.System_Void,
    };

    public static HashSet<INamedTypeSymbol> ComputeCoupledTypes(SemanticModel model, SyntaxNode node, INamedTypeSymbol self)
    {
        var coupled = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var identifier in node.DescendantNodesAndSelf().OfType<SimpleNameSyntax>())
        {
            var symbol = model.GetSymbolInfo(identifier).Symbol;
            var (type, isDataMember) = symbol switch
            {
                INamedTypeSymbol t => (t, false),
                IMethodSymbol m => (m.ContainingType, false),
                IFieldSymbol f => (f.ContainingType, true),
                IPropertySymbol p => (p.ContainingType, true),
                _ => (null, false),
            };

            if (type is null || PrimitiveSpecialTypes.Contains(type.SpecialType))
            {
                continue;
            }

            // Using an inherited field/property is using your own state; calling an inherited
            // method still counts as coupling to the type that defines the behavior.
            var excluded = isDataMember
                ? IsSelfOrAncestor(type, self)
                : SymbolEqualityComparer.Default.Equals(type, self);
            if (excluded)
            {
                continue;
            }

            coupled.Add(type);
        }

        return coupled;
    }

    private static bool IsSelfOrAncestor(INamedTypeSymbol candidate, INamedTypeSymbol self)
    {
        for (var current = self; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, candidate))
            {
                return true;
            }
        }

        return false;
    }

    public static int ComputeMaintainabilityIndex(SyntaxNode node, int cyclomaticComplexity, int executableLines)
    {
        var operators = 0;
        var operands = 0;
        var distinctOperators = new HashSet<string>();
        var distinctOperands = new HashSet<string>();

        foreach (var token in node.DescendantTokens())
        {
            var text = token.Text;
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            if (token.IsKind(SyntaxKind.IdentifierToken) || token.IsKeyword() ||
                token.Kind() is SyntaxKind.NumericLiteralToken or SyntaxKind.StringLiteralToken or SyntaxKind.CharacterLiteralToken)
            {
                operands++;
                distinctOperands.Add(text);
            }
            else if (SyntaxFacts.IsPunctuation(token.Kind()))
            {
                operators++;
                distinctOperators.Add(text);
            }
        }

        var vocabulary = distinctOperators.Count + distinctOperands.Count;
        var length = operators + operands;
        var volume = vocabulary <= 1 || length == 0 ? 0.0 : length * Math.Log2(vocabulary);
        var loc = Math.Max(executableLines, 1);

        var raw = volume <= 0
            ? 171 - 0.23 * cyclomaticComplexity - 16.2 * Math.Log(loc)
            : 171 - 5.2 * Math.Log(volume) - 0.23 * cyclomaticComplexity - 16.2 * Math.Log(loc);

        var mi = Math.Max(0.0, raw * 100.0 / 171.0);
        return (int)Math.Round(Math.Min(mi, 100.0));
    }
}
