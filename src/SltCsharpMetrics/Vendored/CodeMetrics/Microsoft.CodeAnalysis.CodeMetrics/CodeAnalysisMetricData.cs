using System.Collections.Immutable;
using System.Text;
using Analyzer.Utilities.Extensions;

namespace Microsoft.CodeAnalysis.CodeMetrics;

/// <summary>
/// Code analysis metrics data.
/// See https://learn.microsoft.com/visualstudio/code-quality/code-metrics-values for more details
/// </summary>
public abstract class CodeAnalysisMetricData
{
	private sealed class AssemblyMetricData : CodeAnalysisMetricData
	{
		private AssemblyMetricData(IAssemblySymbol symbol, int maintainabilityIndex, ImmutableHashSet<INamedTypeSymbol> coupledNamedTypes, long linesOfCode, int cyclomaticComplexity, int? depthOfInheritance, ImmutableArray<CodeAnalysisMetricData> children)
			: base(symbol, maintainabilityIndex, Microsoft.CodeAnalysis.CodeMetrics.ComputationalComplexityMetrics.Default, coupledNamedTypes, linesOfCode, cyclomaticComplexity, depthOfInheritance, null, children)
		{
		}

		internal static async Task<AssemblyMetricData> ComputeAsync(IAssemblySymbol assembly, CodeMetricsAnalysisContext context)
		{
			return ComputeFromChildren(assembly, await ComputeAsync(GetChildSymbols(assembly), context).ConfigureAwait(continueOnCapturedContext: false), context);
		}

		internal static AssemblyMetricData ComputeSynchronously(IAssemblySymbol assembly, CodeMetricsAnalysisContext context)
		{
			ImmutableArray<CodeAnalysisMetricData> children = ComputeSynchronously(GetChildSymbols(assembly), context);
			return ComputeFromChildren(assembly, children, context);
		}

		private static AssemblyMetricData ComputeFromChildren(IAssemblySymbol assembly, ImmutableArray<CodeAnalysisMetricData> children, CodeMetricsAnalysisContext context)
		{
			ImmutableHashSet<INamedTypeSymbol>.Builder builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>();
			long num = 0L;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			int num5 = 0;
			ImmutableArray<CodeAnalysisMetricData>.Enumerator enumerator = children.GetEnumerator();
			while (enumerator.MoveNext())
			{
				CodeAnalysisMetricData current = enumerator.Current;
				MetricsHelper.AddCoupledNamedTypes(builder, context.WellKnownTypeProvider, current.CoupledNamedTypes);
				num += current.SourceLines;
				num3 += current.CyclomaticComplexity;
				num4 = Math.Max(current.DepthOfInheritance.GetValueOrDefault(), num4);
				num2 += current.MaintainabilityIndex * current.Children.Length;
				num5 += current.Children.Length;
			}
			int maintainabilityIndex = ((num5 > 0) ? MetricsHelper.GetAverageRoundedMetricValue(num2, num5) : 100);
			return new AssemblyMetricData(assembly, maintainabilityIndex, builder.ToImmutable(), num, num3, num4, children);
		}

		private static ImmutableArray<INamespaceOrTypeSymbol> GetChildSymbols(IAssemblySymbol assembly)
		{
			bool includeGlobalNamespace = false;
			HashSet<INamespaceSymbol> namespacesWithTypeMember = new HashSet<INamespaceSymbol>();
			processNamespace(assembly.GlobalNamespace);
			ImmutableArray<INamespaceOrTypeSymbol>.Builder builder = ImmutableArray.CreateBuilder<INamespaceOrTypeSymbol>();
			if (includeGlobalNamespace)
			{
				builder.Add(assembly.GlobalNamespace);
			}
			foreach (INamespaceSymbol item in namespacesWithTypeMember.OrderBy((INamespaceSymbol ns) => ns.ToDisplayString()))
			{
				builder.Add(item);
			}
			return builder.ToImmutable();
			void processNamespace(INamespaceSymbol @namespace)
			{
				foreach (INamespaceOrTypeSymbol member in @namespace.GetMembers())
				{
					if (member.Kind == SymbolKind.Namespace)
					{
						processNamespace((INamespaceSymbol)member);
					}
					else if (@namespace.IsGlobalNamespace)
					{
						includeGlobalNamespace = true;
					}
					else if (!member.IsImplicitlyDeclared)
					{
						namespacesWithTypeMember.Add(@namespace);
					}
				}
			}
		}
	}

	private sealed class EventMetricData : CodeAnalysisMetricData
	{
		internal EventMetricData(IEventSymbol symbol, int maintainabilityIndex, ComputationalComplexityMetrics computationalComplexityMetrics, ImmutableHashSet<INamedTypeSymbol> coupledNamedTypes, long linesOfCode, int cyclomaticComplexity, int? depthOfInheritance, ImmutableArray<CodeAnalysisMetricData> children)
			: base(symbol, maintainabilityIndex, computationalComplexityMetrics, coupledNamedTypes, linesOfCode, cyclomaticComplexity, depthOfInheritance, null, children)
		{
		}

