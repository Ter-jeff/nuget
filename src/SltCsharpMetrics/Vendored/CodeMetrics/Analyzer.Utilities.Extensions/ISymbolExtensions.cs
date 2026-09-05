using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities.Extensions;

internal static class ISymbolExtensions
{
	private static readonly SymbolDisplayFormat s_memberDisplayFormat = SymbolDisplayFormat.CSharpShortErrorMessageFormat.WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

	public static bool IsType([NotNullWhen(true)] this ISymbol? symbol)
	{
		if (symbol is ITypeSymbol typeSymbol)
		{
			return typeSymbol.IsType;
		}
		return false;
	}

	public static bool IsAccessorMethod([NotNullWhen(true)] this ISymbol? symbol)
	{
		if (symbol is IMethodSymbol method)
		{
			if (!method.IsPropertyAccessor())
			{
				return method.IsEventAccessor();
			}
			return true;
		}
		return false;
	}

	public static IEnumerable<IMethodSymbol> GetAccessors(this ISymbol symbol)
	{
		switch (symbol.Kind)
		{
		case SymbolKind.Property:
		{
			IPropertySymbol property = (IPropertySymbol)symbol;
			if (property.GetMethod != null)
			{
				yield return property.GetMethod;
			}
			if (property.SetMethod != null)
			{
				yield return property.SetMethod;
			}
			break;
		}
		case SymbolKind.Event:
		{
			IEventSymbol eventSymbol = (IEventSymbol)symbol;
			if (eventSymbol.AddMethod != null)
			{
				yield return eventSymbol.AddMethod;
			}
			if (eventSymbol.RemoveMethod != null)
			{
				yield return eventSymbol.RemoveMethod;
			}
			if (eventSymbol.RaiseMethod != null)
			{
				yield return eventSymbol.RaiseMethod;
			}
			break;
		}
		}
	}

	public static bool IsDefaultConstructor([NotNullWhen(true)] this ISymbol? symbol)
	{
		if (symbol.IsConstructor())
		{
			return symbol.GetParameters().IsEmpty;
		}
		return false;
	}

	public static bool IsPublic(this ISymbol symbol)
	{
		return symbol.DeclaredAccessibility == Accessibility.Public;
	}

	public static bool IsProtected(this ISymbol symbol)
	{
		return symbol.DeclaredAccessibility == Accessibility.Protected;
	}

	public static bool IsPrivate(this ISymbol symbol)
	{
		return symbol.DeclaredAccessibility == Accessibility.Private;
	}

	public static bool IsErrorType([NotNullWhen(true)] this ISymbol? symbol)
	{
		if (symbol is ITypeSymbol typeSymbol)
		{
			return typeSymbol.TypeKind == TypeKind.Error;
		}
		return false;
	}

	public static bool IsConstructor([NotNullWhen(true)] this ISymbol? symbol)
	{
		if (symbol is IMethodSymbol methodSymbol)
		{
			return methodSymbol.MethodKind == MethodKind.Constructor;
		}
		return false;
	}

	public static bool IsImplicitConstructor([NotNullWhen(true)] this ISymbol? symbol)
	{
		if (symbol.IsConstructor())
		{
			return symbol.IsImplicitlyDeclared;
		}
		return false;
	}

	public static bool IsDestructor([NotNullWhen(true)] this ISymbol? symbol)
	{
		return (symbol as IMethodSymbol)?.IsFinalizer() ?? false;
	}

	public static bool IsIndexer([NotNullWhen(true)] this ISymbol? symbol)
	{
		if (symbol is IPropertySymbol propertySymbol)
		{
			return propertySymbol.IsIndexer;
		}
		return false;
	}

	public static bool IsPropertyWithBackingField([NotNullWhen(true)] this ISymbol? symbol, [NotNullWhen(true)] out IFieldSymbol? backingField)
	{
		if (symbol is IPropertySymbol propertySymbol)
		{
			ImmutableArray<ISymbol>.Enumerator enumerator = propertySymbol.ContainingType.GetMembers().GetEnumerator();
			while (enumerator.MoveNext())
			{
				if (enumerator.Current is IFieldSymbol { IsImplicitlyDeclared: not false } fieldSymbol && object.Equals(fieldSymbol.AssociatedSymbol, propertySymbol))
				{
					backingField = fieldSymbol;
					return true;
				}
			}
		}
		backingField = null;
		return false;
	}

