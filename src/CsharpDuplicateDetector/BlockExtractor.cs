using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CsharpDuplicateDetector;

public static class BlockExtractor
{
    public static IEnumerable<CodeBlock> ExtractBlocks(string filePath, string displayPath, string blockSplit, bool onlyIdentifiers)
    {
        var text = File.ReadAllText(filePath);
        var tree = CSharpSyntaxTree.ParseText(text, path: filePath);
        var root = tree.GetRoot();

        return blockSplit switch
        {
            "file" => ExtractFileBlock(root, displayPath, onlyIdentifiers),
            "class" => ExtractClassBlocks(root, displayPath, onlyIdentifiers),
            _ => ExtractMethodBlocks(root, displayPath, onlyIdentifiers),
        };
    }

    private static IEnumerable<CodeBlock> ExtractFileBlock(SyntaxNode root, string displayPath, bool onlyIdentifiers)
    {
        yield return new CodeBlock
        {
            File = displayPath,
            Line = 1,
            Tokens = ExtractTokens(root, onlyIdentifiers),
        };
    }

    private static IEnumerable<CodeBlock> ExtractClassBlocks(SyntaxNode root, string displayPath, bool onlyIdentifiers)
    {
        foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            yield return new CodeBlock
            {
                File = displayPath,
                ClassName = typeDecl.Identifier.Text,
                Line = LineOf(typeDecl.Identifier),
                Tokens = ExtractTokens(typeDecl, onlyIdentifiers),
            };
        }
    }

    private static IEnumerable<CodeBlock> ExtractMethodBlocks(SyntaxNode root, string displayPath, bool onlyIdentifiers)
    {
        foreach (var methodDecl in root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
        {
            if (methodDecl.Body is null && methodDecl.ExpressionBody is null)
            {
                continue;
            }

            var containingType = methodDecl.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
            yield return new CodeBlock
            {
                File = displayPath,
                ClassName = containingType?.Identifier.Text,
                MethodName = DescribeMethod(methodDecl),
                Line = LineOf(methodDecl.GetFirstToken()),
                Tokens = ExtractTokens(methodDecl, onlyIdentifiers),
            };
        }
    }

    private static int LineOf(SyntaxToken token) => token.GetLocation().GetLineSpan().StartLinePosition.Line;

    private static List<string> ExtractTokens(SyntaxNode node, bool onlyIdentifiers)
    {
        var tokens = node.DescendantTokens().Where(t => !t.IsKind(SyntaxKind.None));
        if (onlyIdentifiers)
        {
            tokens = tokens.Where(t => t.IsKind(SyntaxKind.IdentifierToken));
        }

        return tokens.Select(t => t.Text).ToList();
    }

    private static string DescribeMethod(BaseMethodDeclarationSyntax methodDecl) => methodDecl switch
    {
        MethodDeclarationSyntax m => m.Identifier.Text,
        ConstructorDeclarationSyntax c => c.Identifier.Text,
        DestructorDeclarationSyntax d => $"~{d.Identifier.Text}",
        OperatorDeclarationSyntax o => o.OperatorToken.Text,
        _ => methodDecl.ToString().Split('\n')[0].Trim(),
    };
}