		internal static EventMetricData Compute(IEventSymbol @event, CodeMetricsAnalysisContext context)
		{
			ImmutableHashSet<INamedTypeSymbol>.Builder builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>();
			ImmutableArray<SyntaxReference> declaringSyntaxReferences = @event.DeclaringSyntaxReferences;
			long linesOfCode = MetricsHelper.GetLinesOfCode(declaringSyntaxReferences, @event, context);
			var (num, computationalComplexityMetrics) = MetricsHelper.ComputeCoupledTypesAndComplexityExcludingMemberDecls(declaringSyntaxReferences, @event, builder, context);
			MetricsHelper.AddCoupledNamedTypes(builder, context.WellKnownTypeProvider, @event.Type);
			ImmutableArray<CodeAnalysisMetricData> children = ComputeSynchronously(GetAccessors(@event), context);
			int num2 = 0;
			ImmutableArray<CodeAnalysisMetricData>.Enumerator enumerator = children.GetEnumerator();
			while (enumerator.MoveNext())
			{
				CodeAnalysisMetricData current = enumerator.Current;
				MetricsHelper.AddCoupledNamedTypes(builder, context.WellKnownTypeProvider, current.CoupledNamedTypes);
				num2 += current.MaintainabilityIndex;
				num += current.CyclomaticComplexity;
				computationalComplexityMetrics = computationalComplexityMetrics.Union(current.ComputationalComplexityMetrics);
			}
			int? depthOfInheritance = null;
			int maintainabilityIndex = ((!children.IsEmpty) ? MetricsHelper.GetAverageRoundedMetricValue(num2, children.Length) : 100);
			MetricsHelper.RemoveContainingTypes(@event, builder);
			return new EventMetricData(@event, maintainabilityIndex, computationalComplexityMetrics, builder.ToImmutable(), linesOfCode, num, depthOfInheritance, children);
		}

		private static IEnumerable<IMethodSymbol> GetAccessors(IEventSymbol @event)
		{
			if (@event.AddMethod != null)
			{
				yield return @event.AddMethod;
			}
			if (@event.RemoveMethod != null)
			{
				yield return @event.RemoveMethod;
			}
			if (@event.RaiseMethod != null)
			{
				yield return @event.RaiseMethod;
			}
		}
	}

	private sealed class FieldMetricData : CodeAnalysisMetricData
	{
		internal FieldMetricData(IFieldSymbol symbol, int maintainabilityIndex, ComputationalComplexityMetrics computationalComplexityMetrics, ImmutableHashSet<INamedTypeSymbol> coupledNamedTypes, long linesOfCode, int cyclomaticComplexity, int? depthOfInheritance)
			: base(symbol, maintainabilityIndex, computationalComplexityMetrics, coupledNamedTypes, linesOfCode, cyclomaticComplexity, depthOfInheritance, null, ImmutableArray<CodeAnalysisMetricData>.Empty)
		{
		}

		internal static FieldMetricData Compute(IFieldSymbol field, CodeMetricsAnalysisContext context)
		{
			ImmutableHashSet<INamedTypeSymbol>.Builder builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>();
			ImmutableArray<SyntaxReference> declaringSyntaxReferences = field.DeclaringSyntaxReferences;
			long linesOfCode = MetricsHelper.GetLinesOfCode(declaringSyntaxReferences, field, context);
			var (cyclomaticComplexity, computationalComplexityMetrics) = MetricsHelper.ComputeCoupledTypesAndComplexityExcludingMemberDecls(declaringSyntaxReferences, field, builder, context);
			MetricsHelper.AddCoupledNamedTypes(builder, context.WellKnownTypeProvider, field.Type);
			int? depthOfInheritance = null;
			int maintainabilityIndex = CalculateMaintainabilityIndex(computationalComplexityMetrics, cyclomaticComplexity);
			MetricsHelper.RemoveContainingTypes(field, builder);
			return new FieldMetricData(field, maintainabilityIndex, computationalComplexityMetrics, builder.ToImmutable(), linesOfCode, cyclomaticComplexity, depthOfInheritance);
		}

		private static int CalculateMaintainabilityIndex(ComputationalComplexityMetrics computationalComplexityMetrics, int cyclomaticComplexity)
		{
			double num = Math.Max(0.0, Math.Log(computationalComplexityMetrics.Volume));
			double num2 = Math.Max(0.0, Math.Log(computationalComplexityMetrics.EffectiveLinesOfCode));
			return MetricsHelper.NormalizeAndRoundMaintainabilityIndex(171.0 - 5.2 * num - 0.23 * (double)cyclomaticComplexity - 16.2 * num2);
		}
	}

	private sealed class MethodMetricData : CodeAnalysisMetricData
	{
		internal MethodMetricData(IMethodSymbol symbol, int maintainabilityIndex, ComputationalComplexityMetrics computationalComplexityMetrics, ImmutableHashSet<INamedTypeSymbol> coupledNamedTypes, long linesOfCode, int cyclomaticComplexity, int? depthOfInheritance)
			: base(symbol, maintainabilityIndex, computationalComplexityMetrics, coupledNamedTypes, linesOfCode, cyclomaticComplexity, depthOfInheritance, null, ImmutableArray<CodeAnalysisMetricData>.Empty)
		{
		}

