using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities.Extensions;

internal static class ITypeSymbolExtensions
{
	public static bool IsAssignableTo([NotNullWhen(true)] this ITypeSymbol? fromSymbol, [NotNullWhen(true)] ITypeSymbol? toSymbol, Compilation compilation)
	{
		if (fromSymbol != null && toSymbol != null)
		{
			return compilation.ClassifyCommonConversion(fromSymbol, toSymbol).IsImplicit;
		}
		return false;
	}

	public static bool IsPrimitiveType(this ITypeSymbol type)
	{
		SpecialType specialType = type.SpecialType;
		if ((uint)(specialType - 7) <= 9u || (uint)(specialType - 18) <= 2u)
		{
			return true;
		}
		return false;
	}

	public static bool Inherits([NotNullWhen(true)] this ITypeSymbol? type, [NotNullWhen(true)] ITypeSymbol? possibleBase)
	{
		if (type == null || possibleBase == null)
		{
			return false;
		}
		switch (possibleBase.TypeKind)
		{
		case TypeKind.Class:
			if (type.TypeKind == TypeKind.Interface)
			{
				return false;
			}
			return type.DerivesFrom(possibleBase, baseTypesOnly: true);
		case TypeKind.Interface:
			return type.DerivesFrom(possibleBase);
		default:
			return false;
		}
	}

	public static IEnumerable<INamedTypeSymbol> GetBaseTypes(this ITypeSymbol type, Func<INamedTypeSymbol, bool>? takeWhilePredicate = null)
	{
		INamedTypeSymbol current = type.BaseType;
		while (current != null && (takeWhilePredicate == null || takeWhilePredicate(current)))
		{
			yield return current;
			current = current.BaseType;
		}
	}

	public static IEnumerable<ITypeSymbol> GetBaseTypesAndThis(this ITypeSymbol type)
	{
		for (ITypeSymbol current = type; current != null; current = current.BaseType)
		{
			yield return current;
		}
	}

	public static bool DerivesFrom([NotNullWhen(true)] this ITypeSymbol? symbol, [NotNullWhen(true)] ITypeSymbol? candidateBaseType, bool baseTypesOnly = false, bool checkTypeParameterConstraints = true)
	{
		if (candidateBaseType == null || symbol == null)
		{
			return false;
		}
		if (!baseTypesOnly && candidateBaseType.TypeKind == TypeKind.Interface)
		{
			IEnumerable<ITypeSymbol> source = symbol.AllInterfaces.OfType<ITypeSymbol>();
			if (SymbolEqualityComparer.Default.Equals(candidateBaseType.OriginalDefinition, candidateBaseType))
			{
				source = source.Select((ITypeSymbol i) => i.OriginalDefinition);
			}
			if (source.Contains<ITypeSymbol>(candidateBaseType))
			{
				return true;
			}
		}
		if (checkTypeParameterConstraints && symbol.TypeKind == TypeKind.TypeParameter)
		{
			ImmutableArray<ITypeSymbol>.Enumerator enumerator = ((ITypeParameterSymbol)symbol).ConstraintTypes.GetEnumerator();
			while (enumerator.MoveNext())
			{
				if (enumerator.Current.DerivesFrom(candidateBaseType, baseTypesOnly, checkTypeParameterConstraints))
				{
					return true;
				}
			}
		}
		while (symbol != null)
		{
			if (SymbolEqualityComparer.Default.Equals(symbol, candidateBaseType))
			{
				return true;
			}
			symbol = symbol.BaseType;
		}
		return false;
	}

	/// <summary>
	/// Indicates if the given <paramref name="type" /> is disposable,
	/// and thus can be used in a <code>using</code> or <code>await using</code> statement.
	/// </summary>
	public static bool IsDisposable(this ITypeSymbol type, INamedTypeSymbol? iDisposable, INamedTypeSymbol? iAsyncDisposable, INamedTypeSymbol? configuredAsyncDisposable)
	{
		if (type.IsReferenceType)
		{
			if (!IsInterfaceOrImplementsInterface(type, iDisposable))
			{
				return IsInterfaceOrImplementsInterface(type, iAsyncDisposable);
			}
			return true;
		}
		if (SymbolEqualityComparer.Default.Equals(type, configuredAsyncDisposable))
		{
			return true;
		}
		if (type.IsRefLikeType)
		{
			return type.GetMembers("Dispose").OfType<IMethodSymbol>().Any((IMethodSymbol method) => method.HasDisposeSignatureByConvention());
		}
		return false;
		static bool IsInterfaceOrImplementsInterface(ITypeSymbol type, INamedTypeSymbol? interfaceType)
		{
			if (interfaceType != null)
			{
				if (!SymbolEqualityComparer.Default.Equals(type, interfaceType))
				{
					return type.AllInterfaces.Contains(interfaceType);
				}
				return true;
			}
			return false;
		}
	}

