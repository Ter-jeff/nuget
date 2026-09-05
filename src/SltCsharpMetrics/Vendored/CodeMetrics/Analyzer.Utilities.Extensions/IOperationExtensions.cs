using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Analyzer.Utilities.Extensions;

internal static class IOperationExtensions
{
	/// <summary>
	/// PERF: Cache from operation roots to their corresponding <see cref="T:Microsoft.CodeAnalysis.FlowAnalysis.ControlFlowGraph" /> to enable interprocedural flow analysis
	/// across analyzers and analyzer callbacks to re-use the control flow graph.
	/// </summary>
	/// <remarks>Also see <see cref="F:Analyzer.Utilities.Extensions.IMethodSymbolExtensions.s_methodToTopmostOperationBlockCache" /></remarks>
	private static readonly BoundedCache<Compilation, ConcurrentDictionary<IOperation, ControlFlowGraph?>> s_operationToCfgCache = new BoundedCache<Compilation, ConcurrentDictionary<IOperation, ControlFlowGraph>>();

	private static readonly ImmutableArray<OperationKind> s_LambdaAndLocalFunctionKinds = ImmutableArray.Create(OperationKind.AnonymousFunction, OperationKind.LocalFunction);

	/// <summary>
	/// Gets the receiver type for an invocation expression (i.e. type of 'A' in invocation 'A.B()')
	/// If the invocation actually involves a conversion from A to some other type, say 'C', on which B is invoked,
	/// then this method returns type A if <paramref name="beforeConversion" /> is true, and C if false.
	/// </summary>
	public static ITypeSymbol? GetReceiverType(this IInvocationOperation invocation, Compilation compilation, bool beforeConversion, CancellationToken cancellationToken)
	{
		if (invocation.Instance != null)
		{
			if (!beforeConversion)
			{
				return invocation.Instance.Type;
			}
			return GetReceiverType(invocation.Instance.Syntax, compilation, cancellationToken);
		}
		if (invocation.TargetMethod.IsExtensionMethod && !invocation.TargetMethod.Parameters.IsEmpty)
		{
			IArgumentOperation argumentOperation = invocation.Arguments.FirstOrDefault();
			if (argumentOperation != null)
			{
				if (!beforeConversion)
				{
					return argumentOperation.Value.Type;
				}
				return GetReceiverType(argumentOperation.Value.Syntax, compilation, cancellationToken);
			}
			if (invocation.TargetMethod.Parameters[0].IsParams)
			{
				return invocation.TargetMethod.Parameters[0].Type;
			}
		}
		return null;
	}

	private static ITypeSymbol? GetReceiverType(SyntaxNode receiverSyntax, Compilation compilation, CancellationToken cancellationToken)
	{
		return compilation.GetSemanticModel(receiverSyntax.SyntaxTree).GetTypeInfo(receiverSyntax, cancellationToken).Type;
	}

	public static bool HasNullConstantValue(this IOperation operation)
	{
		if (operation.ConstantValue.HasValue)
		{
			return operation.ConstantValue.Value == null;
		}
		return false;
	}

	public static bool TryGetBoolConstantValue(this IOperation operation, out bool constantValue)
	{
		if (operation.ConstantValue.HasValue && operation.ConstantValue.Value is bool flag)
		{
			constantValue = flag;
			return true;
		}
		constantValue = false;
		return false;
	}

	public static bool HasConstantValue(this IOperation operation, long comparand)
	{
		return operation.HasConstantValue((ulong)comparand);
	}

	public static bool HasConstantValue(this IOperation operation, ulong comparand)
	{
		Optional<object> constantValue = operation.ConstantValue;
		if (!constantValue.HasValue)
		{
			return false;
		}
		if (operation.Type == null || operation.Type.IsErrorType())
		{
			return false;
		}
		if (operation.Type.IsPrimitiveType())
		{
			return HasConstantValue(constantValue, operation.Type, comparand);
		}
		if (operation.Type.TypeKind == TypeKind.Enum)
		{
			INamedTypeSymbol enumUnderlyingType = ((INamedTypeSymbol)operation.Type).EnumUnderlyingType;
			if (enumUnderlyingType != null && enumUnderlyingType.IsPrimitiveType())
			{
				return HasConstantValue(constantValue, enumUnderlyingType, comparand);
			}
			return false;
		}
		return false;
	}

	private static bool HasConstantValue(Optional<object?> constantValue, ITypeSymbol constantValueType, ulong comparand)
	{
		SpecialType specialType = constantValueType.SpecialType;
		if ((uint)(specialType - 18) <= 1u)
		{
			return (double?)constantValue.Value == (double)comparand;
		}
		if (DiagnosticHelpers.TryConvertToUInt64(constantValue.Value, constantValueType.SpecialType, out var convertedValue))
		{
			return convertedValue == comparand;
		}
		return false;
	}