		internal static MethodMetricData Compute(IMethodSymbol method, CodeMetricsAnalysisContext context)
		{
			ImmutableHashSet<INamedTypeSymbol>.Builder builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>();
			ImmutableArray<SyntaxReference> declaringSyntaxReferences = method.DeclaringSyntaxReferences;
			long linesOfCode = MetricsHelper.GetLinesOfCode(declaringSyntaxReferences, method, context);
			var (num, computationalComplexityMetrics) = MetricsHelper.ComputeCoupledTypesAndComplexityExcludingMemberDecls(declaringSyntaxReferences, method, builder, context);
			MetricsHelper.AddCoupledNamedTypes(builder, context.WellKnownTypeProvider, method.Parameters);
			if (!method.ReturnsVoid)
			{
				MetricsHelper.AddCoupledNamedTypes(builder, context.WellKnownTypeProvider, method.ReturnType);
			}
			int? depthOfInheritance = null;
			int maintainabilityIndex = CalculateMaintainabilityIndex(computationalComplexityMetrics, num);
			MetricsHelper.RemoveContainingTypes(method, builder);
			if (num == 0)
			{
				num = 1;
			}
			return new MethodMetricData(method, maintainabilityIndex, computationalComplexityMetrics, builder.ToImmutable(), linesOfCode, num, depthOfInheritance);
		}

		private static int CalculateMaintainabilityIndex(ComputationalComplexityMetrics computationalComplexityMetrics, int cyclomaticComplexity)
		{
			double num = Math.Max(0.0, Math.Log(computationalComplexityMetrics.Volume));
			double num2 = Math.Max(0.0, Math.Log(computationalComplexityMetrics.EffectiveLinesOfCode));
			return MetricsHelper.NormalizeAndRoundMaintainabilityIndex(171.0 - 5.2 * num - 0.23 * (double)cyclomaticComplexity - 16.2 * num2);
		}
	}

	private sealed class NamedTypeMetricData : CodeAnalysisMetricData
	{
		internal NamedTypeMetricData(INamedTypeSymbol symbol, int maintainabilityIndex, ComputationalComplexityMetrics computationalComplexityMetrics, ImmutableHashSet<INamedTypeSymbol> coupledNamedTypes, long linesOfCode, int cyclomaticComplexity, int? depthOfInheritance, double? lcom, ImmutableArray<CodeAnalysisMetricData> children)
			: base(symbol, maintainabilityIndex, computationalComplexityMetrics, coupledNamedTypes, linesOfCode, cyclomaticComplexity, depthOfInheritance, lcom, children)
		{
		}

		internal static async Task<NamedTypeMetricData> ComputeAsync(INamedTypeSymbol namedType, CodeMetricsAnalysisContext context)
		{
			return ComputeFromChildren(namedType, await ComputeAsync(GetMembers(namedType, context), context).ConfigureAwait(continueOnCapturedContext: false), context);
		}

		internal static NamedTypeMetricData ComputeSynchronously(INamedTypeSymbol namedType, CodeMetricsAnalysisContext context)
		{
			ImmutableArray<CodeAnalysisMetricData> children = ComputeSynchronously(GetMembers(namedType, context), context);
			return ComputeFromChildren(namedType, children, context);
		}

		private static IEnumerable<ISymbol> GetMembers(INamedTypeSymbol namedType, CodeMetricsAnalysisContext context)
		{
			return from m in namedType.GetMembers()
				where m.Kind != SymbolKind.NamedType
				where m.Kind != SymbolKind.Method || ((IMethodSymbol)m).AssociatedSymbol == null
				select m;
		}

		private static double CalculateLackOfCohesionOfMethods(INamedTypeSymbol namedType, CodeMetricsAnalysisContext context)
		{
			ImmutableArray<ISymbol> members = namedType.GetMembers();
			IPropertySymbol propertySymbol;
			IFieldSymbol[] array = (from m in members
				where m.Kind == SymbolKind.Field
				select (IFieldSymbol)m into f
				where !f.IsBackingFieldForProperty(out propertySymbol) && !f.IsConst && (f == null || !f.IsStatic || !f.IsReadOnly) && !f.IsPublic()
				select f).ToArray();
			IMethodSymbol[] array2 = (from m in members
				where m.Kind == SymbolKind.Method
				select (IMethodSymbol)m into m
				where !m.IsImplicitConstructor() && !m.IsAutoPropertyAccessor()
				select m).ToArray();
			int num = array.Length;
			int num2 = array2.Length;
			if (num == 0 || num2 == 0)
			{
				return double.NaN;
			}
			double num3 = 0.0;
			IFieldSymbol[] array3 = array;
			foreach (IFieldSymbol field in array3)
			{
				IMethodSymbol[] array4 = array2;
				foreach (IMethodSymbol method in array4)
				{
					if (MethodBodyVisitor.IsFieldAccessedByMethod(context, field, method))
					{
						num3 += 1.0;
					}
				}
			}
			return 1.0 - num3 / (double)(num * num2);
		}

