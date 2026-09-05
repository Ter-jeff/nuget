using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Analyzer.Utilities.Lightup;
using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities.Extensions;

internal static class INamedTypeSymbolExtensions
{
	private static readonly Func<INamedTypeSymbol, bool> s_isFileLocal = LightupHelpers.CreateSymbolPropertyAccessor<INamedTypeSymbol, bool>(typeof(INamedTypeSymbol), "IsFileLocal", fallbackResult: false);

	public static bool IsFileLocal(this INamedTypeSymbol symbol)
	{
		return s_isFileLocal(symbol);
	}

	public static IEnumerable<INamedTypeSymbol> GetBaseTypesAndThis(this INamedTypeSymbol type)
	{
		for (INamedTypeSymbol current = type; current != null; current = current.BaseType)
		{
			yield return current;
		}
	}

	/// <summary>
	/// Returns a value indicating whether <paramref name="type" /> derives from, or implements
	/// any generic construction of, the type defined by <paramref name="parentType" />.
	/// </summary>
	/// <remarks>
	/// This method only works when <paramref name="parentType" /> is a definition,
	/// not a constructed type.
	/// </remarks>
	/// <example>
	/// <para>
	/// If <paramref name="parentType" /> is the class <see cref="T:System.Collections.Generic.Stack`1" />, then this
	/// method will return <see langword="true" /> when called on <c>Stack&gt;int&gt;</c>
	/// or any type derived it, because <c>Stack&gt;int&gt;</c> is constructed from
	/// <see cref="T:System.Collections.Generic.Stack`1" />.
	/// </para>
	/// <para>
	/// Similarly, if <paramref name="parentType" /> is the interface <see cref="T:System.Collections.Generic.IList`1" />,
	/// then this method will return <see langword="true" /> for <c>List&gt;int&gt;</c>
	/// or any other class that extends <see cref="T:System.Collections.Generic.IList`1" /> or an class that implements it,
	/// because <c>IList&gt;int&gt;</c> is constructed from <see cref="T:System.Collections.Generic.IList`1" />.
	/// </para>
	/// </example>
	public static bool DerivesFromOrImplementsAnyConstructionOf(this INamedTypeSymbol type, INamedTypeSymbol parentType)
	{
		INamedTypeSymbol parentType2 = parentType;
		if (!parentType2.IsDefinition)
		{
			throw new ArgumentException("The type parentType is not a definition; it is a constructed type", "parentType");
		}
		for (INamedTypeSymbol namedTypeSymbol = type.OriginalDefinition; namedTypeSymbol != null; namedTypeSymbol = namedTypeSymbol.BaseType?.OriginalDefinition)
		{
			if (namedTypeSymbol.Equals(parentType2))
			{
				return true;
			}
		}
		if (type.OriginalDefinition.AllInterfaces.Any((INamedTypeSymbol baseInterface) => baseInterface.OriginalDefinition.Equals(parentType2)))
		{
			return true;
		}
		return false;
	}

	public static bool ImplementsOperator(this INamedTypeSymbol symbol, string op)
	{
		return symbol.GetMembers(op).OfType<IMethodSymbol>().Any((IMethodSymbol m) => m.MethodKind == MethodKind.UserDefinedOperator);
	}

	/// <summary>
	/// Returns a value indicating whether the specified type implements both the
	/// equality and inequality operators.
	/// </summary>
	/// <param name="symbol">
	/// A symbols specifying the type to examine.
	/// </param>
	/// <returns>
	/// true if the type specified by <paramref name="symbol" /> implements both the
	/// equality and inequality operators, otherwise false.
	/// </returns>
	public static bool ImplementsEqualityOperators(this INamedTypeSymbol symbol)
	{
		if (symbol.ImplementsOperator("op_Equality"))
		{
			return symbol.ImplementsOperator("op_Inequality");
		}
		return false;
	}

	public static bool OverridesEquals(this INamedTypeSymbol symbol)
	{
		return symbol.GetMembers("Equals").OfType<IMethodSymbol>().Any((IMethodSymbol m) => m.IsObjectEqualsOverride());
	}

	public static bool OverridesGetHashCode(this INamedTypeSymbol symbol)
	{
		return symbol.GetMembers("GetHashCode").OfType<IMethodSymbol>().Any((IMethodSymbol m) => m.IsGetHashCodeOverride());
	}

	public static bool HasFinalizer(this INamedTypeSymbol symbol)
	{
		return symbol.GetMembers().Any((ISymbol m) => m is IMethodSymbol method && method.IsFinalizer());
	}

