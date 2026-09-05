using System.Collections.Concurrent;
using Analyzer.Utilities;

namespace Microsoft.CodeAnalysis.CodeMetrics;

public sealed class CodeMetricsAnalysisContext
{
	private readonly ConcurrentDictionary<SyntaxTree, SemanticModel> _semanticModelMap;

	private readonly Func<SyntaxTree, SemanticModel> _getSemanticModel;

	public Compilation Compilation { get; }

	public WellKnownTypeProvider WellKnownTypeProvider { get; }

	public CancellationToken CancellationToken { get; }

	public Func<INamedTypeSymbol, bool> IsExcludedFromInheritanceCountFunc { get; }

	public CodeMetricsAnalysisContext(Compilation compilation, CancellationToken cancellationToken, Func<INamedTypeSymbol, bool>? isExcludedFromInheritanceCountFunc = null)
	{
		Compilation = compilation;
		WellKnownTypeProvider = Analyzer.Utilities.WellKnownTypeProvider.GetOrCreate(compilation);
		CancellationToken = cancellationToken;
		_semanticModelMap = new ConcurrentDictionary<SyntaxTree, SemanticModel>();
		IsExcludedFromInheritanceCountFunc = isExcludedFromInheritanceCountFunc ?? ((Func<INamedTypeSymbol, bool>)((INamedTypeSymbol x) => false));
		_getSemanticModel = (SyntaxTree tree) => Compilation.GetSemanticModel(tree);
	}

	internal SemanticModel GetSemanticModel(SyntaxNode node)
	{
		return _semanticModelMap.GetOrAdd(node.SyntaxTree, _getSemanticModel);
	}
}