		private static NamedTypeMetricData ComputeFromChildren(INamedTypeSymbol namedType, ImmutableArray<CodeAnalysisMetricData> children, CodeMetricsAnalysisContext context)
		{
			ImmutableHashSet<INamedTypeSymbol>.Builder builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>();
			ImmutableArray<SyntaxReference> declaringSyntaxReferences = namedType.DeclaringSyntaxReferences;
			(int cyclomaticComplexity, ComputationalComplexityMetrics computationalComplexityMetrics) tuple = MetricsHelper.ComputeCoupledTypesAndComplexityExcludingMemberDecls(declaringSyntaxReferences, namedType, builder, context);
			int num = tuple.cyclomaticComplexity;
			ComputationalComplexityMetrics computationalComplexityMetrics = tuple.computationalComplexityMetrics;
			ImmutableHashSet<IFieldSymbol> immutableHashSet = getFilteredFieldsForComplexity();
			int num2 = 0;
			int num3 = -1;
			ImmutableArray<CodeAnalysisMetricData>.Enumerator enumerator = children.GetEnumerator();
			while (enumerator.MoveNext())
			{
				CodeAnalysisMetricData current = enumerator.Current;
				MetricsHelper.AddCoupledNamedTypes(builder, context.WellKnownTypeProvider, current.CoupledNamedTypes);
				if (current.Symbol.Kind != SymbolKind.Field || immutableHashSet.Contains((IFieldSymbol)current.Symbol))
				{
					num3 = ((num2 == 0 && computationalComplexityMetrics.IsDefault) ? current.MaintainabilityIndex : (-1));
					num2++;
					num += current.CyclomaticComplexity;
					computationalComplexityMetrics = computationalComplexityMetrics.Union(current.ComputationalComplexityMetrics);
				}
			}
			if (num == 0 && !namedType.IsStatic)
			{
				num = 1;
			}
			int value = CalculateDepthOfInheritance(namedType, context.IsExcludedFromInheritanceCountFunc);
			double value2 = CalculateLackOfCohesionOfMethods(namedType, context);
			long linesOfCode = MetricsHelper.GetLinesOfCode(declaringSyntaxReferences, namedType, context);
			int maintainabilityIndex = ((num3 != -1) ? num3 : CalculateMaintainabilityIndex(computationalComplexityMetrics, num, num2));
			MetricsHelper.RemoveContainingTypes(namedType, builder);
			return new NamedTypeMetricData(namedType, maintainabilityIndex, computationalComplexityMetrics, builder.ToImmutable(), linesOfCode, num, value, value2, children);
			ImmutableHashSet<IFieldSymbol> getFilteredFieldsForComplexity()
			{
				ImmutableHashSet<IFieldSymbol>.Builder builder2 = null;
				IOrderedEnumerable<CodeAnalysisMetricData> orderedEnumerable = from c in children
					where c.Symbol.Kind == SymbolKind.Field
					orderby c.MaintainabilityIndex
					select c;
				int num4 = 99;
				foreach (CodeAnalysisMetricData item in orderedEnumerable)
				{
					if (item.MaintainabilityIndex > num4)
					{
						break;
					}
					if (builder2 == null)
					{
						builder2 = ImmutableHashSet.CreateBuilder<IFieldSymbol>();
					}
					builder2.Add((IFieldSymbol)item.Symbol);
					num4 -= 4;
				}
				return builder2?.ToImmutable() ?? ImmutableHashSet<IFieldSymbol>.Empty;
			}
		}

		private static int CalculateDepthOfInheritance(INamedTypeSymbol namedType, Func<INamedTypeSymbol, bool> isExcludedFromInheritanceCount)
		{
			switch (namedType.TypeKind)
			{
			case TypeKind.Class:
			case TypeKind.Interface:
			{
				int num = 0;
				INamedTypeSymbol baseType = namedType.BaseType;
				while (baseType != null && !isExcludedFromInheritanceCount(baseType))
				{
					num++;
					baseType = baseType.BaseType;
				}
				return num;
			}
			case TypeKind.Delegate:
			case TypeKind.Enum:
			case TypeKind.Struct:
				return 1;
			default:
				return 0;
			}
		}

		private static int CalculateMaintainabilityIndex(ComputationalComplexityMetrics computationalComplexityMetrics, int cyclomaticComplexity, int effectiveChildrenCount)
		{
			double d = 1.0;
			double d2 = 0.0;
			double num = 0.0;
			if (effectiveChildrenCount > 0)
			{
				d = computationalComplexityMetrics.Volume / (double)effectiveChildrenCount;
				d2 = (double)computationalComplexityMetrics.EffectiveLinesOfCode / (double)effectiveChildrenCount;
				num = (double)cyclomaticComplexity / (double)effectiveChildrenCount;
			}
			double num2 = Math.Max(0.0, Math.Log(d));
			double num3 = Math.Max(0.0, Math.Log(d2));
			return MetricsHelper.NormalizeAndRoundMaintainabilityIndex(171.0 - 5.2 * num2 - 0.23 * num - 16.2 * num3);
		}
	}

