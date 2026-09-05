using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeAnalysis.CodeMetrics;

internal static class MetricsHelper
{
	private static readonly char[] s_newlineChars = new[] { '\r', '\n' };

	internal static int GetAverageRoundedMetricValue(int total, int childrenCount)
	{
		return RoundMetricValue((double)total / (double)childrenCount);
	}

	private static int RoundMetricValue(double value)
	{
		return (int)Math.Round(value, 0);
	}

	internal static int NormalizeAndRoundMaintainabilityIndex(double maintIndex)
	{
		maintIndex = Math.Max(0.0, maintIndex);
		return RoundMetricValue(maintIndex / 171.0 * 100.0);
	}

	internal static void AddCoupledNamedTypes(ImmutableHashSet<INamedTypeSymbol>.Builder builder, WellKnownTypeProvider wellKnownTypeProvider, ImmutableHashSet<INamedTypeSymbol> coupledTypes)
	{
		foreach (INamedTypeSymbol coupledType in coupledTypes)
		{
			AddCoupledNamedTypesCore(builder, coupledType, wellKnownTypeProvider);
		}
	}

	internal static void AddCoupledNamedTypes(ImmutableHashSet<INamedTypeSymbol>.Builder builder, WellKnownTypeProvider wellKnownTypeProvider, ITypeSymbol coupledType)
	{
		AddCoupledNamedTypesCore(builder, coupledType, wellKnownTypeProvider);
	}

	internal static void AddCoupledNamedTypes(ImmutableHashSet<INamedTypeSymbol>.Builder builder, WellKnownTypeProvider wellKnownTypeProvider, ImmutableArray<IParameterSymbol> parameters)
	{
		ImmutableArray<IParameterSymbol>.Enumerator enumerator = parameters.GetEnumerator();
		while (enumerator.MoveNext())
		{
			IParameterSymbol current = enumerator.Current;
			AddCoupledNamedTypesCore(builder, current.Type, wellKnownTypeProvider);
		}
	}

	internal static long GetLinesOfCode(ImmutableArray<SyntaxReference> declarations, ISymbol symbol, CodeMetricsAnalysisContext context)
	{
		long num = 0L;
		ImmutableArray<SyntaxReference>.Enumerator enumerator = declarations.GetEnumerator();
		while (enumerator.MoveNext())
		{
			SyntaxNode topmostSyntaxNodeForDeclaration = GetTopmostSyntaxNodeForDeclaration(enumerator.Current, symbol, context);
			if (symbol.Kind != SymbolKind.Namespace || object.Equals(context.GetSemanticModel(topmostSyntaxNodeForDeclaration).GetDeclaredSymbol(topmostSyntaxNodeForDeclaration, context.CancellationToken), symbol))
			{
				FileLinePositionSpan lineSpan = topmostSyntaxNodeForDeclaration.SyntaxTree.GetLineSpan(topmostSyntaxNodeForDeclaration.FullSpan, context.CancellationToken);
				long num2 = lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line;
				if (num2 == 0L)
				{
					num2 = 1L;
				}
				else
				{
					int num3 = Math.Max(0, GetNewlineCountFromTriviaList(topmostSyntaxNodeForDeclaration.GetLeadingTrivia(), leading: true) + GetNewlineCountFromTriviaList(topmostSyntaxNodeForDeclaration.GetTrailingTrivia(), leading: false) - 1);
					num2 -= num3;
				}
				num += num2;
			}
		}
		return num;
		static int GetNewlineCountFromTriviaList(SyntaxTriviaList trivialList, bool leading)
		{
			return GetNewlineCount(trivialList.ToFullString(), leading);
		}
		static int GetNewlineCount(string trivia, bool leading)
		{
			int num7 = 0;
			string remaining = trivia;
			while (TryTakeNextLine(ref remaining, out string next2, leading) && IsAllWhiteSpace(next2))
			{
				num7++;
			}
			return num7;
		}
		static bool IsAllWhiteSpace(string value)
		{
			for (int i = 0; i < value.Length; i++)
			{
				if (!char.IsWhiteSpace(value[i]))
				{
					return false;
				}
			}
			return true;
		}
		static bool TryTakeNextLine(ref string remaining, out string next, bool leading)
		{
			if (remaining.Length == 0)
			{
				next = string.Empty;
				return false;
			}
			int num5;
			if (leading)
			{
				int num4 = remaining.IndexOfAny(s_newlineChars);
				if (num4 < 0)
				{
					next = remaining;
					remaining = string.Empty;
					return false;
				}
				next = remaining.Substring(0, num4);
				if (remaining[num4] == '\r' && remaining.Length > num4 + 1 && remaining[num4 + 1] == '\n')
				{
					num5 = num4 + 2;
					remaining = remaining.Substring(num5);
				}
				else
				{
					num5 = num4 + 1;
					remaining = remaining.Substring(num5);
				}
				return true;
			}
			int num6 = remaining.LastIndexOfAny(s_newlineChars);
			if (num6 < 0)
			{
				next = remaining;
				remaining = string.Empty;
				return false;
			}
			num5 = num6 + 1;
			next = remaining.Substring(num5);
			if (remaining[num6] == '\n' && num6 > 0 && remaining[num6 - 1] == '\r')
			{
				remaining = remaining.Substring(0, num6 - 1);
			}
			else
			{
				remaining = remaining.Substring(0, num6);
			}
			return true;
		}
	}