	public static ITypeSymbol? GetElementType(this IArrayCreationOperation? arrayCreation)
	{
		return (arrayCreation?.Type as IArrayTypeSymbol)?.ElementType;
	}

	/// <summary>
	/// Filters out operations that are implicit and have no explicit descendant with a constant value or a non-null type.
	/// </summary>
	public static ImmutableArray<IOperation> WithoutFullyImplicitOperations(this ImmutableArray<IOperation> operations)
	{
		ImmutableArray<IOperation>.Builder builder = null;
		for (int i = 0; i < operations.Length; i++)
		{
			IOperation operation = operations[i];
			if (operation.DescendantsAndSelf().All((IOperation o) => o.IsImplicit || (!o.ConstantValue.HasValue && o.Type == null)))
			{
				if (builder == null)
				{
					builder = ImmutableArray.CreateBuilder<IOperation>();
					builder.AddRange(operations, i);
				}
			}
			else
			{
				builder?.Add(operation);
			}
		}
		return builder?.ToImmutable() ?? operations;
	}

	/// <summary>
	/// Gets explicit descendants or self of the given <paramref name="operation" /> that have no explicit ancestor in
	/// the operation tree rooted at <paramref name="operation" />.
	/// </summary>
	/// <param name="operation">Operation</param>
	public static ImmutableArray<IOperation> GetTopmostExplicitDescendants(this IOperation operation)
	{
		if (!operation.IsImplicit)
		{
			return ImmutableArray.Create(operation);
		}
		ImmutableArray<IOperation>.Builder builder = ImmutableArray.CreateBuilder<IOperation>();
		Queue<IOperation> queue = new Queue<IOperation>();
		queue.Enqueue(operation);
		while (queue.Count > 0)
		{
			operation = queue.Dequeue();
			if (!operation.IsImplicit)
			{
				builder.Add(operation);
				continue;
			}
			foreach (IOperation child in operation.Children)
			{
				queue.Enqueue(child);
			}
		}
		return builder.ToImmutable();
	}

	/// <summary>
	/// True if this operation has no IOperation API support, i.e. <see cref="F:Microsoft.CodeAnalysis.OperationKind.None" /> and
	/// is the root operation, i.e. <see cref="P:Microsoft.CodeAnalysis.Operation.Parent" /> is null.
	/// For example, this returns true for attribute operations.
	/// </summary>
	public static bool IsOperationNoneRoot(this IOperation operation)
	{
		if (operation.Kind == OperationKind.None)
		{
			return operation.Parent == null;
		}
		return false;
	}

	/// <summary>
	/// Returns the topmost <see cref="T:Microsoft.CodeAnalysis.Operations.IBlockOperation" /> containing the given <paramref name="operation" />.
	/// </summary>
	public static IBlockOperation? GetTopmostParentBlock(this IOperation? operation)
	{
		IOperation operation2 = operation;
		IBlockOperation result = null;
		while (operation2 != null)
		{
			if (operation2 is IBlockOperation blockOperation)
			{
				result = blockOperation;
			}
			operation2 = operation2.Parent;
		}
		return result;
	}

	/// <summary>
	/// Returns the first <see cref="T:Microsoft.CodeAnalysis.Operations.IBlockOperation" /> in the parent chain of <paramref name="operation" />.
	/// </summary>
	public static IBlockOperation? GetFirstParentBlock(this IOperation? operation)
	{
		for (IOperation operation2 = operation; operation2 != null; operation2 = operation2.Parent)
		{
			if (operation2 is IBlockOperation result)
			{
				return result;
			}
		}
		return null;
	}

	/// <summary>
	/// Gets the first ancestor of this operation with:
	///  1. Specified OperationKind
	///  2. If <paramref name="predicate" /> is non-null, it succeeds for the ancestor.
	/// Returns null if there is no such ancestor.
	/// </summary>
	public static TOperation? GetAncestor<TOperation>(this IOperation root, OperationKind ancestorKind, Func<TOperation, bool>? predicate = null) where TOperation : class, IOperation
	{
		if (root == null)
		{
			throw new ArgumentNullException("root");
		}
		IOperation operation = root;
		do
		{
			operation = operation.Parent;
		}
		while (operation != null && operation.Kind != ancestorKind);
		if (operation != null)
		{
			if (predicate != null && !predicate((TOperation)operation))
			{
				return operation.GetAncestor(ancestorKind, predicate);
			}
			return (TOperation)operation;
		}
		return null;
	}