	private sealed class NamespaceMetricData : CodeAnalysisMetricData
	{
		internal NamespaceMetricData(INamespaceSymbol symbol, int maintainabilityIndex, ImmutableHashSet<INamedTypeSymbol> coupledNamedTypes, long linesOfCode, int cyclomaticComplexity, int? depthOfInheritance, ImmutableArray<CodeAnalysisMetricData> children)
			: base(symbol, maintainabilityIndex, Microsoft.CodeAnalysis.CodeMetrics.ComputationalComplexityMetrics.Default, coupledNamedTypes, linesOfCode, cyclomaticComplexity, depthOfInheritance, null, children)
		{
		}

		internal static async Task<NamespaceMetricData> ComputeAsync(INamespaceSymbol @namespace, CodeMetricsAnalysisContext context)
		{
			return ComputeFromChildren(@namespace, await ComputeAsync(GetChildSymbols(@namespace), context).ConfigureAwait(continueOnCapturedContext: false), context);
		}

		internal static NamespaceMetricData ComputeSynchronously(INamespaceSymbol @namespace, CodeMetricsAnalysisContext context)
		{
			ImmutableArray<CodeAnalysisMetricData> children = ComputeSynchronously(GetChildSymbols(@namespace), context);
			return ComputeFromChildren(@namespace, children, context);
		}

		private static NamespaceMetricData ComputeFromChildren(INamespaceSymbol @namespace, ImmutableArray<CodeAnalysisMetricData> children, CodeMetricsAnalysisContext context)
		{
			ImmutableHashSet<INamedTypeSymbol>.Builder builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>();
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			long num4 = 0L;
			ImmutableArray<CodeAnalysisMetricData>.Enumerator enumerator = children.GetEnumerator();
			while (enumerator.MoveNext())
			{
				CodeAnalysisMetricData current = enumerator.Current;
				MetricsHelper.AddCoupledNamedTypes(builder, context.WellKnownTypeProvider, current.CoupledNamedTypes);
				num += current.MaintainabilityIndex;
				num2 += current.CyclomaticComplexity;
				num3 = Math.Max(current.DepthOfInheritance.GetValueOrDefault(), num3);
				if (current.Symbol.ContainingType == null)
				{
					num4 += current.SourceLines;
				}
			}
			long linesOfCode = (@namespace.IsImplicitlyDeclared ? num4 : MetricsHelper.GetLinesOfCode(@namespace.DeclaringSyntaxReferences, @namespace, context));
			int maintainabilityIndex = ((!children.IsEmpty) ? MetricsHelper.GetAverageRoundedMetricValue(num, children.Length) : 100);
			return new NamespaceMetricData(@namespace, maintainabilityIndex, builder.ToImmutable(), linesOfCode, num2, num3, children);
		}

		private static ImmutableArray<INamespaceOrTypeSymbol> GetChildSymbols(INamespaceSymbol @namespace)
		{
			HashSet<INamedTypeSymbol> typesInNamespace = new HashSet<INamedTypeSymbol>();
			ImmutableArray<INamedTypeSymbol>.Enumerator enumerator = @namespace.GetTypeMembers().GetEnumerator();
			while (enumerator.MoveNext())
			{
				processType(enumerator.Current);
			}
			ImmutableArray<INamespaceOrTypeSymbol>.Builder builder = ImmutableArray.CreateBuilder<INamespaceOrTypeSymbol>();
			foreach (INamedTypeSymbol item in typesInNamespace.OrderBy((INamedTypeSymbol t) => t.ToDisplayString()))
			{
				builder.Add(item);
			}
			return builder.ToImmutable();
			void processType(INamedTypeSymbol namedType)
			{
				typesInNamespace.Add(namedType);
				ImmutableArray<INamedTypeSymbol>.Enumerator enumerator3 = namedType.GetTypeMembers().GetEnumerator();
				while (enumerator3.MoveNext())
				{
					processType(enumerator3.Current);
				}
			}
		}
	}

	private sealed class PropertyMetricData : CodeAnalysisMetricData
	{
		internal PropertyMetricData(IPropertySymbol symbol, int maintainabilityIndex, ComputationalComplexityMetrics computationalComplexityMetrics, ImmutableHashSet<INamedTypeSymbol> coupledNamedTypes, long linesOfCode, int cyclomaticComplexity, int? depthOfInheritance, ImmutableArray<CodeAnalysisMetricData> children)
			: base(symbol, maintainabilityIndex, computationalComplexityMetrics, coupledNamedTypes, linesOfCode, cyclomaticComplexity, depthOfInheritance, null, children)
		{
		}

