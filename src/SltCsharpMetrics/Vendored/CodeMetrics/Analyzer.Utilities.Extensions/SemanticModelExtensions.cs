using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities.Extensions;

internal static class SemanticModelExtensions
{
	public static IOperation? GetOperationWalkingUpParentChain(this SemanticModel semanticModel, SyntaxNode node, CancellationToken cancellationToken)
	{
		do
		{
			IOperation operation = semanticModel.GetOperation(node, cancellationToken);
			if (operation != null)
			{
				return operation;
			}
			node = node.Parent;
		}
		while (node != null);
		return null;
	}
}