	/// <summary>
	/// Gets the first ancestor of this operation with:
	///  1. Any OperationKind from the specified <paramref name="ancestorKinds" />.
	///  2. If <paramref name="predicate" /> is non-null, it succeeds for the ancestor.
	/// Returns null if there is no such ancestor.
	/// </summary>
	public static IOperation? GetAncestor(this IOperation root, ImmutableArray<OperationKind> ancestorKinds, Func<IOperation, bool>? predicate = null)
	{
		if (root == null)
		{
			throw new ArgumentNullException("root");
		}
		IOperation operation = root;
		do
		{
			operation = operation.Parent;
		}
		while (operation != null && !ancestorKinds.Contains(operation.Kind));
		if (operation != null)
		{
			if (predicate != null && !predicate(operation))
			{
				return operation.GetAncestor(ancestorKinds, predicate);
			}
			return operation;
		}
		return null;
	}

	public static IConditionalAccessOperation? GetConditionalAccess(this IConditionalAccessInstanceOperation operation)
	{
		IConditionalAccessInstanceOperation operation2 = operation;
		return operation2.GetAncestor(OperationKind.ConditionalAccess, (IConditionalAccessOperation c) => c.Operation.Syntax == operation2.Syntax);
	}

	/// <summary>
	/// Gets the operation for the object being created that is being referenced by <paramref name="operation" />.
	/// If the operation is referencing an implicit or an explicit this/base/Me/MyBase/MyClass instance, then we return "null".
	/// </summary>
	/// <param name="operation"></param>
	/// <param name="isInsideAnonymousObjectInitializer">Flag to indicate if the operation is a descendant of an <see cref="T:Microsoft.CodeAnalysis.Operations.IAnonymousObjectCreationOperation" />.</param>
	/// <remarks>
	/// PERF: Note that the parameter <paramref name="isInsideAnonymousObjectInitializer" /> is to improve performance by avoiding walking the entire IOperation parent for non-initializer cases.
	/// </remarks>
	public static IOperation? GetInstance(this IInstanceReferenceOperation operation, bool isInsideAnonymousObjectInitializer)
	{
		if (isInsideAnonymousObjectInitializer)
		{
			IOperation operation2 = operation;
			while (operation2 != null && operation2.Kind != OperationKind.Block)
			{
				if (operation2.Kind == OperationKind.AnonymousObjectCreation && operation2.Syntax == operation.Syntax)
				{
					return operation2;
				}
				operation2 = operation2.Parent;
			}
		}
		return null;
	}

	public static bool HasAnyOperationDescendant(this ImmutableArray<IOperation> operationBlocks, Func<IOperation, bool> predicate)
	{
		ImmutableArray<IOperation>.Enumerator enumerator = operationBlocks.GetEnumerator();
		while (enumerator.MoveNext())
		{
			if (enumerator.Current.HasAnyOperationDescendant(predicate))
			{
				return true;
			}
		}
		return false;
	}

	public static bool HasAnyOperationDescendant(this IOperation operationBlock, Func<IOperation, bool> predicate)
	{
		IOperation foundOperation;
		return operationBlock.HasAnyOperationDescendant(predicate, out foundOperation);
	}

	public static bool HasAnyOperationDescendant(this IOperation operationBlock, Func<IOperation, bool> predicate, [NotNullWhen(true)] out IOperation? foundOperation)
	{
		foreach (IOperation item in operationBlock.DescendantsAndSelf())
		{
			if (predicate(item))
			{
				foundOperation = item;
				return true;
			}
		}
		foundOperation = null;
		return false;
	}

	public static bool HasAnyOperationDescendant(this ImmutableArray<IOperation> operationBlocks, OperationKind kind)
	{
		return operationBlocks.HasAnyOperationDescendant((IOperation operation) => operation.Kind == kind);
	}

	/// <summary>
	/// Indicates if the given <paramref name="binaryOperation" /> is a predicate operation used in a condition.
	/// </summary>
	/// <param name="binaryOperation"></param>
	/// <returns></returns>
	public static bool IsComparisonOperator(this IBinaryOperation binaryOperation)
	{
		BinaryOperatorKind operatorKind = binaryOperation.OperatorKind;
		if ((uint)(operatorKind - 16) <= 7u)
		{
			return true;
		}
		return false;
	}

	/// <summary>
	/// Indicates if the given <paramref name="binaryOperation" /> is an addition or substaction operation.
	/// </summary>
	/// <param name="binaryOperation"></param>
	/// <returns>true if the operation is addition or substruction</returns>
	public static bool IsAdditionOrSubstractionOperation(this IBinaryOperation binaryOperation, out char binaryOperator)
	{
		binaryOperator = '\0';
		switch (binaryOperation.OperatorKind)
		{
		case BinaryOperatorKind.Add:
			binaryOperator = '+';
			return true;
		case BinaryOperatorKind.Subtract:
			binaryOperator = '-';
			return true;
		default:
			return false;
		}
	}