	/// <summary>
	/// Returns a value indicating whether the specified symbol is a static
	/// holder type.
	/// </summary>
	/// <param name="symbol">
	/// The symbol being examined.
	/// </param>
	/// <returns>
	/// <see langword="true" /> if <paramref name="symbol" /> is a static holder type;
	/// otherwise <see langword="false" />.
	/// </returns>
	/// <remarks>
	/// A symbol is a static holder type if it is a class with at least one
	/// "qualifying member" (<see cref="M:Analyzer.Utilities.Extensions.INamedTypeSymbolExtensions.IsQualifyingMember(Microsoft.CodeAnalysis.ISymbol)" />) and no
	/// "disqualifying members" (<see cref="M:Analyzer.Utilities.Extensions.INamedTypeSymbolExtensions.IsDisqualifyingMember(Microsoft.CodeAnalysis.ISymbol)" />).
	/// </remarks>
	public static bool IsStaticHolderType(this INamedTypeSymbol symbol)
	{
		if (symbol.TypeKind != TypeKind.Class)
		{
			return false;
		}
		if (symbol.BaseType == null || symbol.BaseType.SpecialType != SpecialType.System_Object || !symbol.AllInterfaces.IsDefaultOrEmpty)
		{
			return false;
		}
		if (symbol.IsSealed && symbol.Language == "C#")
		{
			return false;
		}
		bool flag = false;
		ImmutableArray<ISymbol>.Enumerator enumerator = symbol.GetMembers().GetEnumerator();
		while (enumerator.MoveNext())
		{
			ISymbol current = enumerator.Current;
			if (!current.IsImplicitlyDeclared)
			{
				if (!flag && IsQualifyingMember(current))
				{
					flag = true;
				}
				if (IsDisqualifyingMember(current))
				{
					return false;
				}
			}
		}
		return flag;
	}

	/// <summary>
	/// Returns a value indicating whether the specified symbol qualifies as a
	/// member of a static holder class.
	/// </summary>
	/// <param name="member">
	/// The member being examined.
	/// </param>
	/// <returns>
	/// <see langword="true" /> if <paramref name="member" /> qualifies as a member of
	/// a static holder class; otherwise <see langword="false" />.
	/// </returns>
	private static bool IsQualifyingMember(ISymbol member)
	{
		if (member.IsType())
		{
			return true;
		}
		if (member.IsUserDefinedOperator())
		{
			return false;
		}
		if (member.IsConstructor())
		{
			return false;
		}
		if (member.IsProtected() || member.IsPrivate())
		{
			return false;
		}
		return member.IsStatic;
	}

	/// <summary>
	/// Returns a value indicating whether the presence of the specified symbol
	/// disqualifies a class from being considered a static holder class.
	/// </summary>
	/// <param name="member">
	/// The member being examined.
	/// </param>
	/// <returns>
	/// <see langword="true" /> if the presence of <paramref name="member" /> disqualifies the
	/// current type as a static holder class; otherwise <see langword="false" />.
	/// </returns>
	private static bool IsDisqualifyingMember(ISymbol member)
	{
		if (member.IsUserDefinedOperator())
		{
			return true;
		}
		if (member.IsConversionOperator())
		{
			return true;
		}
		if (member.IsType())
		{
			return false;
		}
		if (!member.IsStatic)
		{
			return !member.IsDefaultConstructor();
		}
		return false;
	}

	public static bool IsBenchmarkOrXUnitTestAttribute(this INamedTypeSymbol attributeClass, ConcurrentDictionary<INamedTypeSymbol, bool> knownTestAttributes, INamedTypeSymbol? benchmarkAttribute, INamedTypeSymbol? xunitFactAttribute)
	{
		if (knownTestAttributes.TryGetValue(attributeClass, out var value))
		{
			return value;
		}
		bool value2 = (xunitFactAttribute != null && attributeClass.DerivesFrom(xunitFactAttribute)) || (benchmarkAttribute != null && attributeClass.DerivesFrom(benchmarkAttribute));
		return knownTestAttributes.GetOrAdd(attributeClass, value2);
	}

	/// <summary>
	/// Check if the given <paramref name="typeSymbol" /> is an implicitly generated type for top level statements.
	/// </summary>
	public static bool IsTopLevelStatementsEntryPointType([NotNullWhen(true)] this INamedTypeSymbol? typeSymbol)
	{
		return typeSymbol?.GetMembers().OfType<IMethodSymbol>().Any((IMethodSymbol m) => m.IsTopLevelStatementsEntryPointMethod()) ?? false;
	}
}