	/// <summary>
	/// Gets all attributes directly applied to the type or inherited from a base type.
	/// </summary>
	/// <param name="type">The type symbol.</param>
	/// <param name="attributeUsageAttribute">The compilation symbol for <see cref="T:System.AttributeUsageAttribute" />.</param>
	public static IEnumerable<AttributeData> GetApplicableAttributes(this INamedTypeSymbol type, INamedTypeSymbol? attributeUsageAttribute)
	{
		List<AttributeData> list = new List<AttributeData>();
		bool flag = false;
		while (type != null)
		{
			ImmutableArray<AttributeData> attributes = type.GetAttributes();
			if (!flag || attributeUsageAttribute == null)
			{
				list.AddRange(attributes);
			}
			else
			{
				ImmutableArray<AttributeData>.Enumerator enumerator = attributes.GetEnumerator();
				while (enumerator.MoveNext())
				{
					AttributeData current = enumerator.Current;
					if (IsInheritedAttribute(current, attributeUsageAttribute))
					{
						list.Add(current);
					}
				}
			}
			type = type.BaseType;
			flag = true;
		}
		return list;
		static bool IsInheritedAttribute(AttributeData attributeData, INamedTypeSymbol attributeUsageAttribute)
		{
			for (INamedTypeSymbol namedTypeSymbol = attributeData.AttributeClass; namedTypeSymbol != null; namedTypeSymbol = namedTypeSymbol.BaseType)
			{
				ImmutableArray<AttributeData>.Enumerator enumerator2 = namedTypeSymbol.GetAttributes().GetEnumerator();
				while (enumerator2.MoveNext())
				{
					AttributeData current2 = enumerator2.Current;
					if (SymbolEqualityComparer.Default.Equals(current2.AttributeClass, attributeUsageAttribute))
					{
						ImmutableArray<KeyValuePair<string, TypedConstant>>.Enumerator enumerator3 = current2.NamedArguments.GetEnumerator();
						while (enumerator3.MoveNext())
						{
							var (text2, typedConstant2) = enumerator3.Current;
							if (!(text2 != "Inherited"))
							{
								return !object.Equals(false, typedConstant2.Value);
							}
						}
						return true;
					}
				}
			}
			return true;
		}
	}

	public static IEnumerable<AttributeData> GetApplicableExportAttributes(this INamedTypeSymbol? type, INamedTypeSymbol? exportAttributeV1, INamedTypeSymbol? exportAttributeV2, INamedTypeSymbol? inheritedExportAttribute)
	{
		List<AttributeData> list = new List<AttributeData>();
		bool flag = false;
		while (type != null)
		{
			ImmutableArray<AttributeData>.Enumerator enumerator = type.GetAttributes().GetEnumerator();
			while (enumerator.MoveNext())
			{
				AttributeData current = enumerator.Current;
				if (current.AttributeClass.Inherits(inheritedExportAttribute))
				{
					list.Add(current);
				}
				else if (!flag && (current.AttributeClass.Inherits(exportAttributeV1) || current.AttributeClass.Inherits(exportAttributeV2)))
				{
					list.Add(current);
				}
			}
			if (inheritedExportAttribute == null)
			{
				break;
			}
			type = type.BaseType;
			flag = true;
		}
		return list;
	}

	public static bool IsAttribute(this ITypeSymbol symbol)
	{
		for (INamedTypeSymbol baseType = symbol.BaseType; baseType != null; baseType = baseType.BaseType)
		{
			if (baseType.MetadataName == "Attribute" && baseType.ContainingType == null && baseType.ContainingNamespace != null && baseType.ContainingNamespace.Name == "System" && baseType.ContainingNamespace.ContainingNamespace != null && baseType.ContainingNamespace.ContainingNamespace.IsGlobalNamespace)
			{
				return true;
			}
		}
		return false;
	}

	public static bool HasValueCopySemantics(this ITypeSymbol typeSymbol)
	{
		if (!typeSymbol.IsValueType)
		{
			return typeSymbol.SpecialType == SpecialType.System_String;
		}
		return true;
	}

	public static bool CanHoldNullValue([NotNullWhen(true)] this ITypeSymbol? typeSymbol)
	{
		if (!typeSymbol.IsReferenceTypeOrNullableValueType() && (typeSymbol == null || !typeSymbol.IsRefLikeType))
		{
			if (typeSymbol is ITypeParameterSymbol typeParameterSymbol)
			{
				return !typeParameterSymbol.IsValueType;
			}
			return false;
		}
		return true;
	}

