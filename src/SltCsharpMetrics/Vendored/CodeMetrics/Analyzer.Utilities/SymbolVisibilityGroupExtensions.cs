using Analyzer.Utilities.Extensions;

namespace Analyzer.Utilities;

internal static class SymbolVisibilityGroupExtensions
{
	public static bool Contains(this SymbolVisibilityGroup symbolVisibilityGroup, SymbolVisibility symbolVisibility)
	{
		return symbolVisibility switch
		{
			SymbolVisibility.Public => (symbolVisibilityGroup & SymbolVisibilityGroup.Public) != 0, 
			SymbolVisibility.Internal => (symbolVisibilityGroup & SymbolVisibilityGroup.Internal) != 0, 
			SymbolVisibility.Private => (symbolVisibilityGroup & SymbolVisibilityGroup.Private) != 0, 
			_ => throw new ArgumentOutOfRangeException("symbolVisibility", symbolVisibility, null), 
		};
	}
}