	public static IOperation GetRoot(this IOperation operation)
	{
		while (operation.Parent != null)
		{
			operation = operation.Parent;
		}
		return operation;
	}

	public static bool TryGetEnclosingControlFlowGraph(this IOperation operation, [NotNullWhen(true)] out ControlFlowGraph? cfg)
	{
		operation = operation.GetRoot();
		ConcurrentDictionary<IOperation, ControlFlowGraph> orCreateValue = s_operationToCfgCache.GetOrCreateValue(operation.SemanticModel.Compilation);
		cfg = orCreateValue.GetOrAdd(operation, CreateControlFlowGraph);
		return cfg != null;
	}

	public static ControlFlowGraph? GetEnclosingControlFlowGraph(this IBlockOperation blockOperation)
	{
		blockOperation.TryGetEnclosingControlFlowGraph(out ControlFlowGraph cfg);
		return cfg;
	}

	private static ControlFlowGraph? CreateControlFlowGraph(IOperation operation)
	{
		if (!(operation is IBlockOperation body))
		{
			if (!(operation is IMethodBodyOperation methodBody))
			{
				if (!(operation is IConstructorBodyOperation constructorBody))
				{
					if (!(operation is IFieldInitializerOperation initializer))
					{
						if (!(operation is IPropertyInitializerOperation initializer2))
						{
							if (operation is IParameterInitializerOperation)
							{
								return null;
							}
							return null;
						}
						return ControlFlowGraph.Create(initializer2);
					}
					return ControlFlowGraph.Create(initializer);
				}
				return ControlFlowGraph.Create(constructorBody);
			}
			return ControlFlowGraph.Create(methodBody);
		}
		return ControlFlowGraph.Create(body);
	}

	/// <summary>
	/// Gets the symbols captured from the enclosing function(s) by the given lambda or local function.
	/// </summary>
	/// <param name="operation">Operation representing the lambda or local function.</param>
	/// <param name="lambdaOrLocalFunction">Method symbol for the lambda or local function.</param>
	public static PooledHashSet<ISymbol> GetCaptures(this IOperation operation, IMethodSymbol lambdaOrLocalFunction)
	{
		lambdaOrLocalFunction = lambdaOrLocalFunction.OriginalDefinition;
		PooledHashSet<ISymbol> builder = PooledHashSet<ISymbol>.GetInstance();
		PooledHashSet<IMethodSymbol> nestedLambdasAndLocalFunctions = PooledHashSet<IMethodSymbol>.GetInstance();
		try
		{
			nestedLambdasAndLocalFunctions.Add(lambdaOrLocalFunction);
			foreach (IOperation item in operation.Descendants())
			{
				switch (item.Kind)
				{
				case OperationKind.LocalReference:
					ProcessLocalOrParameter(((ILocalReferenceOperation)item).Local);
					break;
				case OperationKind.ParameterReference:
					ProcessLocalOrParameter(((IParameterReferenceOperation)item).Parameter);
					break;
				case OperationKind.InstanceReference:
					builder.Add(lambdaOrLocalFunction.ContainingType);
					break;
				case OperationKind.AnonymousFunction:
					nestedLambdasAndLocalFunctions.Add(((IAnonymousFunctionOperation)item).Symbol);
					break;
				case OperationKind.LocalFunction:
					nestedLambdasAndLocalFunctions.Add(((ILocalFunctionOperation)item).Symbol);
					break;
				}
			}
			return builder;
		}
		finally
		{
			if (nestedLambdasAndLocalFunctions != null)
			{
				((IDisposable)nestedLambdasAndLocalFunctions).Dispose();
			}
		}
		void ProcessLocalOrParameter(ISymbol symbol)
		{
			ISymbol containingSymbol = symbol.ContainingSymbol;
			if (containingSymbol != null && containingSymbol.Kind == SymbolKind.Method && !nestedLambdasAndLocalFunctions.Contains(symbol.ContainingSymbol.OriginalDefinition))
			{
				builder.Add(symbol);
			}
		}
	}

	public static bool IsWithinLambdaOrLocalFunction(this IOperation operation, [NotNullWhen(true)] out IOperation? containingLambdaOrLocalFunctionOperation)
	{
		containingLambdaOrLocalFunctionOperation = operation.GetAncestor(s_LambdaAndLocalFunctionKinds);
		return containingLambdaOrLocalFunctionOperation != null;
	}