	/// <summary>
	/// Determines if the given symbol is a backing field for a property.
	/// </summary>
	/// <param name="symbol">This symbol to check.</param>
	/// <param name="propertySymbol">The property that this field symbol is backing.</param>
	/// <returns>True if the given symbol is a backing field for a property, false otherwise.</returns>
	public static bool IsBackingFieldForProperty([NotNullWhen(true)] this ISymbol? symbol, [NotNullWhen(true)] out IPropertySymbol? propertySymbol)
	{
		if (symbol is IFieldSymbol { IsImplicitlyDeclared: not false, AssociatedSymbol: IPropertySymbol associatedSymbol })
		{
			propertySymbol = associatedSymbol;
			return true;
		}
		propertySymbol = null;
		return false;
	}

	public static bool IsUserDefinedOperator([NotNullWhen(true)] this ISymbol? symbol)
	{
		if (symbol is IMethodSymbol methodSymbol)
		{
			return methodSymbol.MethodKind == MethodKind.UserDefinedOperator;
		}
		return false;
	}

	public static bool IsConversionOperator([NotNullWhen(true)] this ISymbol? symbol)
	{
		if (symbol is IMethodSymbol methodSymbol)
		{
			return methodSymbol.MethodKind == MethodKind.Conversion;
		}
		return false;
	}

	public static ImmutableArray<IParameterSymbol> GetParameters(this ISymbol? symbol)
	{
		if (!(symbol is IMethodSymbol methodSymbol))
		{
			if (symbol is IPropertySymbol propertySymbol)
			{
				return propertySymbol.Parameters;
			}
			return ImmutableArray<IParameterSymbol>.Empty;
		}
		return methodSymbol.Parameters;
	}

	/// <summary>
	/// True if the symbol is externally visible outside this assembly.
	/// </summary>
	public static bool IsExternallyVisible(this ISymbol symbol)
	{
		return symbol.GetResultantVisibility() == SymbolVisibility.Public;
	}

	public static SymbolVisibility GetResultantVisibility(this ISymbol symbol)
	{
		SymbolVisibility result = SymbolVisibility.Public;
		switch (symbol.Kind)
		{
		case SymbolKind.Alias:
			return SymbolVisibility.Private;
		case SymbolKind.Parameter:
			return symbol.ContainingSymbol.GetResultantVisibility();
		case SymbolKind.TypeParameter:
			return SymbolVisibility.Private;
		}
		while (symbol != null && symbol.Kind != SymbolKind.Namespace)
		{
			switch (symbol.DeclaredAccessibility)
			{
			case Accessibility.NotApplicable:
			case Accessibility.Private:
				return SymbolVisibility.Private;
			case Accessibility.ProtectedAndInternal:
			case Accessibility.Internal:
				result = SymbolVisibility.Internal;
				break;
			}
			symbol = symbol.ContainingSymbol;
		}
		return result;
	}

	public static bool MatchMemberDerivedByName([NotNullWhen(true)] this ISymbol? member, INamedTypeSymbol type, string name)
	{
		if (member != null && member.MetadataName == name)
		{
			return member.ContainingType.DerivesFrom(type);
		}
		return false;
	}

	public static bool MatchMethodDerivedByName([NotNullWhen(true)] this IMethodSymbol? method, INamedTypeSymbol type, string name)
	{
		return method?.MatchMemberDerivedByName(type, name) ?? false;
	}

	public static bool MatchMethodByName([NotNullWhen(true)] this ISymbol? member, INamedTypeSymbol type, string name)
	{
		if (member != null && member.Kind == SymbolKind.Method)
		{
			return member.MatchMemberByName(type, name);
		}
		return false;
	}

	public static bool MatchPropertyDerivedByName([NotNullWhen(true)] this ISymbol? member, INamedTypeSymbol type, string name)
	{
		if (member != null && member.Kind == SymbolKind.Property)
		{
			return member.MatchMemberDerivedByName(type, name);
		}
		return false;
	}

	public static bool MatchMemberByName([NotNullWhen(true)] this ISymbol? member, INamedTypeSymbol type, string name)
	{
		if (member != null && object.Equals(member.ContainingType, type))
		{
			return member.MetadataName == name;
		}
		return false;
	}