	internal static SyntaxNode GetTopmostSyntaxNodeForDeclaration(SyntaxReference declaration, ISymbol declaredSymbol, CodeMetricsAnalysisContext context)
	{
		SyntaxNode syntaxNode = declaration.GetSyntax(context.CancellationToken);
		if (syntaxNode.Language == "Visual Basic")
		{
			SemanticModel semanticModel = context.GetSemanticModel(syntaxNode);
			while (syntaxNode.Parent != null && object.Equals(semanticModel.GetDeclaredSymbol(syntaxNode.Parent, context.CancellationToken), declaredSymbol))
			{
				syntaxNode = syntaxNode.Parent;
			}
		}
		return syntaxNode;
	}

	internal static (int cyclomaticComplexity, ComputationalComplexityMetrics computationalComplexityMetrics) ComputeCoupledTypesAndComplexityExcludingMemberDecls(ImmutableArray<SyntaxReference> declarations, ISymbol symbol, ImmutableHashSet<INamedTypeSymbol>.Builder builder, CodeMetricsAnalysisContext context)
	{
		int num = 0;
		ComputationalComplexityMetrics computationalComplexityMetrics = ComputationalComplexityMetrics.Default;
		Queue<SyntaxNode> queue = new Queue<SyntaxNode>();
		using (PooledHashSet<SyntaxNode> pooledHashSet = PooledHashSet<SyntaxNode>.GetInstance())
		{
			ImmutableArray<SyntaxReference>.Enumerator enumerator = declarations.GetEnumerator();
			while (enumerator.MoveNext())
			{
				SyntaxReference current = enumerator.Current;
				SyntaxNode topmostSyntaxNodeForDeclaration = GetTopmostSyntaxNodeForDeclaration(current, symbol, context);
				queue.Enqueue(topmostSyntaxNodeForDeclaration);
				ImmutableArray<IParameterSymbol>.Enumerator enumerator2 = symbol.GetParameters().GetEnumerator();
				while (enumerator2.MoveNext())
				{
					SyntaxReference syntaxReference = enumerator2.Current.DeclaringSyntaxReferences.FirstOrDefault();
					if (syntaxReference != null)
					{
						SyntaxNode syntax = syntaxReference.GetSyntax(context.CancellationToken);
						queue.Enqueue(syntax);
					}
				}
				ImmutableArray<AttributeData> immutableArray = symbol.GetAttributes();
				if (symbol is IMethodSymbol methodSymbol)
				{
					immutableArray = immutableArray.AddRange(methodSymbol.GetReturnTypeAttributes());
				}
				ImmutableArray<AttributeData>.Enumerator enumerator3 = immutableArray.GetEnumerator();
				while (enumerator3.MoveNext())
				{
					AttributeData current2 = enumerator3.Current;
					if (current2.ApplicationSyntaxReference != null && current2.ApplicationSyntaxReference.SyntaxTree == current.SyntaxTree)
					{
						SyntaxNode syntax2 = current2.ApplicationSyntaxReference.GetSyntax(context.CancellationToken);
						if (pooledHashSet.Add(syntax2))
						{
							queue.Enqueue(syntax2);
						}
					}
				}
				do
				{
					SyntaxNode syntaxNode = queue.Dequeue();
					SemanticModel semanticModel = context.GetSemanticModel(syntaxNode);
					if (syntaxNode != topmostSyntaxNodeForDeclaration)
					{
						ISymbol declaredSymbol = semanticModel.GetDeclaredSymbol(syntaxNode, context.CancellationToken);
						if (declaredSymbol != null && !object.Equals(symbol, declaredSymbol) && declaredSymbol.Kind != SymbolKind.Parameter)
						{
							continue;
						}
					}
					AddCoupledNamedTypesCore(builder, semanticModel.GetTypeInfo(syntaxNode, context.CancellationToken).Type, context.WellKnownTypeProvider);
					IOperation operation2 = semanticModel.GetOperation(syntaxNode, context.CancellationToken);
					if (operation2 != null && operation2.Parent == null)
					{
						OperationKind kind = operation2.Kind;
						if (kind <= OperationKind.Block)
						{
							if (kind == OperationKind.None)
							{
								goto IL_01ca;
							}
							if (kind == OperationKind.Block)
							{
								goto IL_01c4;
							}
						}
						else
						{
							if ((uint)(kind - 88) <= 1u)
							{
								goto IL_01c4;
							}
							if (kind == (OperationKind)125)
							{
								goto IL_01ca;
							}
						}
						goto IL_01d7;
					}
					foreach (SyntaxNode item in syntaxNode.ChildNodes())
					{
						queue.Enqueue(item);
					}
					continue;
					IL_01ca:
					if (pooledHashSet.Contains(syntaxNode))
					{
						goto IL_01d7;
					}
					continue;
					IL_01c4:
					num++;
					goto IL_01d7;
					IL_01d7:
					computationalComplexityMetrics = computationalComplexityMetrics.Union(ComputationalComplexityMetrics.Compute(operation2));
					foreach (IOperation item2 in operation2.DescendantsAndSelf())
					{
						if (!item2.IsImplicit && hasConditionalLogic(item2))
						{
							num++;
						}
						AddCoupledNamedTypesCore(builder, item2.Type, context.WellKnownTypeProvider);
						if (item2 is IMemberReferenceOperation memberReferenceOperation && memberReferenceOperation.Member.IsStatic)
						{
							AddCoupledNamedTypesCore(builder, memberReferenceOperation.Member.ContainingType, context.WellKnownTypeProvider);
						}
						else if (item2 is IInvocationOperation invocationOperation && (invocationOperation.TargetMethod.IsStatic || invocationOperation.TargetMethod.IsExtensionMethod))
						{
							AddCoupledNamedTypesCore(builder, invocationOperation.TargetMethod.ContainingType, context.WellKnownTypeProvider);
						}
					}
				}
				while (queue.Count != 0);
			}
			return (cyclomaticComplexity: num, computationalComplexityMetrics: computationalComplexityMetrics);
		}
		static bool hasConditionalLogic(IOperation operation)
		{
			switch (operation.Kind)
			{
			case OperationKind.Loop:
			case OperationKind.Conditional:
			case OperationKind.Coalesce:
			case OperationKind.ConditionalAccess:
			case OperationKind.CaseClause:
				return true;
			case OperationKind.Binary:
			{
				IBinaryOperation binaryOperation = (IBinaryOperation)operation;
				if (binaryOperation.OperatorKind != BinaryOperatorKind.ConditionalAnd && binaryOperation.OperatorKind != BinaryOperatorKind.ConditionalOr)
				{
					if (binaryOperation.Type.SpecialType == SpecialType.System_Boolean)
					{
						if (binaryOperation.OperatorKind != BinaryOperatorKind.Or)
						{
							return binaryOperation.OperatorKind == BinaryOperatorKind.And;
						}
						return true;
					}
					return false;
				}
				return true;
			}
			default:
				return false;
			}
		}
	}