	public static bool IsWithinExpressionTree(this IOperation operation, [NotNullWhen(true)] INamedTypeSymbol? linqExpressionTreeType)
	{
		if (linqExpressionTreeType != null)
		{
			ITypeSymbol typeSymbol = operation.GetAncestor(s_LambdaAndLocalFunctionKinds)?.Parent?.Type?.OriginalDefinition;
			if (typeSymbol != null)
			{
				return linqExpressionTreeType.Equals(typeSymbol);
			}
		}
		return false;
	}

	public static ITypeSymbol? GetPatternType(this IPatternOperation pattern)
	{
		if (!(pattern is IDeclarationPatternOperation declarationPatternOperation))
		{
			if (!(pattern is IRecursivePatternOperation recursivePatternOperation))
			{
				if (!(pattern is IDiscardPatternOperation discardPatternOperation))
				{
					if (pattern is IConstantPatternOperation constantPatternOperation)
					{
						return constantPatternOperation.Value.Type;
					}
					return null;
				}
				return discardPatternOperation.InputType;
			}
			return recursivePatternOperation.MatchedType;
		}
		return declarationPatternOperation.MatchedType;
	}

	/// <summary>
	/// If the given <paramref name="tupleOperation" /> is a nested tuple,
	/// gets the parenting tuple operation and the tuple element of that parenting tuple
	/// which contains the given tupleOperation as a descendant operation.
	/// </summary>
	public static bool TryGetParentTupleOperation(this ITupleOperation tupleOperation, [NotNullWhen(true)] out ITupleOperation? parentTupleOperation, [NotNullWhen(true)] out IOperation? elementOfParentTupleContainingTuple)
	{
		parentTupleOperation = null;
		elementOfParentTupleContainingTuple = null;
		IOperation operation = tupleOperation;
		for (IOperation parent = tupleOperation.Parent; parent != null; parent = parent.Parent)
		{
			switch (parent.Kind)
			{
			case OperationKind.Conversion:
			case OperationKind.Parenthesized:
			case OperationKind.DeclarationExpression:
				break;
			case OperationKind.Tuple:
				parentTupleOperation = (ITupleOperation)parent;
				elementOfParentTupleContainingTuple = operation;
				return true;
			default:
				return false;
			}
			operation = parent;
		}
		return false;
	}

	public static bool IsExtensionMethodAndHasNoInstance(this IInvocationOperation invocationOperation)
	{
		if (invocationOperation.TargetMethod.IsExtensionMethod)
		{
			if (!(invocationOperation.Language != "Visual Basic"))
			{
				return invocationOperation.Instance == null;
			}
			return true;
		}
		return false;
	}

	public static IOperation? GetInstance(this IInvocationOperation invocationOperation)
	{
		if (!invocationOperation.IsExtensionMethodAndHasNoInstance())
		{
			return invocationOperation.Instance;
		}
		return invocationOperation.Arguments[0].Value;
	}

	public static SyntaxNode? GetInstanceSyntax(this IInvocationOperation invocationOperation)
	{
		return invocationOperation.GetInstance()?.Syntax;
	}

	public static ITypeSymbol? GetInstanceType(this IOperation operation)
	{
		IOperation instance;
		if (!(operation is IInvocationOperation invocationOperation))
		{
			if (!(operation is IPropertyReferenceOperation propertyReferenceOperation))
			{
				throw new NotImplementedException();
			}
			instance = propertyReferenceOperation.Instance;
		}
		else
		{
			instance = invocationOperation.GetInstance();
		}
		return instance?.WalkDownConversion().Type;
	}

	public static ISymbol? GetReferencedMemberOrLocalOrParameter(this IOperation? operation)
	{
		if (!(operation is IMemberReferenceOperation memberReferenceOperation))
		{
			if (!(operation is IParameterReferenceOperation parameterReferenceOperation))
			{
				if (!(operation is ILocalReferenceOperation localReferenceOperation))
				{
					if (!(operation is IParenthesizedOperation parenthesizedOperation))
					{
						if (operation is IConversionOperation conversionOperation)
						{
							return conversionOperation.Operand.GetReferencedMemberOrLocalOrParameter();
						}
						return null;
					}
					return parenthesizedOperation.Operand.GetReferencedMemberOrLocalOrParameter();
				}
				return localReferenceOperation.Local;
			}
			return parameterReferenceOperation.Parameter;
		}
		return memberReferenceOperation.Member;
	}