	public static bool MatchPropertyByName([NotNullWhen(true)] this ISymbol? member, INamedTypeSymbol type, string name)
	{
		if (member != null && member.Kind == SymbolKind.Property)
		{
			return member.MatchMemberByName(type, name);
		}
		return false;
	}

	public static bool MatchFieldByName([NotNullWhen(true)] this ISymbol? member, INamedTypeSymbol type, string name)
	{
		if (member != null && member.Kind == SymbolKind.Field)
		{
			return member.MatchMemberByName(type, name);
		}
		return false;
	}

	/// <summary>
	/// Format member names in a way consistent with FxCop's display format.
	/// </summary>
	/// <param name="member"></param>
	/// <returns>
	/// A string representing the name of the member in a format consistent with FxCop.
	/// </returns>
	public static string FormatMemberName(this ISymbol member)
	{
		return member.ToDisplayString(s_memberDisplayFormat);
	}

	/// <summary>
	/// Check whether given parameters contains any parameter with given type.
	/// </summary>
	public static bool ContainsParameterOfType(this IEnumerable<IParameterSymbol> parameters, INamedTypeSymbol type)
	{
		return parameters.GetParametersOfType(type).Any();
	}

	/// <summary>
	/// Get parameters which type is the given type
	/// </summary>
	public static IEnumerable<IParameterSymbol> GetParametersOfType(this IEnumerable<IParameterSymbol> parameters, INamedTypeSymbol type)
	{
		INamedTypeSymbol type2 = type;
		return parameters.Where((IParameterSymbol p) => p.Type.Equals(type2));
	}

	/// <summary>
	/// Gets the parameters whose type is equal to the given special type.
	/// </summary>
	public static IEnumerable<IParameterSymbol> GetParametersOfType(this IEnumerable<IParameterSymbol> parameters, SpecialType specialType)
	{
		return parameters.Where((IParameterSymbol p) => p.Type.SpecialType == specialType);
	}

	/// <summary>
	/// Check whether given overloads has any overload whose parameters has the given type as its parameter type.
	/// </summary>
	public static bool HasOverloadWithParameterOfType(this IEnumerable<IMethodSymbol> overloads, IMethodSymbol self, INamedTypeSymbol type, CancellationToken cancellationToken)
	{
		foreach (IMethodSymbol overload in overloads)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if ((self == null || !self.Equals(overload)) && overload.Parameters.ContainsParameterOfType(type))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Convert given parameters to the indices to the given method's parameter list.
	/// </summary>
	public static IEnumerable<int> GetParameterIndices(this IMethodSymbol method, IEnumerable<IParameterSymbol> parameters, CancellationToken cancellationToken)
	{
		HashSet<IParameterSymbol> set = new HashSet<IParameterSymbol>(parameters);
		for (int i = 0; i < method.Parameters.Length; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (set.Contains(method.Parameters[i]))
			{
				yield return i;
			}
		}
	}

	/// <summary>
	/// Check whether parameter count and parameter types of the given methods are same.
	/// </summary>
	public static bool ParametersAreSame(this IMethodSymbol method1, IMethodSymbol method2)
	{
		if (method1.Parameters.Length != method2.Parameters.Length)
		{
			return false;
		}
		for (int i = 0; i < method1.Parameters.Length; i++)
		{
			if (!method1.Parameters[i].ParameterTypesAreSame(method2.Parameters[i]))
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>
	/// Check whether parameter types of the given methods are same for given parameter indices.
	/// </summary>
	public static bool ParameterTypesAreSame(this IMethodSymbol method1, IMethodSymbol method2, IEnumerable<int> parameterIndices, CancellationToken cancellationToken)
	{
		foreach (int parameterIndex in parameterIndices)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!method1.Parameters[parameterIndex].ParameterTypesAreSame(method2.Parameters[parameterIndex]))
			{
				return false;
			}
		}
		return true;
	}

	public static bool ParameterTypesAreSame(this IParameterSymbol parameter1, IParameterSymbol parameter2)
	{
		ITypeSymbol originalDefinition = parameter1.Type.OriginalDefinition;
		ITypeSymbol originalDefinition2 = parameter2.Type.OriginalDefinition;
		if (originalDefinition.TypeKind == TypeKind.TypeParameter && originalDefinition2.TypeKind == TypeKind.TypeParameter && ((ITypeParameterSymbol)originalDefinition).Ordinal == ((ITypeParameterSymbol)originalDefinition2).Ordinal)
		{
			return true;
		}
		return SymbolEqualityComparer.Default.Equals(originalDefinition2, originalDefinition);
	}

	/// <summary>
	/// Check whether return type, parameters count and parameter types are same for the given methods.
	/// </summary>
	public static bool ReturnTypeAndParametersAreSame(this IMethodSymbol method, IMethodSymbol otherMethod)
	{
		if (SymbolEqualityComparer.Default.Equals(method.ReturnType, otherMethod.ReturnType))
		{
			return method.ParametersAreSame(otherMethod);
		}
		return false;
	}

	/// <summary>
	/// Check whether given symbol is from mscorlib
	/// </summary>
	public static bool IsFromMscorlib(this ISymbol symbol, Compilation compilation)
	{
		INamedTypeSymbol specialType = compilation.GetSpecialType(SpecialType.System_Object);
		return SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, specialType.ContainingAssembly);
	}