		internal static PropertyMetricData Compute(IPropertySymbol property, CodeMetricsAnalysisContext context)
		{
			ImmutableHashSet<INamedTypeSymbol>.Builder builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>();
			ImmutableArray<SyntaxReference> declaringSyntaxReferences = property.DeclaringSyntaxReferences;
			long linesOfCode = MetricsHelper.GetLinesOfCode(declaringSyntaxReferences, property, context);
			var (num, computationalComplexityMetrics) = MetricsHelper.ComputeCoupledTypesAndComplexityExcludingMemberDecls(declaringSyntaxReferences, property, builder, context);
			MetricsHelper.AddCoupledNamedTypes(builder, context.WellKnownTypeProvider, property.Parameters);
			MetricsHelper.AddCoupledNamedTypes(builder, context.WellKnownTypeProvider, property.Type);
			ImmutableArray<CodeAnalysisMetricData> children = ComputeSynchronously(GetAccessors(property), context);
			int num2 = 0;
			ImmutableArray<CodeAnalysisMetricData>.Enumerator enumerator = children.GetEnumerator();
			while (enumerator.MoveNext())
			{
				CodeAnalysisMetricData current = enumerator.Current;
				MetricsHelper.AddCoupledNamedTypes(builder, context.WellKnownTypeProvider, current.CoupledNamedTypes);
				num2 += current.MaintainabilityIndex;
				num += current.CyclomaticComplexity;
				computationalComplexityMetrics = computationalComplexityMetrics.Union(current.ComputationalComplexityMetrics);
			}
			int? depthOfInheritance = null;
			int maintainabilityIndex = ((!children.IsEmpty) ? MetricsHelper.GetAverageRoundedMetricValue(num2, children.Length) : 100);
			MetricsHelper.RemoveContainingTypes(property, builder);
			return new PropertyMetricData(property, maintainabilityIndex, computationalComplexityMetrics, builder.ToImmutable(), linesOfCode, num, depthOfInheritance, children);
		}

		private static IEnumerable<IMethodSymbol> GetAccessors(IPropertySymbol property)
		{
			if (property.GetMethod != null)
			{
				yield return property.GetMethod;
			}
			if (property.SetMethod != null)
			{
				yield return property.SetMethod;
			}
		}
	}

	/// <summary>
	/// Symbol corresponding to the metric data.
	/// </summary>
	public ISymbol Symbol { get; }

	internal ComputationalComplexityMetrics ComputationalComplexityMetrics { get; }

	/// <summary>
	/// Indicates an index value between 0 and 100 that represents the relative ease of maintaining the code.
	/// A high value means better maintainability.
	/// </summary>
	public int MaintainabilityIndex { get; }

	/// <summary>
	/// Indicates the coupling to unique named types through parameters, local variables, return types, method calls,
	/// generic or template instantiations, base classes, interface implementations, fields defined on external types, and attribute decoration.
	/// Good software design dictates that types and methods should have high cohesion and low coupling.
	/// High coupling indicates a design that is difficult to reuse and maintain because of its many interdependencies on other types.
	/// </summary>
	public ImmutableHashSet<INamedTypeSymbol> CoupledNamedTypes { get; }

	/// <summary>
	/// Indicates the exact number of lines in source code file.
	/// </summary>
	public long SourceLines { get; }

	/// <summary>
	/// Indicates the approximate number of executable statements/lines in code.
	/// The count is based on the executable <see cref="T:Microsoft.CodeAnalysis.IOperation" />s in code and is therefore not the exact number of lines in the source code file.
	/// A high count might indicate that a type or method is trying to do too much work and should be split up.
	/// It might also indicate that the type or method might be hard to maintain.
	/// </summary>
	public long ExecutableLines { get; }

	/// <summary>
	/// Measures the structural complexity of the code.
	/// It is created by calculating the number of different code paths in the flow of the program.
	/// A program that has complex control flow requires more tests to achieve good code coverage and is less maintainable.
	/// </summary>
	public int CyclomaticComplexity { get; }

	/// <summary>
	/// Indicates the number of different classes that inherit from one another, all the way back to the base class.
	/// Depth of Inheritance is similar to class coupling in that a change in a base class can affect any of its inherited classes.
	/// The higher this number, the deeper the inheritance and the higher the potential for base class modifications to result in a breaking change.
	/// For Depth of Inheritance, a low value is good and a high value is bad.
	/// </summary>
	public int? DepthOfInheritance { get; }

	/// <summary>
	/// Lack of Cohesion of Methods, from 0 to 1.
	/// Low value is good.
	/// High value (generally greater than or equal to 0.8) is bad.
	/// </summary>
	public double? LackOfCohesionOfMethods { get; }

	/// <summary>
	/// Array of code metrics data for symbolic children of <see cref="P:Microsoft.CodeAnalysis.CodeMetrics.CodeAnalysisMetricData.Symbol" />, if any.
	/// </summary>
	public ImmutableArray<CodeAnalysisMetricData> Children { get; }

	internal CodeAnalysisMetricData(ISymbol symbol, int maintainabilityIndex, ComputationalComplexityMetrics computationalComplexityMetrics, ImmutableHashSet<INamedTypeSymbol> coupledNamedTypes, long linesOfCode, int cyclomaticComplexity, int? depthOfInheritance, double? lcom, ImmutableArray<CodeAnalysisMetricData> children)
	{
		long num = ((!computationalComplexityMetrics.IsDefault) ? computationalComplexityMetrics.ExecutableLines : children.Sum((CodeAnalysisMetricData c) => c.ExecutableLines));
		Symbol = symbol;
		MaintainabilityIndex = maintainabilityIndex;
		ComputationalComplexityMetrics = computationalComplexityMetrics;
		CoupledNamedTypes = coupledNamedTypes;
		SourceLines = linesOfCode;
		ExecutableLines = num;
		CyclomaticComplexity = cyclomaticComplexity;
		DepthOfInheritance = depthOfInheritance;
		LackOfCohesionOfMethods = lcom;
		Children = children;
	}