	/// <summary>
	/// Walks down consecutive parenthesized operations until an operand is reached that isn't a parenthesized operation.
	/// </summary>
	/// <param name="operation">The starting operation.</param>
	/// <returns>The inner non parenthesized operation or the starting operation if it wasn't a parenthesized operation.</returns>
	public static IOperation WalkDownParentheses(this IOperation operation)
	{
		while (operation is IParenthesizedOperation parenthesizedOperation)
		{
			operation = parenthesizedOperation.Operand;
		}
		return operation;
	}

	[return: NotNullIfNotNull("operation")]
	public static IOperation? WalkUpParentheses(this IOperation? operation)
	{
		if (operation == null)
		{
			return null;
		}
		while (operation.Parent is IParenthesizedOperation parenthesizedOperation)
		{
			operation = parenthesizedOperation;
		}
		return operation;
	}

	/// <summary>
	/// Walks down consecutive conversion operations until an operand is reached that isn't a conversion operation.
	/// </summary>
	/// <param name="operation">The starting operation.</param>
	/// <returns>The inner non conversion operation or the starting operation if it wasn't a conversion operation.</returns>
	public static IOperation WalkDownConversion(this IOperation operation)
	{
		while (operation is IConversionOperation conversionOperation)
		{
			operation = conversionOperation.Operand;
		}
		return operation;
	}

	/// <summary>
	/// Walks down consecutive conversion operations that satisfy <paramref name="predicate" /> until an operand is reached that
	/// either isn't a conversion or doesn't satisfy <paramref name="predicate" />.
	/// </summary>
	/// <param name="operation">The starting operation.</param>
	/// <param name="predicate">A predicate to filter conversion operations.</param>
	/// <returns>The first operation that either isn't a conversion or doesn't satisfy <paramref name="predicate" />.</returns>
	public static IOperation WalkDownConversion(this IOperation operation, Func<IConversionOperation, bool> predicate)
	{
		while (operation is IConversionOperation conversionOperation && predicate(conversionOperation))
		{
			operation = conversionOperation.Operand;
		}
		return operation;
	}

	[return: NotNullIfNotNull("operation")]
	public static IOperation? WalkUpConversion(this IOperation? operation)
	{
		if (operation == null)
		{
			return null;
		}
		while (operation.Parent is IConversionOperation conversionOperation)
		{
			operation = conversionOperation;
		}
		return operation;
	}

	public static IOperation? GetThrownException(this IThrowOperation operation)
	{
		IOperation operation2 = operation.Exception;
		if (operation2 is IConversionOperation { Conversion: { Exists: not false } } conversionOperation)
		{
			operation2 = conversionOperation.Operand;
		}
		return operation2;
	}

	public static ITypeSymbol? GetThrownExceptionType(this IThrowOperation operation)
	{
		return operation.GetThrownException()?.Type;
	}

	/// <summary>
	/// Determines if the one of the invocation's arguments' values is an argument of the specified type, and if so, find
	/// the first one.
	/// </summary>
	/// <param name="invocationOperation">Invocation operation whose arguments to look through.</param>
	/// <param name="firstFoundArgument">First found IArgumentOperation.Value of the specified type, order by the method's
	/// signature's parameters (as opposed to how arguments are specified when invoked).</param>
	/// <returns>True if one is found, false otherwise.</returns>
	/// <remarks>
	/// IInvocationOperation.Arguments are ordered by how they are specified, which may differ from the order in the method
	/// signature if the caller specifies arguments by name. This will find the first typeof operation ordered by the
	/// method signature's parameters.
	/// </remarks>
	public static bool HasArgument<TOperation>(this IInvocationOperation invocationOperation, [NotNullWhen(true)] out TOperation? firstFoundArgument) where TOperation : class, IOperation
	{
		firstFoundArgument = null;
		int num = int.MaxValue;
		ImmutableArray<IArgumentOperation>.Enumerator enumerator = invocationOperation.Arguments.GetEnumerator();
		while (enumerator.MoveNext())
		{
			IArgumentOperation current = enumerator.Current;
			IParameterSymbol? parameter = current.Parameter;
			if (parameter != null && parameter.Ordinal < num && current.Value is TOperation val)
			{
				num = current.Parameter.Ordinal;
				firstFoundArgument = val;
			}
		}
		return firstFoundArgument != null;
	}

	public static bool HasAnyExplicitDescendant(this IOperation operation, Func<IOperation, bool>? descendIntoOperation = null)
	{
		using ArrayBuilder<IEnumerator<IOperation>> arrayBuilder = ArrayBuilder<IEnumerator<IOperation>>.GetInstance();
		arrayBuilder.Add(operation.Children.GetEnumerator());
		while (arrayBuilder.Any())
		{
			IEnumerator<IOperation> enumerator = arrayBuilder.Last();
			arrayBuilder.RemoveLast();
			if (!enumerator.MoveNext())
			{
				continue;
			}
			IOperation current = enumerator.Current;
			arrayBuilder.Add(enumerator);
			if (current != null && (descendIntoOperation == null || descendIntoOperation(current)))
			{
				if (!current.IsImplicit && (current.ConstantValue.HasValue || current.Type != null))
				{
					return true;
				}
				arrayBuilder.Add(current.Children.GetEnumerator());
			}
		}
		return false;
	}

