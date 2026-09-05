using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities.Extensions;

internal static class IEnumerableOfIMethodSymbolExtensions
{
	/// <summary>
	/// Excludes <paramref name="methods" /> that have an attribute that precisely matches <paramref name="attributeType" />.
	/// </summary>
	/// <param name="methods">List of <see cref="T:Microsoft.CodeAnalysis.IMethodSymbol" /> to filter.</param>
	/// <param name="attributeType">The <see cref="T:Microsoft.CodeAnalysis.INamedTypeSymbol" /> of the attribute class to search.</param>
	/// <returns>A filtered list of methods.</returns>
	public static IEnumerable<IMethodSymbol> WhereMethodDoesNotContainAttribute(this IEnumerable<IMethodSymbol> methods, INamedTypeSymbol? attributeType)
	{
		INamedTypeSymbol attributeType2 = attributeType;
		if (attributeType2 == null)
		{
			return methods;
		}
		return methods.Where((IMethodSymbol m) => !m.HasAnyAttribute(attributeType2));
	}

	/// <summary>
	/// Returns a list of method symbols from a given list of the method symbols, which has its parameter type as
	/// expectedParameterType as its first parameter or the last parameter in addition to matching all the other 
	/// parameter types of the selectedOverload method symbol
	/// </summary>
	/// <param name="methods">List of <see cref="T:Microsoft.CodeAnalysis.IMethodSymbol" /> to scan for possible overloads</param>
	/// <param name="selectedOverload"><see cref="T:Microsoft.CodeAnalysis.IMethodSymbol" /> that is currently picked by the user</param>
	/// <param name="expectedParameterType"><see cref="T:Microsoft.CodeAnalysis.INamedTypeSymbol" /> type of the leading parameter or the trailing parameter</param>
	/// <param name="trailingOnly"><see cref="T:Microsoft.CodeAnalysis.INamedTypeSymbol" /> If the expected parameter should appear at the trailing position of the parameter list of the method overload</param>
	public static IEnumerable<IMethodSymbol> GetMethodOverloadsWithDesiredParameterAtLeadingOrTrailing(this IEnumerable<IMethodSymbol> methods, IMethodSymbol selectedOverload, INamedTypeSymbol expectedParameterType, bool trailingOnly = false)
	{
		IMethodSymbol selectedOverload2 = selectedOverload;
		INamedTypeSymbol expectedParameterType2 = expectedParameterType;
		return methods.Where(delegate(IMethodSymbol candidateMethod)
		{
			if (!System.Collections.Immutable.ImmutableArrayExtensions.HasExactly(candidateMethod.Parameters, selectedOverload2.Parameters.Length + 1))
			{
				return false;
			}
			int num = 0;
			if (!trailingOnly && candidateMethod.Parameters.First().Type.Equals(expectedParameterType2) && candidateMethod.Parameters[0].RefKind == RefKind.None)
			{
				num = 1;
			}
			else
			{
				IParameterSymbol parameterSymbol = candidateMethod.Parameters.Last();
				if (!parameterSymbol.Type.Equals(expectedParameterType2) || parameterSymbol.RefKind != 0)
				{
					return false;
				}
			}
			int num2 = 0;
			while (num2 < selectedOverload2.Parameters.Length)
			{
				if (!selectedOverload2.Parameters[num2].Type.Equals(candidateMethod.Parameters[num].Type) || selectedOverload2.Parameters[num2].IsParams != candidateMethod.Parameters[num].IsParams || selectedOverload2.Parameters[num2].RefKind != candidateMethod.Parameters[num].RefKind)
				{
					return false;
				}
				num2++;
				num++;
			}
			return true;
		});
	}

	/// <summary>
	/// Returns a list of method symbols from a given list of the method symbols, which has its parameter type as
	/// expectedParameterType as its last parameter in addition to matching all the other parameter types of the 
	/// selectedOverload method symbol
	/// </summary>
	/// <param name="methods">List of <see cref="T:Microsoft.CodeAnalysis.IMethodSymbol" /> to scan for possible overloads</param>
	/// <param name="selectedOverload"><see cref="T:Microsoft.CodeAnalysis.IMethodSymbol" /> that is currently picked by the user</param>
	/// <param name="expectedTrailingParameterType"><see cref="T:Microsoft.CodeAnalysis.INamedTypeSymbol" /> type of the leading parameter or the trailing parameter</param>
	public static IEnumerable<IMethodSymbol> GetMethodOverloadsWithDesiredParameterAtTrailing(this IEnumerable<IMethodSymbol> methods, IMethodSymbol selectedOverload, INamedTypeSymbol expectedTrailingParameterType)
	{
		return methods.GetMethodOverloadsWithDesiredParameterAtLeadingOrTrailing(selectedOverload, expectedTrailingParameterType, trailingOnly: true);
	}

