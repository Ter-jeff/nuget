using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities.Lightup;

internal static class OperationKindEx
{
	public const OperationKind FunctionPointerInvocation = (OperationKind)120;

	public const OperationKind ImplicitIndexerReference = (OperationKind)123;

	public const OperationKind Utf8String = (OperationKind)124;

	public const OperationKind Attribute = (OperationKind)125;

	public const OperationKind CollectionExpression = (OperationKind)127;
}