	public static bool IsSetMethodInvocation(this IPropertyReferenceOperation operation)
	{
		if (operation.Property.SetMethod == null)
		{
			return false;
		}
		IOperation operation2 = operation;
		while (true)
		{
			IOperation parent = operation2.Parent;
			if ((!(parent is IParenthesizedOperation) && !(parent is ITupleOperation)) || 1 == 0)
			{
				break;
			}
			operation2 = operation2.Parent;
		}
		if (operation2.Parent is IAssignmentOperation assignmentOperation && assignmentOperation.Target == operation2)
		{
			return true;
		}
		return false;
	}

	public static bool TryGetArgumentForParameterAtIndex(this ImmutableArray<IArgumentOperation> arguments, int parameterIndex, [NotNullWhen(true)] out IArgumentOperation? result)
	{
		ImmutableArray<IArgumentOperation>.Enumerator enumerator = arguments.GetEnumerator();
		while (enumerator.MoveNext())
		{
			IArgumentOperation current = enumerator.Current;
			IParameterSymbol? parameter = current.Parameter;
			if (parameter != null && parameter.Ordinal == parameterIndex)
			{
				result = current;
				return true;
			}
		}
		result = null;
		return false;
	}

	public static IArgumentOperation GetArgumentForParameterAtIndex(this ImmutableArray<IArgumentOperation> arguments, int parameterIndex)
	{
		if (arguments.TryGetArgumentForParameterAtIndex(parameterIndex, out IArgumentOperation result))
		{
			return result;
		}
		throw new InvalidOperationException();
	}

	/// <summary>
	/// Useful when named arguments used for a method call and you need them in the original parameter order.
	/// </summary>
	/// <param name="arguments">Arguments of the method</param>
	/// <returns>Returns the arguments in parameter order</returns>
	public static ImmutableArray<IArgumentOperation> GetArgumentsInParameterOrder(this ImmutableArray<IArgumentOperation> arguments)
	{
		using ArrayBuilder<IArgumentOperation> arrayBuilder = ArrayBuilder<IArgumentOperation>.GetInstance(arguments.Length, null);
		ImmutableArray<IArgumentOperation>.Enumerator enumerator = arguments.GetEnumerator();
		while (enumerator.MoveNext())
		{
			IArgumentOperation current = enumerator.Current;
			arrayBuilder[current.Parameter.Ordinal] = current;
		}
		return arrayBuilder.ToImmutableArray();
	}

