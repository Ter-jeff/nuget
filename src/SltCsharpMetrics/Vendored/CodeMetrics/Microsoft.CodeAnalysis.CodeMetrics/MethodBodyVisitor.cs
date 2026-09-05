using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.CodeAnalysis.CodeMetrics;

/// <summary>
/// Utility for visiting the body of a method
/// </summary>
internal class MethodBodyVisitor : CSharpSyntaxWalker
{
	private readonly CodeMetricsAnalysisContext _context;

	private readonly IFieldSymbol _targetField;

	private bool _fieldReferenced;

	private MethodBodyVisitor(CodeMetricsAnalysisContext context, IFieldSymbol targetField)
	{
		_context = context;
		_targetField = targetField;
		_fieldReferenced = false;
	}

	/// <summary>
	/// Check if a field is accessed in the body of a method
	/// </summary>
	public static bool IsFieldAccessedByMethod(CodeMetricsAnalysisContext context, IFieldSymbol field, IMethodSymbol method)
	{
		SyntaxNode syntaxNode = method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
		if (syntaxNode == null)
		{
			return false;
		}
		MethodBodyVisitor methodBodyVisitor = new MethodBodyVisitor(context, field);
		methodBodyVisitor.Visit(syntaxNode);
		return methodBodyVisitor._fieldReferenced;
	}

	public override void VisitIdentifierName(IdentifierNameSyntax node)
	{
		ISymbol symbol = _context.GetSemanticModel(node).GetSymbolInfo(node).Symbol;
		if (symbol != null && symbol.Equals(_targetField, SymbolEqualityComparer.Default))
		{
			_fieldReferenced = true;
		}
		base.VisitIdentifierName(node);
	}
}
