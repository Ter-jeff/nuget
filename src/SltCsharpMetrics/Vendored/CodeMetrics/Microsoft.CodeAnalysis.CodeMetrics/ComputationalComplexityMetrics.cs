using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeAnalysis.CodeMetrics;

/// <summary>
/// Calculates computational complexity metrics based on the number
/// of operators and operands found in the code.
/// </summary>
/// <remarks>This metric is based off of the Halstead metric.</remarks>
internal sealed class ComputationalComplexityMetrics
{
	internal static readonly ComputationalComplexityMetrics Default = new ComputationalComplexityMetrics(0L, 0L, 0L, 0L, 0L, ImmutableHashSet<OperationKind>.Empty, ImmutableHashSet<BinaryOperatorKind>.Empty, ImmutableHashSet<UnaryOperatorKind>.Empty, ImmutableHashSet<CaseKind>.Empty, ImmutableHashSet<ISymbol>.Empty, ImmutableHashSet<object>.Empty);

	private static readonly object s_nullConstantPlaceholder = new object();

	private readonly long _symbolUsageCounts;

	private readonly long _constantUsageCounts;

	private readonly ImmutableHashSet<OperationKind> _distinctOperatorKinds;

	private readonly ImmutableHashSet<BinaryOperatorKind> _distinctBinaryOperatorKinds;

	private readonly ImmutableHashSet<UnaryOperatorKind> _distinctUnaryOperatorKinds;

	private readonly ImmutableHashSet<CaseKind> _distinctCaseKinds;

	private readonly ImmutableHashSet<ISymbol> _distinctReferencedSymbols;

	private readonly ImmutableHashSet<object> _distinctReferencedConstants;

	public bool IsDefault => this == Default;

	/// <summary>The number of unique operators found.</summary>
	public long DistinctOperators
	{
		get
		{
			int num = _distinctBinaryOperatorKinds.Count;
			if (_distinctBinaryOperatorKinds.Count > 1)
			{
				num += _distinctBinaryOperatorKinds.Count - 1;
			}
			if (_distinctUnaryOperatorKinds.Count > 1)
			{
				num += _distinctUnaryOperatorKinds.Count - 1;
			}
			if (_distinctCaseKinds.Count > 1)
			{
				num += _distinctCaseKinds.Count - 1;
			}
			return num;
		}
	}

	/// <summary>The number of unique operands found.</summary>
	public long DistinctOperands => _distinctReferencedSymbols.Count + _distinctReferencedConstants.Count;

	/// <summary>The total number of operator usages found.</summary>
	public long TotalOperators { get; }

	/// <summary>The total number of operand usages found.</summary>
	public long TotalOperands => _symbolUsageCounts + _constantUsageCounts;

	public long Vocabulary => DistinctOperators + DistinctOperands;

	public long Length => TotalOperators + TotalOperands;

	public double Volume => (double)Length * Math.Max(0.0, Math.Log(Vocabulary, 2.0));

	/// <summary>
	/// Count of executable lines of code, i.e. basically IOperations parented by IBlockOperation.
	/// </summary>
	public long ExecutableLines { get; }

	/// <summary>
	/// Count of effective lines of code for computation of maintainability index.
	/// </summary>
	public long EffectiveLinesOfCode { get; }

	private ComputationalComplexityMetrics(long executableLinesOfCode, long effectiveLinesOfMaintainableCode, long operatorUsageCounts, long symbolUsageCounts, long constantUsageCounts, ImmutableHashSet<OperationKind> distinctOperatorKinds, ImmutableHashSet<BinaryOperatorKind> distinctBinaryOperatorKinds, ImmutableHashSet<UnaryOperatorKind> distinctUnaryOperatorKinds, ImmutableHashSet<CaseKind> distinctCaseKinds, ImmutableHashSet<ISymbol> distinctReferencedSymbols, ImmutableHashSet<object> distinctReferencedConstants)
	{
		ExecutableLines = executableLinesOfCode;
		EffectiveLinesOfCode = effectiveLinesOfMaintainableCode;
		TotalOperators = operatorUsageCounts;
		_symbolUsageCounts = symbolUsageCounts;
		_constantUsageCounts = constantUsageCounts;
		_distinctOperatorKinds = distinctOperatorKinds;
		_distinctBinaryOperatorKinds = distinctBinaryOperatorKinds;
		_distinctUnaryOperatorKinds = distinctUnaryOperatorKinds;
		_distinctCaseKinds = distinctCaseKinds;
		_distinctReferencedSymbols = distinctReferencedSymbols;
		_distinctReferencedConstants = distinctReferencedConstants;
	}