	private static void AddCoupledNamedTypesCore(ImmutableHashSet<INamedTypeSymbol>.Builder builder, ITypeSymbol typeOpt, WellKnownTypeProvider wellKnownTypeProvider)
	{
		if (!(typeOpt is INamedTypeSymbol namedTypeSymbol) || isIgnoreableType(namedTypeSymbol, wellKnownTypeProvider))
		{
			return;
		}
		builder.Add(namedTypeSymbol.OriginalDefinition);
		if (namedTypeSymbol.IsGenericType)
		{
			ImmutableArray<ITypeSymbol>.Enumerator enumerator = namedTypeSymbol.TypeArguments.GetEnumerator();
			while (enumerator.MoveNext())
			{
				ITypeSymbol current = enumerator.Current;
				AddCoupledNamedTypesCore(builder, current, wellKnownTypeProvider);
			}
		}
		static bool isIgnoreableType(INamedTypeSymbol namedType, WellKnownTypeProvider wellKnownTypeProvider)
		{
			switch (namedType.SpecialType)
			{
			case SpecialType.System_Object:
			case SpecialType.System_ValueType:
			case SpecialType.System_Void:
			case SpecialType.System_Boolean:
			case SpecialType.System_Char:
			case SpecialType.System_SByte:
			case SpecialType.System_Byte:
			case SpecialType.System_Int16:
			case SpecialType.System_UInt16:
			case SpecialType.System_Int32:
			case SpecialType.System_UInt32:
			case SpecialType.System_Int64:
			case SpecialType.System_UInt64:
			case SpecialType.System_Single:
			case SpecialType.System_Double:
			case SpecialType.System_String:
			case SpecialType.System_IntPtr:
			case SpecialType.System_UIntPtr:
				return true;
			default:
				return namedType.IsAnonymousType || namedType.GetAttributes().Any((AttributeData a, WellKnownTypeProvider wellKnownTypeProvider) => a.AttributeClass.Equals(wellKnownTypeProvider.GetOrCreateTypeByMetadataName("System.Runtime.CompilerServices.CompilerGeneratedAttribute")) || a.AttributeClass.Equals(wellKnownTypeProvider.GetOrCreateTypeByMetadataName("System.CodeDom.Compiler.GeneratedCodeAttribute")), wellKnownTypeProvider);
			}
		}
	}

	internal static void RemoveContainingTypes(ISymbol symbol, ImmutableHashSet<INamedTypeSymbol>.Builder coupledTypesBuilder)
	{
		for (INamedTypeSymbol namedTypeSymbol = (symbol as INamedTypeSymbol) ?? symbol.ContainingType; namedTypeSymbol != null; namedTypeSymbol = namedTypeSymbol.ContainingType)
		{
			coupledTypesBuilder.Remove(namedTypeSymbol);
		}
	}

	internal static ImmutableArray<IParameterSymbol> GetParameters(this ISymbol member)
	{
		return member.Kind switch
		{
			SymbolKind.Method => ((IMethodSymbol)member).Parameters, 
			SymbolKind.Property => ((IPropertySymbol)member).Parameters, 
			_ => ImmutableArray<IParameterSymbol>.Empty, 
		};
	}
}
