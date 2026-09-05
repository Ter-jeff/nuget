using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities;

/// <summary>
/// <see cref="T:System.Collections.Generic.IComparer`1" /> for <see cref="T:Microsoft.CodeAnalysis.ITypeSymbol" />s sorted by display strings.
/// </summary>
internal sealed class SymbolByDisplayStringComparer : IComparer<ITypeSymbol>
{
	/// <summary>
	/// Cache of symbol display strings.
	/// </summary>
	public SymbolDisplayStringCache SymbolDisplayStringCache { get; }

	/// <summary>
	/// Constructs.
	/// </summary>
	/// <param name="compilation">The compilation containing the types to be compared.</param>
	public SymbolByDisplayStringComparer(Compilation compilation)
		: this(Analyzer.Utilities.SymbolDisplayStringCache.GetOrCreate(compilation))
	{
	}

	/// <summary>
	/// Constructs.
	/// </summary>
	/// <param name="symbolDisplayStringCache">The cache display strings to use.</param>
	public SymbolByDisplayStringComparer(SymbolDisplayStringCache symbolDisplayStringCache)
	{
		SymbolDisplayStringCache = symbolDisplayStringCache ?? throw new ArgumentNullException("symbolDisplayStringCache");
	}

	/// <summary>
	/// Compares two type symbols by their display strings.
	/// </summary>
	/// <param name="x">First type symbol to compare.</param>
	/// <param name="y">Second type symbol to compare.</param>
	/// <returns>Less than 0 if <paramref name="x" /> is before <paramref name="y" />, 0 if equal, greater than 0 if
	/// <paramref name="x" /> is after <paramref name="y" />.</returns>
	public int Compare([AllowNull] ITypeSymbol x, [AllowNull] ITypeSymbol y)
	{
		return StringComparer.Ordinal.Compare(SymbolDisplayStringCache.GetDisplayString(x), SymbolDisplayStringCache.GetDisplayString(y));
	}
}