	/// <summary>
	/// Computes string representation of metrics data.
	/// </summary>
	public sealed override string ToString()
	{
		StringBuilder builder = new StringBuilder();
		string text;
		switch (Symbol.Kind)
		{
		case SymbolKind.Assembly:
			text = "Assembly";
			break;
		case SymbolKind.Namespace:
			if (((INamespaceSymbol)Symbol).IsGlobalNamespace)
			{
				appendChildren(string.Empty);
				return builder.ToString();
			}
			text = Symbol.Name;
			break;
		case SymbolKind.NamedType:
		{
			text = Symbol.ToDisplayString();
			int num = text.LastIndexOf(".", StringComparison.OrdinalIgnoreCase);
			if (num >= 0 && num < text.Length)
			{
				string text2 = text;
				int num2 = num + 1;
				text = text2.Substring(num2, text2.Length - num2);
			}
			break;
		}
		default:
			text = Symbol.ToDisplayString();
			break;
		}
		StringBuilder stringBuilder = builder;
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(52, 5, stringBuilder);
		handler.AppendFormatted(text);
		handler.AppendLiteral(": (Lines: ");
		handler.AppendFormatted(SourceLines);
		handler.AppendLiteral(", ExecutableLines: ");
		handler.AppendFormatted(ExecutableLines);
		handler.AppendLiteral(", MntIndex: ");
		handler.AppendFormatted(MaintainabilityIndex);
		handler.AppendLiteral(", CycCxty: ");
		handler.AppendFormatted(CyclomaticComplexity);
		stringBuilder2.Append(ref handler);
		if (!CoupledNamedTypes.IsEmpty)
		{
			string value = string.Join(", ", from t in CoupledNamedTypes
				select t.ToDisplayString() into n
				orderby n
				select n);
			stringBuilder = builder;
			StringBuilder stringBuilder3 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(18, 1, stringBuilder);
			handler.AppendLiteral(", CoupledTypes: {");
			handler.AppendFormatted(value);
			handler.AppendLiteral("}");
			stringBuilder3.Append(ref handler);
		}
		if (DepthOfInheritance.HasValue)
		{
			stringBuilder = builder;
			StringBuilder stringBuilder4 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(16, 1, stringBuilder);
			handler.AppendLiteral(", DepthInherit: ");
			handler.AppendFormatted(DepthOfInheritance);
			stringBuilder4.Append(ref handler);
		}
		builder.Append(')');
		appendChildren("   ");
		return builder.ToString();
		void appendChildren(string indent)
		{
			ImmutableArray<CodeAnalysisMetricData>.Enumerator enumerator = Children.GetEnumerator();
			while (enumerator.MoveNext())
			{
				string[] array = enumerator.Current.ToString().Split(new string[1] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
				foreach (string value2 in array)
				{
					builder.AppendLine();
					StringBuilder stringBuilder5 = builder;
					StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(0, 2, stringBuilder5);
					handler2.AppendFormatted(indent);
					handler2.AppendFormatted(value2);
					stringBuilder5.Append(ref handler2);
				}
			}
		}
	}

	/// <summary>
	/// Computes <see cref="T:Microsoft.CodeAnalysis.CodeMetrics.CodeAnalysisMetricData" /> for the given <paramref name="compilation" />.
	/// </summary>
	[Obsolete("Use ComputeAsync(CodeMetricsAnalysisContext) instead.")]
	public static Task<CodeAnalysisMetricData> ComputeAsync(Compilation compilation, CancellationToken cancellationToken)
	{
		if (compilation == null)
		{
			throw new ArgumentNullException("compilation");
		}
		return ComputeAsync(compilation.Assembly, new CodeMetricsAnalysisContext(compilation, cancellationToken));
	}

	/// <summary>
	/// Computes <see cref="T:Microsoft.CodeAnalysis.CodeMetrics.CodeAnalysisMetricData" /> for the given <paramref name="context" />.
	/// </summary>
	public static Task<CodeAnalysisMetricData> ComputeAsync(CodeMetricsAnalysisContext context)
	{
		if (context == null)
		{
			throw new ArgumentNullException("context");
		}
		return ComputeAsync(context.Compilation.Assembly, context);
	}

	/// <summary>
	/// Computes <see cref="T:Microsoft.CodeAnalysis.CodeMetrics.CodeAnalysisMetricData" /> for the given <paramref name="context" />.
	/// </summary>
	public static CodeAnalysisMetricData ComputeSynchronously(CodeMetricsAnalysisContext context)
	{
		if (context == null)
		{
			throw new ArgumentNullException("context");
		}
		return ComputeSynchronously(context.Compilation.Assembly, context);
	}

	/// <summary>
	/// Computes <see cref="T:Microsoft.CodeAnalysis.CodeMetrics.CodeAnalysisMetricData" /> for the given <paramref name="symbol" /> from the given <paramref name="compilation" />.
	/// </summary>
	[Obsolete("Use ComputeAsync(ISymbol, CodeMetricsAnalysisContext) instead.")]
	public static Task<CodeAnalysisMetricData> ComputeAsync(ISymbol symbol, Compilation compilation, CancellationToken cancellationToken)
	{
		if (symbol == null)
		{
			throw new ArgumentNullException("symbol");
		}
		if (compilation == null)
		{
			throw new ArgumentNullException("compilation");
		}
		return ComputeAsync(symbol, new CodeMetricsAnalysisContext(compilation, cancellationToken));
	}

	/// <summary>
	/// Computes <see cref="T:Microsoft.CodeAnalysis.CodeMetrics.CodeAnalysisMetricData" /> for the given <paramref name="symbol" /> from the given <paramref name="context" />.
	/// </summary>
	public static Task<CodeAnalysisMetricData> ComputeAsync(ISymbol symbol, CodeMetricsAnalysisContext context)
	{
		if (symbol == null)
		{
			throw new ArgumentNullException("symbol");
		}
		if (context == null)
		{
			throw new ArgumentNullException("context");
		}
		if (context.CancellationToken.IsCancellationRequested)
		{
			return Task.FromCanceled<CodeAnalysisMetricData>(context.CancellationToken);
		}
		return ComputeAsync(symbol, context);
		/// <summary>
		/// Computes <see cref="T:Microsoft.CodeAnalysis.CodeMetrics.CodeAnalysisMetricData" /> for the given <paramref name="symbol" /> from the given <paramref name="context" />.
		/// </summary>
		static async Task<CodeAnalysisMetricData> ComputeAsync(ISymbol symbol, CodeMetricsAnalysisContext context)
		{
			return symbol.Kind switch
			{
				SymbolKind.Assembly => await AssemblyMetricData.ComputeAsync((IAssemblySymbol)symbol, context).ConfigureAwait(continueOnCapturedContext: false), 
				SymbolKind.Namespace => await NamespaceMetricData.ComputeAsync((INamespaceSymbol)symbol, context).ConfigureAwait(continueOnCapturedContext: false), 
				SymbolKind.NamedType => await NamedTypeMetricData.ComputeAsync((INamedTypeSymbol)symbol, context).ConfigureAwait(continueOnCapturedContext: false), 
				SymbolKind.Method => MethodMetricData.Compute((IMethodSymbol)symbol, context), 
				SymbolKind.Property => PropertyMetricData.Compute((IPropertySymbol)symbol, context), 
				SymbolKind.Field => FieldMetricData.Compute((IFieldSymbol)symbol, context), 
				SymbolKind.Event => EventMetricData.Compute((IEventSymbol)symbol, context), 
				_ => throw new NotSupportedException(), 
			};
		}
	}

	/// <summary>
	/// Computes <see cref="T:Microsoft.CodeAnalysis.CodeMetrics.CodeAnalysisMetricData" /> for the given <paramref name="symbol" /> from the given <paramref name="context" />.
	/// </summary>
	public static CodeAnalysisMetricData ComputeSynchronously(ISymbol symbol, CodeMetricsAnalysisContext context)
	{
		if (symbol == null)
		{
			throw new ArgumentNullException("symbol");
		}
		if (context == null)
		{
			throw new ArgumentNullException("context");
		}
		context.CancellationToken.ThrowIfCancellationRequested();
		return symbol.Kind switch
		{
			SymbolKind.Assembly => AssemblyMetricData.ComputeSynchronously((IAssemblySymbol)symbol, context), 
			SymbolKind.Namespace => NamespaceMetricData.ComputeSynchronously((INamespaceSymbol)symbol, context), 
			SymbolKind.NamedType => NamedTypeMetricData.ComputeSynchronously((INamedTypeSymbol)symbol, context), 
			SymbolKind.Method => MethodMetricData.Compute((IMethodSymbol)symbol, context), 
			SymbolKind.Property => PropertyMetricData.Compute((IPropertySymbol)symbol, context), 
			SymbolKind.Field => FieldMetricData.Compute((IFieldSymbol)symbol, context), 
			SymbolKind.Event => EventMetricData.Compute((IEventSymbol)symbol, context), 
			_ => throw new NotSupportedException(), 
		};
	}

	internal static async Task<ImmutableArray<CodeAnalysisMetricData>> ComputeAsync(IEnumerable<ISymbol> children, CodeMetricsAnalysisContext context)
	{
		CodeMetricsAnalysisContext context2 = context;
		return (await Task.WhenAll(from child in children
			where !child.IsImplicitlyDeclared || (child is INamespaceSymbol namespaceSymbol && namespaceSymbol.IsGlobalNamespace)
			select Task.Run(() => ComputeAsync(child, context2))).ConfigureAwait(continueOnCapturedContext: false)).ToImmutableArray();
	}

	internal static ImmutableArray<CodeAnalysisMetricData> ComputeSynchronously(IEnumerable<ISymbol> children, CodeMetricsAnalysisContext context)
	{
		CodeMetricsAnalysisContext context2 = context;
		return (from child in children
			where !child.IsImplicitlyDeclared || (child is INamespaceSymbol namespaceSymbol && namespaceSymbol.IsGlobalNamespace)
			select ComputeSynchronously(child, context2)).ToImmutableArray();
	}
}
