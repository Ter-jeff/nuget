using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities.Extensions;

internal static class OperationKinds
{
	public static ImmutableArray<OperationKind> MemberReference { get; } = ImmutableArray.Create(OperationKind.EventReference, OperationKind.FieldReference, OperationKind.MethodReference, OperationKind.PropertyReference);

}