	public static bool IsNonNullableValueType([NotNullWhen(true)] this ITypeSymbol? typeSymbol)
	{
		if (typeSymbol != null && typeSymbol.IsValueType)
		{
			return typeSymbol.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T;
		}
		return false;
	}

	public static bool IsNullableValueType([NotNullWhen(true)] this ITypeSymbol? typeSymbol)
	{
		if (typeSymbol != null && typeSymbol.IsValueType)
		{
			return typeSymbol.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
		}
		return false;
	}

	public static bool IsReferenceTypeOrNullableValueType([NotNullWhen(true)] this ITypeSymbol? typeSymbol)
	{
		if (typeSymbol != null)
		{
			if (!typeSymbol.IsReferenceType)
			{
				return typeSymbol.IsNullableValueType();
			}
			return true;
		}
		return false;
	}

	public static bool IsNullableOfBoolean([NotNullWhen(true)] this ITypeSymbol? typeSymbol)
	{
		if (typeSymbol.IsNullableValueType())
		{
			return ((INamedTypeSymbol)typeSymbol).TypeArguments[0].SpecialType == SpecialType.System_Boolean;
		}
		return false;
	}

	public static ITypeSymbol? GetNullableValueTypeUnderlyingType(this ITypeSymbol? typeSymbol)
	{
		if (!typeSymbol.IsNullableValueType())
		{
			return null;
		}
		return ((INamedTypeSymbol)typeSymbol).TypeArguments[0];
	}

	public static ITypeSymbol? GetUnderlyingValueTupleTypeOrThis(this ITypeSymbol? typeSymbol)
	{
		ITypeSymbol typeSymbol2 = (typeSymbol as INamedTypeSymbol)?.TupleUnderlyingType;
		return typeSymbol2 ?? typeSymbol;
	}

	/// <summary>
	/// Checks whether the current type contains one of the following count property:
	///     - <see cref="P:System.Collections.ICollection.Count" />
	///     - <see cref="P:System.Collections.Generic.ICollection`1.Count" />
	///     - <see cref="P:System.Collections.Generic.IReadOnlyCollection`1.Count" />
	/// </summary>
	/// <param name="invocationTarget">The type to check</param>
	/// <param name="wellKnownTypeProvider">An instance of the <see cref="T:Analyzer.Utilities.WellKnownTypeProvider" /> used to access the three described known types.</param>
	/// <returns><c>true</c> when the type contains one of the supported collection count property; otherwise <c>false</c>.</returns>
	public static bool HasAnyCollectionCountProperty([NotNullWhen(true)] this ITypeSymbol? invocationTarget, WellKnownTypeProvider wellKnownTypeProvider)
	{
		if (invocationTarget == null || !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName("System.Collections.ICollection", out INamedTypeSymbol iCollection) || !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName("System.Collections.Generic.ICollection`1", out INamedTypeSymbol iCollectionOfT) || !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName("System.Collections.Generic.IReadOnlyCollection`1", out INamedTypeSymbol iReadOnlyCollectionOfT))
		{
			return false;
		}
		if (isAnySupportedCollectionType(invocationTarget))
		{
			return true;
		}
		if (invocationTarget.TypeKind == TypeKind.Interface)
		{
			if (invocationTarget.GetMembers("Count").OfType<IPropertySymbol>().Any())
			{
				return false;
			}
			ImmutableArray<INamedTypeSymbol>.Enumerator enumerator = invocationTarget.AllInterfaces.GetEnumerator();
			while (enumerator.MoveNext())
			{
				if (isAnySupportedCollectionType(enumerator.Current))
				{
					return true;
				}
			}
		}
		else
		{
			ImmutableArray<INamedTypeSymbol>.Enumerator enumerator = invocationTarget.AllInterfaces.GetEnumerator();
			while (enumerator.MoveNext())
			{
				INamedTypeSymbol current = enumerator.Current;
				if (isAnySupportedCollectionType(current) && invocationTarget.FindImplementationForInterfaceMember(current.GetMembers("Count")[0]) is IPropertySymbol propertySymbol && !propertySymbol.ExplicitInterfaceImplementations.Any())
				{
					return true;
				}
			}
		}
		return false;
		bool isAnySupportedCollectionType(ITypeSymbol type)
		{
			if (type.OriginalDefinition is INamedTypeSymbol y)
			{
				if (!SymbolEqualityComparer.Default.Equals(iCollection, y) && !SymbolEqualityComparer.Default.Equals(iCollectionOfT, y))
				{
					return SymbolEqualityComparer.Default.Equals(iReadOnlyCollectionOfT, y);
				}
				return true;
			}
			return false;
		}
	}
}