	/// <summary>
	/// Gets the <see cref="T:Microsoft.CodeAnalysis.IMethodSymbol" /> in the sequence who's parameters match <paramref name="expectedParameterTypesInOrder" />.
	/// </summary>
	/// <param name="members">The sequence of <see cref="T:Microsoft.CodeAnalysis.IMethodSymbol" />s to search.</param>
	/// <param name="expectedParameterTypesInOrder">The types of the parameters, in order.</param>
	/// <returns>
	/// The first <see cref="T:Microsoft.CodeAnalysis.IMethodSymbol" /> in the sequence who's parameters match <paramref name="expectedParameterTypesInOrder" />, or <langword>null</langword> if
	/// no method was found.
	/// </returns>
	public static IMethodSymbol? GetFirstOrDefaultMemberWithParameterTypes(this IEnumerable<IMethodSymbol>? members, params ITypeSymbol[] expectedParameterTypesInOrder)
	{
		ITypeSymbol[] expectedParameterTypesInOrder2 = expectedParameterTypesInOrder;
		return members?.FirstOrDefault(delegate(IMethodSymbol member)
		{
			if (member.Parameters.Length != expectedParameterTypesInOrder2.Length)
			{
				return false;
			}
			for (int i = 0; i < expectedParameterTypesInOrder2.Length; i++)
			{
				ITypeSymbol type = member.Parameters[i].Type;
				if (!expectedParameterTypesInOrder2[i].Equals(type))
				{
					return false;
				}
			}
			return true;
		});
	}

	/// <summary>
	/// Given a <see cref="T:System.Collections.Generic.IEnumerable`1" />, this method returns the method symbol which 
	/// matches the expectedParameterTypesInOrder parameter requirement
	/// </summary>
	/// <param name="members"></param>
	/// <param name="expectedParameterTypesInOrder"></param>
	/// <returns></returns>
	public static IMethodSymbol? GetFirstOrDefaultMemberWithParameterInfos(this IEnumerable<IMethodSymbol>? members, params ParameterInfo[] expectedParameterTypesInOrder)
	{
		ParameterInfo[] expectedParameterTypesInOrder2 = expectedParameterTypesInOrder;
		int expectedParameterCount = expectedParameterTypesInOrder2.Length;
		return members?.FirstOrDefault(delegate(IMethodSymbol member)
		{
			if (member.Parameters.Length != expectedParameterCount)
			{
				return false;
			}
			for (int i = 0; i < expectedParameterCount; i++)
			{
				if (i == expectedParameterCount - 1 && member.Parameters[i].IsParams != expectedParameterTypesInOrder2[i].IsParams)
				{
					return false;
				}
				ITypeSymbol typeSymbol = member.Parameters[i].Type;
				if (expectedParameterTypesInOrder2[i].IsArray)
				{
					IArrayTypeSymbol arrayTypeSymbol = typeSymbol as IArrayTypeSymbol;
					if (arrayTypeSymbol?.Rank != expectedParameterTypesInOrder2[i].ArrayRank)
					{
						return false;
					}
					typeSymbol = arrayTypeSymbol.ElementType;
				}
				if (!expectedParameterTypesInOrder2[i].ParameterType.Equals(typeSymbol))
				{
					return false;
				}
			}
			return true;
		});
	}

	/// <summary>
	/// Given an <see cref="T:System.Collections.Generic.IEnumerable`1" />, returns the <see cref="T:Microsoft.CodeAnalysis.IMethodSymbol" /> whose parameter list
	/// matches <paramref name="expectedParameterTypesInOrder" />.
	/// </summary>
	/// <param name="members"></param>
	/// <param name="expectedParameterTypesInOrder">Expected types of the member's parameters.</param>
	/// <returns>
	/// The first member in the sequence whose parameters match <paramref name="expectedParameterTypesInOrder" />, 
	/// or null if no matches are found.
	/// </returns>
	public static IMethodSymbol? GetFirstOrDefaultMemberWithParameterTypes(this IEnumerable<IMethodSymbol>? members, IReadOnlyList<ITypeSymbol> expectedParameterTypesInOrder)
	{
		IReadOnlyList<ITypeSymbol> expectedParameterTypesInOrder2 = expectedParameterTypesInOrder;
		if (members == null)
		{
			return null;
		}
		foreach (IMethodSymbol member in members)
		{
			if (Predicate(member))
			{
				return member;
			}
		}
		return null;
		bool Predicate(IMethodSymbol member)
		{
			if (member.Parameters.Length != expectedParameterTypesInOrder2.Count)
			{
				return false;
			}
			for (int i = 0; i < expectedParameterTypesInOrder2.Count; i++)
			{
				if (!member.Parameters[i].Type.Equals(expectedParameterTypesInOrder2[i]))
				{
					return false;
				}
			}
			return true;
		}
	}
}
