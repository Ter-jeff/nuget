using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities;

internal static class SymbolModifiersExtensions
{
	public static bool Contains(this SymbolModifiers modifiers, SymbolModifiers modifiersToCheck)
	{
		return (modifiers & modifiersToCheck) == modifiersToCheck;
	}

	public static SymbolModifiers GetSymbolModifiers(this ISymbol symbol)
	{
		SymbolModifiers symbolModifiers = SymbolModifiers.None;
		if (symbol.IsStatic)
		{
			symbolModifiers |= SymbolModifiers.Static;
		}
		if (symbol.IsConst())
		{
			symbolModifiers |= SymbolModifiers.Const;
		}
		if (symbol.IsReadOnly())
		{
			symbolModifiers |= SymbolModifiers.ReadOnly;
		}
		if (symbol.IsAbstract)
		{
			symbolModifiers |= SymbolModifiers.Abstract;
		}
		if (symbol.IsVirtual)
		{
			symbolModifiers |= SymbolModifiers.Virtual;
		}
		if (symbol.IsOverride)
		{
			symbolModifiers |= SymbolModifiers.Override;
		}
		if (symbol.IsSealed)
		{
			symbolModifiers |= SymbolModifiers.Sealed;
		}
		if (symbol.IsExtern)
		{
			symbolModifiers |= SymbolModifiers.Extern;
		}
		if (symbol is IMethodSymbol { IsAsync: not false })
		{
			symbolModifiers |= SymbolModifiers.Async;
		}
		return symbolModifiers;
	}
}
