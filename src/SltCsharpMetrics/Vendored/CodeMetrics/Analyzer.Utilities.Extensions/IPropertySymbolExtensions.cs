using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities.Extensions;

internal static class IPropertySymbolExtensions
{
	/// <summary>
	/// Check if a property is an auto-property.
	/// TODO: Remove this helper when https://github.com/dotnet/roslyn/issues/46682 is handled.
	/// </summary>
	public static bool IsAutoProperty(this IPropertySymbol propertySymbol)
	{
		IPropertySymbol propertySymbol2 = propertySymbol;
		return propertySymbol2.ContainingType.GetMembers().OfType<IFieldSymbol>().Any((IFieldSymbol f) => f.IsImplicitlyDeclared && propertySymbol2.Equals(f.AssociatedSymbol));
	}

	public static bool IsIsCompletedFromAwaiterPattern([NotNullWhen(true)] this IPropertySymbol? property, [NotNullWhen(true)] INamedTypeSymbol? inotifyCompletionType, [NotNullWhen(true)] INamedTypeSymbol? icriticalNotifyCompletionType)
	{
		if (property != null && property.Name.Equals("IsCompleted", StringComparison.Ordinal))
		{
			ITypeSymbol type = property.Type;
			if (type != null && type.SpecialType == SpecialType.System_Boolean)
			{
				INamedTypeSymbol symbol = property.ContainingType?.OriginalDefinition;
				if (!symbol.DerivesFrom(inotifyCompletionType))
				{
					return symbol.DerivesFrom(icriticalNotifyCompletionType);
				}
				return true;
			}
		}
		return false;
	}

	public static ImmutableArray<IPropertySymbol> GetOriginalDefinitions(this IPropertySymbol propertySymbol)
	{
		IPropertySymbol propertySymbol2 = propertySymbol;
		ImmutableArray<IPropertySymbol>.Builder builder = ImmutableArray.CreateBuilder<IPropertySymbol>();
		if (propertySymbol2.IsOverride && propertySymbol2.OverriddenProperty != null)
		{
			builder.Add(propertySymbol2.OverriddenProperty);
		}
		if (!propertySymbol2.ExplicitInterfaceImplementations.IsEmpty)
		{
			builder.AddRange(propertySymbol2.ExplicitInterfaceImplementations);
		}
		INamedTypeSymbol typeSymbol = propertySymbol2.ContainingType;
		string methodSymbolName = propertySymbol2.Name;
		builder.AddRange(from m in typeSymbol.AllInterfaces.SelectMany((INamedTypeSymbol m) => m.GetMembers(methodSymbolName)).OfType<IPropertySymbol>()
			where propertySymbol2.Parameters.Length == m.Parameters.Length && propertySymbol2.IsIndexer == m.IsIndexer && typeSymbol.FindImplementationForInterfaceMember(m) != null
			select m);
		return builder.ToImmutable();
	}
}