	private static ComputationalComplexityMetrics Create(long executableLinesOfCode, long operatorUsageCounts, long symbolUsageCounts, long constantUsageCounts, bool hasSymbolInitializer, ImmutableHashSet<OperationKind> distinctOperatorKinds, ImmutableHashSet<BinaryOperatorKind> distinctBinaryOperatorKinds, ImmutableHashSet<UnaryOperatorKind> distinctUnaryOperatorKinds, ImmutableHashSet<CaseKind> distinctCaseKinds, ImmutableHashSet<ISymbol> distinctReferencedSymbols, ImmutableHashSet<object> distinctReferencedConstants)
	{
		if (executableLinesOfCode == 0L && operatorUsageCounts == 0L && symbolUsageCounts == 0L && constantUsageCounts == 0L && !hasSymbolInitializer)
		{
			return Default;
		}
		long effectiveLinesOfMaintainableCode = (hasSymbolInitializer ? (executableLinesOfCode + 1) : executableLinesOfCode);
		return new ComputationalComplexityMetrics(executableLinesOfCode, effectiveLinesOfMaintainableCode, operatorUsageCounts, symbolUsageCounts, constantUsageCounts, distinctOperatorKinds, distinctBinaryOperatorKinds, distinctUnaryOperatorKinds, distinctCaseKinds, distinctReferencedSymbols, distinctReferencedConstants);
	}