	/// <summary>
	/// Get overload from the given overloads that matches given method signature + given parameter
	/// </summary>
	public static IMethodSymbol? GetMatchingOverload(this IMethodSymbol method, IEnumerable<IMethodSymbol> overloads, int parameterIndex, INamedTypeSymbol type, CancellationToken cancellationToken)
	{
		foreach (IMethodSymbol overload in overloads)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!SymbolEqualityComparer.Default.Equals(method, overload) && overload.Parameters.Length == method.Parameters.Length && method.ParameterTypesAreSame(overload, from i in Enumerable.Range(0, method.Parameters.Length)
				where i != parameterIndex
				select i, cancellationToken) && SymbolEqualityComparer.Default.Equals(overload.Parameters[parameterIndex].Type, type))
			{
				return overload;
			}
		}
		return null;
	}

	/// <summary>
	/// Checks if a given symbol implements an interface member implicitly or explicitly
	/// </summary>
	public static bool IsImplementationOfAnyInterfaceMember(this ISymbol symbol)
	{
		if (!symbol.IsImplementationOfAnyExplicitInterfaceMember())
		{
			return symbol.IsImplementationOfAnyImplicitInterfaceMember();
		}
		return true;
	}

	public static bool IsImplementationOfAnyImplicitInterfaceMember(this ISymbol symbol)
	{
		return symbol.IsImplementationOfAnyImplicitInterfaceMember<ISymbol>();
	}

	/// <summary>
	/// Checks if a given symbol implements an interface member implicitly
	/// </summary>
	public static bool IsImplementationOfAnyImplicitInterfaceMember<TSymbol>(this ISymbol symbol) where TSymbol : ISymbol
	{
		if (symbol.ContainingType != null)
		{
			ImmutableArray<INamedTypeSymbol>.Enumerator enumerator = symbol.ContainingType.AllInterfaces.GetEnumerator();
			while (enumerator.MoveNext())
			{
				foreach (TSymbol item in enumerator.Current.GetMembers().OfType<TSymbol>())
				{
					if (symbol.IsImplementationOfInterfaceMember(item))
					{
						return true;
					}
				}
			}
		}
		return false;
	}

	/// <summary>
	/// Checks if a given symbol implements an interface member implicitly
	/// </summary>
	public static bool IsImplementationOfAnyImplicitInterfaceMember<TSymbol>(this ISymbol symbol, out TSymbol interfaceMember) where TSymbol : ISymbol
	{
		if (symbol.ContainingType != null)
		{
			ImmutableArray<INamedTypeSymbol>.Enumerator enumerator = symbol.ContainingType.AllInterfaces.GetEnumerator();
			while (enumerator.MoveNext())
			{
				foreach (TSymbol item in enumerator.Current.GetMembers().OfType<TSymbol>())
				{
					if (symbol.IsImplementationOfInterfaceMember(item))
					{
						interfaceMember = item;
						return true;
					}
				}
			}
		}
		interfaceMember = default(TSymbol);
		return false;
	}

	public static bool IsImplementationOfInterfaceMember(this ISymbol symbol, [NotNullWhen(true)] ISymbol? interfaceMember)
	{
		if (interfaceMember != null)
		{
			return SymbolEqualityComparer.Default.Equals(symbol, symbol.ContainingType.FindImplementationForInterfaceMember(interfaceMember));
		}
		return false;
	}

	/// <summary>
	/// Checks if a given symbol implements an interface member or overrides an implementation of an interface member.
	/// </summary>
	public static bool IsOverrideOrImplementationOfInterfaceMember(this ISymbol symbol, [NotNullWhen(true)] ISymbol? interfaceMember)
	{
		if (interfaceMember == null)
		{
			return false;
		}
		if (symbol.IsImplementationOfInterfaceMember(interfaceMember))
		{
			return true;
		}
		if (symbol.IsOverride)
		{
			return symbol.GetOverriddenMember()?.IsOverrideOrImplementationOfInterfaceMember(interfaceMember) ?? false;
		}
		return false;
	}

	/// <summary>
	/// Gets the symbol overridden by the given <paramref name="symbol" />.
	/// </summary>
	/// <remarks>Requires that <see cref="P:Microsoft.CodeAnalysis.ISymbol.IsOverride" /> is true for the given <paramref name="symbol" />.</remarks>
	public static ISymbol GetOverriddenMember(this ISymbol symbol)
	{
		if (!(symbol is IMethodSymbol methodSymbol))
		{
			if (!(symbol is IPropertySymbol propertySymbol))
			{
				if (symbol is IEventSymbol eventSymbol)
				{
					return eventSymbol.OverriddenEvent;
				}
				throw new NotImplementedException();
			}
			return propertySymbol.OverriddenProperty;
		}
		return methodSymbol.OverriddenMethod;
	}

	/// <summary>
	/// Checks if a given symbol implements an interface member explicitly
	/// </summary>
	public static bool IsImplementationOfAnyExplicitInterfaceMember([NotNullWhen(true)] this ISymbol? symbol)
	{
		if (symbol is IMethodSymbol methodSymbol && !methodSymbol.ExplicitInterfaceImplementations.IsEmpty)
		{
			return true;
		}
		if (symbol is IPropertySymbol propertySymbol && !propertySymbol.ExplicitInterfaceImplementations.IsEmpty)
		{
			return true;
		}
		if (symbol is IEventSymbol eventSymbol && !eventSymbol.ExplicitInterfaceImplementations.IsEmpty)
		{
			return true;
		}
		return false;
	}

	public static ITypeSymbol? GetMemberOrLocalOrParameterType(this ISymbol symbol)
	{
		return symbol.Kind switch
		{
			SymbolKind.Local => ((ILocalSymbol)symbol).Type, 
			SymbolKind.Parameter => ((IParameterSymbol)symbol).Type, 
			_ => symbol.GetMemberType(), 
		};
	}

	public static ITypeSymbol? GetMemberType(this ISymbol? symbol)
	{
		if (!(symbol is IEventSymbol eventSymbol))
		{
			if (!(symbol is IFieldSymbol fieldSymbol))
			{
				if (!(symbol is IMethodSymbol methodSymbol))
				{
					if (symbol is IPropertySymbol propertySymbol)
					{
						return propertySymbol.Type;
					}
					return null;
				}
				return methodSymbol.ReturnType;
			}
			return fieldSymbol.Type;
		}
		return eventSymbol.Type;
	}

	public static bool IsReadOnlyFieldOrProperty([NotNullWhen(true)] this ISymbol? symbol)
	{
		if (!(symbol is IFieldSymbol { IsReadOnly: var isReadOnly }))
		{
			if (!(symbol is IPropertySymbol { IsReadOnly: var isReadOnly2 }))
			{
				return false;
			}
			return isReadOnly2;
		}
		return isReadOnly;
	}

	public static AttributeData? GetAttribute(this ISymbol symbol, [NotNullWhen(true)] INamedTypeSymbol? attributeType)
	{
		return symbol.GetAttributes(attributeType).FirstOrDefault();
	}

	public static IEnumerable<AttributeData> GetAttributes(this ISymbol symbol, IEnumerable<INamedTypeSymbol?> attributesToMatch)
	{
		ImmutableArray<AttributeData>.Enumerator enumerator = symbol.GetAttributes().GetEnumerator();
		while (enumerator.MoveNext())
		{
			AttributeData current = enumerator.Current;
			if (current.AttributeClass == null)
			{
				continue;
			}
			foreach (INamedTypeSymbol item in attributesToMatch)
			{
				if (SymbolEqualityComparer.Default.Equals(current.AttributeClass, item))
				{
					yield return current;
					break;
				}
			}
		}
	}

	public static IEnumerable<AttributeData> GetAttributes(this ISymbol symbol, params INamedTypeSymbol?[] attributeTypesToMatch)
	{
		return symbol.GetAttributes((IEnumerable<INamedTypeSymbol?>)attributeTypesToMatch);
	}

	public static IEnumerable<AttributeData> GetAttributes(this ISymbol symbol, INamedTypeSymbol? attributeTypeToMatch1)
	{
		ImmutableArray<AttributeData>.Enumerator enumerator = symbol.GetAttributes().GetEnumerator();
		while (enumerator.MoveNext())
		{
			AttributeData current = enumerator.Current;
			if (SymbolEqualityComparer.Default.Equals(current.AttributeClass, attributeTypeToMatch1))
			{
				yield return current;
			}
		}
	}

	public static IEnumerable<AttributeData> GetAttributes(this ISymbol symbol, INamedTypeSymbol? attributeTypeToMatch1, INamedTypeSymbol? attributeTypeToMatch2)
	{
		ImmutableArray<AttributeData>.Enumerator enumerator = symbol.GetAttributes().GetEnumerator();
		while (enumerator.MoveNext())
		{
			AttributeData current = enumerator.Current;
			if (SymbolEqualityComparer.Default.Equals(current.AttributeClass, attributeTypeToMatch1) || SymbolEqualityComparer.Default.Equals(current.AttributeClass, attributeTypeToMatch2))
			{
				yield return current;
			}
		}
	}

	public static bool HasAnyAttribute(this ISymbol symbol, IEnumerable<INamedTypeSymbol> attributesToMatch)
	{
		return symbol.GetAttributes(attributesToMatch).Any();
	}

	public static bool HasAnyAttribute(this ISymbol symbol, params INamedTypeSymbol?[] attributeTypesToMatch)
	{
		return symbol.GetAttributes(attributeTypesToMatch).Any();
	}

	/// <summary>
	/// Returns a value indicating whether the specified symbol has the specified
	/// attribute.
	/// </summary>
	/// <param name="symbol">
	/// The symbol being examined.
	/// </param>
	/// <param name="attribute">
	/// The attribute in question.
	/// </param>
	/// <returns>
	/// <see langword="true" /> if <paramref name="symbol" /> has an attribute of type
	/// <paramref name="attribute" />; otherwise <see langword="false" />.
	/// </returns>
	/// <remarks>
	/// If <paramref name="symbol" /> is a type, this method does not find attributes
	/// on its base types.
	/// </remarks>
	public static bool HasAnyAttribute(this ISymbol symbol, [NotNullWhen(true)] INamedTypeSymbol? attribute)
	{
		if (attribute == null)
		{
			return false;
		}
		ImmutableArray<AttributeData>.Enumerator enumerator = symbol.GetAttributes().GetEnumerator();
		while (enumerator.MoveNext())
		{
			AttributeData current = enumerator.Current;
			if (SymbolEqualityComparer.Default.Equals(current.AttributeClass, attribute))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Returns a value indicating whether the specified or inherited symbol has the specified
	/// attribute.
	/// </summary>
	/// <param name="symbol">
	/// The symbol being examined.
	/// </param>
	/// <param name="attribute">
	/// The attribute in question.
	/// </param>
	/// <returns>
	/// <see langword="true" /> if <paramref name="symbol" /> has an attribute of type
	/// <paramref name="attribute" />; otherwise <see langword="false" />.
	/// </returns>
	public static bool HasDerivedTypeAttribute(this ITypeSymbol symbol, [NotNullWhen(true)] INamedTypeSymbol? attribute)
	{
		if (attribute == null)
		{
			return false;
		}
		while (symbol != null)
		{
			if (symbol.HasAnyAttribute(attribute))
			{
				return true;
			}
			if (symbol.BaseType == null)
			{
				return false;
			}
			symbol = symbol.BaseType;
		}
		return false;
	}

	/// <summary>
	/// Returns a value indicating whether the specified or inherited method symbol has the specified
	/// attribute.
	/// </summary>
	/// <param name="symbol">
	/// The symbol being examined.
	/// </param>
	/// <param name="attribute">
	/// The attribute in question.
	/// </param>
	/// <returns>
	/// <see langword="true" /> if <paramref name="symbol" /> has an attribute of type
	/// <paramref name="attribute" />; otherwise <see langword="false" />.
	/// </returns>
	public static bool HasDerivedMethodAttribute(this IMethodSymbol symbol, [NotNullWhen(true)] INamedTypeSymbol? attribute)
	{
		if (attribute == null)
		{
			return false;
		}
		while (symbol != null)
		{
			if (symbol.HasAnyAttribute(attribute))
			{
				return true;
			}
			if (symbol.OverriddenMethod == null)
			{
				return false;
			}
			symbol = symbol.OverriddenMethod;
		}
		return false;
	}

	/// <summary>
	/// Determines if the given symbol has the specified attributes.
	/// </summary>
	/// <param name="symbol">Symbol to examine.</param>
	/// <param name="attributes">Type symbols of the attributes to check for.</param>
	/// <returns>Boolean array, same size and order as <paramref name="attributes" />, indicating that the corresponding
	/// attribute is present.</returns>
	public static bool[] HasAttributes(this ISymbol symbol, params INamedTypeSymbol?[] attributes)
	{
		bool[] array = new bool[attributes.Length];
		ImmutableArray<AttributeData>.Enumerator enumerator = symbol.GetAttributes().GetEnumerator();
		while (enumerator.MoveNext())
		{
			AttributeData current = enumerator.Current;
			for (int i = 0; i < attributes.Length; i++)
			{
				if (SymbolEqualityComparer.Default.Equals(current.AttributeClass, attributes[i]))
				{
					array[i] = true;
				}
			}
		}
		return array;
	}

	/// <summary>
	/// Indicates if a symbol has at least one location in source.
	/// </summary>
	public static bool IsInSource(this ISymbol symbol)
	{
		return symbol.Locations.Any((Location l) => l.IsInSource);
	}

	public static bool IsLambdaOrLocalFunction([NotNullWhen(true)] this ISymbol? symbol)
	{
		return (symbol as IMethodSymbol)?.IsLambdaOrLocalFunction() ?? false;
	}

	/// <summary>
	/// Returns true for symbols whose name starts with an underscore and
	/// are optionally followed by an integer or other underscores, such as '_', '_1', '_2', '__', '___', etc.
	/// These symbols can be treated as special discard symbol names.
	/// </summary>
	public static bool IsSymbolWithSpecialDiscardName([NotNullWhen(true)] this ISymbol? symbol)
	{
		if (symbol != null && symbol.Name.StartsWith("_", StringComparison.Ordinal))
		{
			if (symbol.Name.Length != 1)
			{
				string name = symbol.Name;
				if (!uint.TryParse(name.Substring(1, name.Length - 1), out var _))
				{
					return symbol.Name.All((char n) => n.Equals('_'));
				}
			}
			return true;
		}
		return false;
	}

	public static bool IsConst([NotNullWhen(true)] this ISymbol? symbol)
	{
		if (!(symbol is IFieldSymbol { IsConst: var isConst }))
		{
			if (!(symbol is ILocalSymbol { IsConst: var isConst2 }))
			{
				return false;
			}
			return isConst2;
		}
		return isConst;
	}

	public static bool IsReadOnly([NotNullWhen(true)] this ISymbol? symbol)
	{
		if (!(symbol is IFieldSymbol { IsReadOnly: var isReadOnly }))
		{
			if (!(symbol is IPropertySymbol { IsReadOnly: var isReadOnly2 }))
			{
				if (!(symbol is IMethodSymbol { IsReadOnly: var isReadOnly3 }))
				{
					if (!(symbol is ITypeSymbol { IsReadOnly: var isReadOnly4 }))
					{
						return false;
					}
					return isReadOnly4;
				}
				return isReadOnly3;
			}
			return isReadOnly2;
		}
		return isReadOnly;
	}
}
