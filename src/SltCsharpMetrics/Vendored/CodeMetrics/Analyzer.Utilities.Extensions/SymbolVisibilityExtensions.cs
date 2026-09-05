namespace Analyzer.Utilities.Extensions;

/// <summary>
/// Extensions for <see cref="T:Analyzer.Utilities.Extensions.SymbolVisibility" />.
/// </summary>
internal static class SymbolVisibilityExtensions
{
	/// <summary>
	/// Determines whether <paramref name="typeVisibility" /> is at least as visible as <paramref name="comparisonVisibility" />.
	/// </summary>
	/// <param name="typeVisibility">The visibility to compare against.</param>
	/// <param name="comparisonVisibility">The visibility to compare with.</param>
	/// <returns>True if one can say that <paramref name="typeVisibility" /> is at least as visible as <paramref name="comparisonVisibility" />.</returns>
	/// <remarks>
	/// For example, <see cref="F:Analyzer.Utilities.Extensions.SymbolVisibility.Public" /> is at least as visible as <see cref="F:Analyzer.Utilities.Extensions.SymbolVisibility.Internal" />, but <see cref="F:Analyzer.Utilities.Extensions.SymbolVisibility.Private" /> is not as visible as <see cref="F:Analyzer.Utilities.Extensions.SymbolVisibility.Public" />.
	/// </remarks>
	public static bool IsAtLeastAsVisibleAs(this SymbolVisibility typeVisibility, SymbolVisibility comparisonVisibility)
	{
		return typeVisibility switch
		{
			SymbolVisibility.Public => true, 
			SymbolVisibility.Internal => comparisonVisibility != SymbolVisibility.Public, 
			SymbolVisibility.Private => comparisonVisibility == SymbolVisibility.Private, 
			_ => throw new ArgumentOutOfRangeException("typeVisibility", typeVisibility, null), 
		};
	}
}
