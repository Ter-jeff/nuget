using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Analyzer.Utilities.Extensions;

internal static class OperationBlockAnalysisContextExtension
{
	public static bool IsMethodNotImplementedOrSupported(this OperationBlockStartAnalysisContext context, bool checkPlatformNotSupported = false)
	{
		ImmutableArray<IOperation> immutableArray = context.OperationBlocks.WhereAsArray((IOperation operation) => !operation.IsOperationNoneRoot());
		IBlockOperation blockOperation = null;
		if (immutableArray.Length == 1 && immutableArray[0].Kind == OperationKind.Block)
		{
			blockOperation = (IBlockOperation)immutableArray[0];
		}
		else if (immutableArray.Length > 1)
		{
			ImmutableArray<IOperation>.Enumerator enumerator = immutableArray.GetEnumerator();
			while (enumerator.MoveNext())
			{
				IOperation current = enumerator.Current;
				if (current.Kind == OperationKind.Block)
				{
					blockOperation = (IBlockOperation)current;
					break;
				}
			}
		}
		if (blockOperation != null && IsSingleStatementBody(blockOperation))
		{
			ImmutableArray<IOperation> topmostExplicitDescendants = blockOperation.Operations[0].GetTopmostExplicitDescendants();
			if (topmostExplicitDescendants.Length == 1 && topmostExplicitDescendants[0] is IThrowOperation operation2)
			{
				ITypeSymbol thrownExceptionType = operation2.GetThrownExceptionType();
				if (thrownExceptionType != null && (SymbolEqualityComparer.Default.Equals(context.Compilation.GetOrCreateTypeByMetadataName("System.NotImplementedException"), thrownExceptionType.OriginalDefinition) || SymbolEqualityComparer.Default.Equals(context.Compilation.GetOrCreateTypeByMetadataName("System.NotSupportedException"), thrownExceptionType.OriginalDefinition) || (checkPlatformNotSupported && SymbolEqualityComparer.Default.Equals(context.Compilation.GetOrCreateTypeByMetadataName("System.PlatformNotSupportedException"), thrownExceptionType.OriginalDefinition))))
				{
					return true;
				}
			}
		}
		return false;
		static bool IsSingleStatementBody(IBlockOperation body)
		{
			if (body.Operations.Length != 1)
			{
				if (body.Operations.Length == 3 && body.Syntax.Language == "Visual Basic" && body.Operations[1] is ILabeledOperation { IsImplicit: not false } && body.Operations[2] is IReturnOperation returnOperation)
				{
					return returnOperation.IsImplicit;
				}
				return false;
			}
			return true;
		}
	}
}
