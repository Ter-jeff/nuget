using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Simplification;

namespace Analyzer.Utilities;

internal static class SyntaxNodeExtensions
{
	private static Optional<SyntaxAnnotation?> s_addImportsAnnotation;

	private static SyntaxAnnotation? AddImportsAnnotation
	{
		get
		{
			if (!s_addImportsAnnotation.HasValue)
			{
				s_addImportsAnnotation = typeof(Simplifier).GetTypeInfo().GetDeclaredProperty("AddImportsAnnotation")?.GetValue(null) as SyntaxAnnotation;
			}
			return s_addImportsAnnotation.Value;
		}
	}

	/// <summary>
	/// Annotates a syntax node representing a type so that any missing imports get automatically added. Does not work in any other kinds of nodes.
	/// </summary>
	/// <param name="syntaxNode">The type node to annotate.</param>
	/// <returns>The annotated type node.</returns>
	public static SyntaxNode WithAddImportsAnnotation(this SyntaxNode syntaxNode)
	{
		if ((object)AddImportsAnnotation == null)
		{
			return syntaxNode;
		}
		return syntaxNode.WithAdditionalAnnotations(AddImportsAnnotation);
	}
}