	public static ComputationalComplexityMetrics Compute(IOperation operationBlock)
	{
		bool hasSymbolInitializer2 = false;
		long num = 0L;
		long operatorUsageCounts = 0L;
		long symbolUsageCounts = 0L;
		long num2 = 0L;
		ImmutableHashSet<OperationKind>.Builder distinctOperatorKindsBuilder = null;
		ImmutableHashSet<BinaryOperatorKind>.Builder distinctBinaryOperatorKindsBuilder = null;
		ImmutableHashSet<UnaryOperatorKind>.Builder distinctUnaryOperatorKindsBuilder = null;
		ImmutableHashSet<CaseKind>.Builder builder = null;
		ImmutableHashSet<ISymbol>.Builder distinctReferencedSymbolsBuilder = null;
		ImmutableHashSet<object>.Builder builder2 = null;
		OperationKind kind = operationBlock.Kind;
		bool flag = ((kind == OperationKind.None || kind == (OperationKind)125) ? true : false);
		if (flag && hasAnyExplicitExpression(operationBlock))
		{
			num++;
		}
		foreach (IOperation item in operationBlock.Descendants())
		{
			num += getExecutableLinesOfCode(item, ref hasSymbolInitializer2);
			if (item.IsImplicit)
			{
				continue;
			}
			if (item.ConstantValue.HasValue)
			{
				num2++;
				if (builder2 == null)
				{
					builder2 = ImmutableHashSet.CreateBuilder<object>();
				}
				builder2.Add(item.ConstantValue.Value ?? s_nullConstantPlaceholder);
				continue;
			}
			switch (item.Kind)
			{
			case OperationKind.LocalReference:
				countOperand(((ILocalReferenceOperation)item).Local);
				break;
			case OperationKind.ParameterReference:
				countOperand(((IParameterReferenceOperation)item).Parameter);
				break;
			case OperationKind.FieldReference:
			case OperationKind.MethodReference:
			case OperationKind.PropertyReference:
			case OperationKind.EventReference:
				countOperator(item);
				countOperand(((IMemberReferenceOperation)item).Member);
				break;
			case OperationKind.FieldInitializer:
			{
				ImmutableArray<IFieldSymbol>.Enumerator enumerator3 = ((IFieldInitializerOperation)item).InitializedFields.GetEnumerator();
				while (enumerator3.MoveNext())
				{
					IFieldSymbol current3 = enumerator3.Current;
					countOperator(item);
					countOperand(current3);
				}
				break;
			}
			case OperationKind.PropertyInitializer:
			{
				ImmutableArray<IPropertySymbol>.Enumerator enumerator2 = ((IPropertyInitializerOperation)item).InitializedProperties.GetEnumerator();
				while (enumerator2.MoveNext())
				{
					IPropertySymbol current2 = enumerator2.Current;
					countOperator(item);
					countOperand(current2);
				}
				break;
			}
			case OperationKind.ParameterInitializer:
				countOperator(item);
				countOperand(((IParameterInitializerOperation)item).Parameter);
				break;
			case OperationKind.VariableInitializer:
				countOperator(item);
				break;
			case OperationKind.VariableDeclarator:
			{
				IVariableDeclaratorOperation variableDeclaratorOperation = (IVariableDeclaratorOperation)item;
				if (variableDeclaratorOperation.GetVariableInitializer() != null)
				{
					countOperand(variableDeclaratorOperation.Symbol);
				}
				break;
			}
			case OperationKind.Invocation:
			{
				countOperator(item);
				IInvocationOperation invocationOperation = (IInvocationOperation)item;
				if (!invocationOperation.TargetMethod.ReturnsVoid)
				{
					countOperand(invocationOperation.TargetMethod);
				}
				break;
			}
			case OperationKind.ObjectCreation:
				countOperator(item);
				countOperand(((IObjectCreationOperation)item).Constructor);
				break;
			case OperationKind.TypeParameterObjectCreation:
			case OperationKind.AnonymousObjectCreation:
			case OperationKind.DynamicObjectCreation:
			case OperationKind.DynamicInvocation:
			case OperationKind.DelegateCreation:
				countOperator(item);
				break;
			case OperationKind.Binary:
				countBinaryOperator(item, ((IBinaryOperation)item).OperatorKind);
				break;
			case OperationKind.CompoundAssignment:
				countBinaryOperator(item, ((ICompoundAssignmentOperation)item).OperatorKind);
				break;
			case OperationKind.TupleBinary:
				countBinaryOperator(item, ((ITupleBinaryOperation)item).OperatorKind);
				break;
			case OperationKind.Unary:
				countUnaryOperator(item, ((IUnaryOperation)item).OperatorKind);
				break;
			case OperationKind.CaseClause:
			{
				ICaseClauseOperation caseClauseOperation = (ICaseClauseOperation)item;
				if (builder == null)
				{
					builder = ImmutableHashSet.CreateBuilder<CaseKind>();
				}
				builder.Add(caseClauseOperation.CaseKind);
				if (caseClauseOperation.CaseKind == CaseKind.Relational)
				{
					countBinaryOperator(item, ((IRelationalCaseClauseOperation)item).Relation);
				}
				else
				{
					countOperator(item);
				}
				break;
			}
			case OperationKind.Conversion:
			case OperationKind.ArrayElementReference:
			case OperationKind.Coalesce:
			case OperationKind.IsType:
			case OperationKind.Await:
			case OperationKind.SimpleAssignment:
			case OperationKind.Parenthesized:
			case OperationKind.EventAssignment:
			case OperationKind.ConditionalAccess:
			case OperationKind.MemberInitializer:
			case OperationKind.NameOf:
			case OperationKind.TypeOf:
			case OperationKind.SizeOf:
			case OperationKind.AddressOf:
			case OperationKind.IsPattern:
			case OperationKind.Increment:
			case OperationKind.Decrement:
			case OperationKind.DeconstructionAssignment:
				countOperator(item);
				break;
			case OperationKind.Lock:
			case OperationKind.Using:
			case OperationKind.RaiseEvent:
			case OperationKind.ArrayCreation:
			case OperationKind.InterpolatedString:
			case OperationKind.Tuple:
			case OperationKind.DynamicMemberReference:
			case OperationKind.DynamicIndexerAccess:
			case OperationKind.Throw:
			case OperationKind.ArrayInitializer:
				countOperator(item);
				break;
			case OperationKind.Return:
			case OperationKind.YieldBreak:
			case OperationKind.YieldReturn:
				if (((IReturnOperation)item).ReturnedValue != null)
				{
					countOperator(item);
				}
				break;
			}
		}
		return Create(num, operatorUsageCounts, symbolUsageCounts, num2, hasSymbolInitializer2, (distinctOperatorKindsBuilder != null) ? distinctOperatorKindsBuilder.ToImmutable() : ImmutableHashSet<OperationKind>.Empty, (distinctBinaryOperatorKindsBuilder != null) ? distinctBinaryOperatorKindsBuilder.ToImmutable() : ImmutableHashSet<BinaryOperatorKind>.Empty, (distinctUnaryOperatorKindsBuilder != null) ? distinctUnaryOperatorKindsBuilder.ToImmutable() : ImmutableHashSet<UnaryOperatorKind>.Empty, (builder != null) ? builder.ToImmutable() : ImmutableHashSet<CaseKind>.Empty, (distinctReferencedSymbolsBuilder != null) ? distinctReferencedSymbolsBuilder.ToImmutable() : ImmutableHashSet<ISymbol>.Empty, (builder2 != null) ? builder2.ToImmutable() : ImmutableHashSet<object>.Empty);
		void countBinaryOperator(IOperation operation, BinaryOperatorKind operatorKind)
		{
			countOperator(operation);
			if (distinctBinaryOperatorKindsBuilder == null)
			{
				distinctBinaryOperatorKindsBuilder = ImmutableHashSet.CreateBuilder<BinaryOperatorKind>();
			}
			distinctBinaryOperatorKindsBuilder.Add(operatorKind);
		}
		void countOperand(ISymbol? symbol)
		{
			if (symbol != null)
			{
				symbolUsageCounts++;
				if (distinctReferencedSymbolsBuilder == null)
				{
					distinctReferencedSymbolsBuilder = ImmutableHashSet.CreateBuilder<ISymbol>();
				}
				distinctReferencedSymbolsBuilder.Add(symbol);
			}
		}
		void countOperator(IOperation operation)
		{
			operatorUsageCounts++;
			if (distinctOperatorKindsBuilder == null)
			{
				distinctOperatorKindsBuilder = ImmutableHashSet.CreateBuilder<OperationKind>();
			}
			distinctOperatorKindsBuilder.Add(operation.Kind);
		}
		void countUnaryOperator(IOperation operation, UnaryOperatorKind operatorKind)
		{
			countOperator(operation);
			if (distinctUnaryOperatorKindsBuilder == null)
			{
				distinctUnaryOperatorKindsBuilder = ImmutableHashSet.CreateBuilder<UnaryOperatorKind>();
			}
			distinctUnaryOperatorKindsBuilder.Add(operatorKind);
		}
		static int getExecutableLinesOfCode(IOperation operation, ref bool hasSymbolInitializer)
		{
			if (operation.Parent != null)
			{
				switch (operation.Parent.Kind)
				{
				case OperationKind.Block:
					return hasAnyExplicitExpression(operation) ? 1 : 0;
				case OperationKind.FieldInitializer:
				case OperationKind.PropertyInitializer:
				case OperationKind.ParameterInitializer:
					if (hasAnyExplicitExpression(operation))
					{
						hasSymbolInitializer = true;
						return 1;
					}
					break;
				case OperationKind.Conditional:
					if (operation.Kind != OperationKind.Conditional || !hasAnyExplicitExpression(operation))
					{
						return 0;
					}
					return 1;
				}
			}
			return 0;
		}
		static bool hasAnyExplicitExpression(IOperation operation)
		{
			return !operation.DescendantsAndSelf().All(delegate(IOperation o)
			{
				bool flag2 = o.IsImplicit;
				if (!flag2)
				{
					bool flag3 = !o.ConstantValue.HasValue && o.Type == null;
					if (flag3)
					{
						OperationKind kind2 = o.Kind;
						bool flag4 = ((kind2 == OperationKind.Branch || kind2 == (OperationKind)125) ? true : false);
						flag3 = !flag4;
					}
					flag2 = flag3;
				}
				return flag2;
			});
		}
	}

	public ComputationalComplexityMetrics Union(ComputationalComplexityMetrics other)
	{
		if (this == Default)
		{
			return other;
		}
		if (other == Default)
		{
			return this;
		}
		return new ComputationalComplexityMetrics(ExecutableLines + other.ExecutableLines, EffectiveLinesOfCode + other.EffectiveLinesOfCode, TotalOperators + other.TotalOperators, _symbolUsageCounts + other._symbolUsageCounts, _constantUsageCounts + other._constantUsageCounts, _distinctOperatorKinds.Union(other._distinctOperatorKinds), _distinctBinaryOperatorKinds.Union(other._distinctBinaryOperatorKinds), _distinctUnaryOperatorKinds.Union(other._distinctUnaryOperatorKinds), _distinctCaseKinds.Union(other._distinctCaseKinds), _distinctReferencedSymbols.Union(other._distinctReferencedSymbols), _distinctReferencedConstants.Union(other._distinctReferencedConstants));
	}
}