	/// <summary>
	/// Returns the <see cref="T:Analyzer.Utilities.ValueUsageInfo" /> for the given operation.
	/// This extension can be removed once https://github.com/dotnet/roslyn/issues/25057 is implemented.
	/// </summary>
	public static ValueUsageInfo GetValueUsageInfo(this IOperation operation, ISymbol containingSymbol)
	{
		if (operation is ILocalReferenceOperation { IsDeclaration: not false, IsImplicit: false })
		{
			return ValueUsageInfo.Write;
		}
		IOperation parent;
		if (operation is IDeclarationPatternOperation)
		{
			parent = operation.Parent;
			if (!(parent is IPatternCaseClauseOperation))
			{
				if (!(parent is IRecursivePatternOperation))
				{
					if (!(parent is ISwitchExpressionArmOperation))
					{
						if (!(parent is IIsPatternOperation))
						{
							if (parent is IPropertySubpatternOperation)
							{
								return ValueUsageInfo.Write;
							}
							return ValueUsageInfo.ReadWrite;
						}
						return ValueUsageInfo.Write;
					}
					return ValueUsageInfo.Write;
				}
				return ValueUsageInfo.Write;
			}
			return ValueUsageInfo.Write;
		}
		if (operation.Parent is IAssignmentOperation assignmentOperation && assignmentOperation.Target == operation)
		{
			if (!operation.Parent.IsAnyCompoundAssignment())
			{
				return ValueUsageInfo.Write;
			}
			return ValueUsageInfo.ReadWrite;
		}
		if (operation.Parent is IIncrementOrDecrementOperation)
		{
			return ValueUsageInfo.ReadWrite;
		}
		if (operation.Parent is IParenthesizedOperation operation2)
		{
			return operation2.GetValueUsageInfo(containingSymbol) & ~ValueUsageInfo.WritableReference;
		}
		parent = operation.Parent;
		if ((parent is INameOfOperation || parent is ITypeOfOperation || parent is ISizeOfOperation) ? true : false)
		{
			return ValueUsageInfo.Name;
		}
		if (operation.Parent is IArgumentOperation argumentOperation)
		{
			return argumentOperation.Parameter?.RefKind switch
			{
				RefKind.In => ValueUsageInfo.ReadableReference, 
				RefKind.Out => ValueUsageInfo.WritableReference, 
				RefKind.Ref => ValueUsageInfo.ReadableWritableReference, 
				_ => ValueUsageInfo.Read, 
			};
		}
		if (operation.Parent is IReturnOperation operation3)
		{
			return operation3.GetRefKind(containingSymbol) switch
			{
				RefKind.In => ValueUsageInfo.ReadableReference, 
				RefKind.Ref => ValueUsageInfo.ReadableWritableReference, 
				_ => ValueUsageInfo.Read, 
			};
		}
		if (operation.Parent is IConditionalOperation conditionalOperation)
		{
			if (operation == conditionalOperation.WhenTrue || operation == conditionalOperation.WhenFalse)
			{
				return conditionalOperation.GetValueUsageInfo(containingSymbol);
			}
			return ValueUsageInfo.Read;
		}
		if (operation.Parent is IReDimClauseOperation reDimClauseOperation && reDimClauseOperation.Operand == operation)
		{
			if (!(reDimClauseOperation.Parent is IReDimOperation { Preserve: not false }))
			{
				return ValueUsageInfo.Write;
			}
			return ValueUsageInfo.ReadWrite;
		}
		if (operation.Parent is IDeclarationExpressionOperation operation4)
		{
			return operation4.GetValueUsageInfo(containingSymbol);
		}
		if (operation.IsInLeftOfDeconstructionAssignment(out IDeconstructionAssignmentOperation _))
		{
			return ValueUsageInfo.Write;
		}
		if (operation.Parent is IVariableInitializerOperation { Parent: IVariableDeclaratorOperation parent2 })
		{
			switch (parent2.Symbol.RefKind)
			{
			case RefKind.Ref:
				return ValueUsageInfo.ReadableWritableReference;
			case RefKind.In:
				return ValueUsageInfo.ReadableReference;
			}
		}
		return ValueUsageInfo.Read;
	}

	public static bool IsInLeftOfDeconstructionAssignment([DisallowNull] this IOperation? operation, out IDeconstructionAssignmentOperation? deconstructionAssignment)
	{
		deconstructionAssignment = null;
		IOperation operation2 = operation;
		for (operation = operation.Parent; operation != null; operation = operation.Parent)
		{
			switch (operation.Kind)
			{
			case OperationKind.DeconstructionAssignment:
				deconstructionAssignment = (IDeconstructionAssignmentOperation)operation;
				return deconstructionAssignment.Target == operation2;
			case OperationKind.Conversion:
			case OperationKind.Parenthesized:
			case OperationKind.Tuple:
				break;
			default:
				return false;
			}
			operation2 = operation;
		}
		return false;
	}

	public static RefKind GetRefKind(this IReturnOperation operation, ISymbol containingSymbol)
	{
		return (operation.TryGetContainingAnonymousFunctionOrLocalFunction() ?? (containingSymbol as IMethodSymbol))?.RefKind ?? RefKind.None;
	}

	public static IMethodSymbol? TryGetContainingAnonymousFunctionOrLocalFunction(this IOperation? operation)
	{
		for (operation = operation?.Parent; operation != null; operation = operation.Parent)
		{
			switch (operation.Kind)
			{
			case OperationKind.AnonymousFunction:
				return ((IAnonymousFunctionOperation)operation).Symbol;
			case OperationKind.LocalFunction:
				return ((ILocalFunctionOperation)operation).Symbol;
			}
		}
		return null;
	}

	/// <summary>
	/// Returns true if the given operation is a regular compound assignment,
	/// i.e. <see cref="T:Microsoft.CodeAnalysis.Operations.ICompoundAssignmentOperation" /> such as <code>a += b</code>,
	/// or a special null coalescing compound assignment, i.e. <see cref="T:Microsoft.CodeAnalysis.Operations.ICoalesceAssignmentOperation" />
	/// such as <code>a ??= b</code>.
	/// </summary>
	public static bool IsAnyCompoundAssignment(this IOperation operation)
	{
		if (operation is ICompoundAssignmentOperation || operation is ICoalesceAssignmentOperation)
		{
			return true;
		}
		return false;
	}
}
