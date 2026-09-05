using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities;

internal static class DocumentExtensions
{
	public static async ValueTask<SemanticModel> GetRequiredSemanticModelAsync(this Document document, CancellationToken cancellationToken)
	{
		if (document.TryGetSemanticModel(out SemanticModel semanticModel))
		{
			return semanticModel;
		}
		return (await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false)) ?? throw new InvalidOperationException("SyntaxTree is required to accomplish the task but is not supported by document");
	}

	public static async ValueTask<SyntaxTree> GetRequiredSyntaxTreeAsync(this Document document, CancellationToken cancellationToken)
	{
		if (document.TryGetSyntaxTree(out SyntaxTree syntaxTree))
		{
			return syntaxTree;
		}
		return (await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false)) ?? throw new InvalidOperationException("SyntaxTree is required to accomplish the task but is not supported by document");
	}

	public static async ValueTask<SyntaxNode> GetRequiredSyntaxRootAsync(this Document document, CancellationToken cancellationToken)
	{
		if (document.TryGetSyntaxRoot(out SyntaxNode root))
		{
			return root;
		}
		return (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false)) ?? throw new InvalidOperationException("SyntaxTree is required to accomplish the task but is not supported by document");
	}
}
