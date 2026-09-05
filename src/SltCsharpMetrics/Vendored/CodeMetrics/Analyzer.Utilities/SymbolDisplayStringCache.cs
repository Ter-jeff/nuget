using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities;

/// <summary>
/// Cache ISymbol.ToDisplayName() results, to avoid performance concerns.
/// </summary>
internal sealed class SymbolDisplayStringCache
{
	/// <summary>
	/// Caches by compilation.
	/// </summary>
	private static readonly BoundedCacheWithFactory<Compilation, ConcurrentDictionary<SymbolDisplayFormat, SymbolDisplayStringCache>> s_byCompilationCache = new BoundedCacheWithFactory<Compilation, ConcurrentDictionary<SymbolDisplayFormat, SymbolDisplayStringCache>>();

	/// <summary>
	/// ConcurrentDictionary key for a null SymbolDisplayFormat.
	/// </summary>
	private static readonly SymbolDisplayFormat NullSymbolDisplayFormat = new SymbolDisplayFormat();

	/// <summary>
	/// Mapping of a symbol to its ToDisplayString().
	/// </summary>
	private readonly ConcurrentDictionary<ISymbol, string> SymbolToDisplayNames = new ConcurrentDictionary<ISymbol, string>();

	private readonly SymbolDisplayFormat? Format;

	/// <summary>
	/// Privately constructs.
	/// </summary>
	/// <param name="format">SymbolDisplayFormat to use, or null for the default.</param>
	private SymbolDisplayStringCache(SymbolDisplayFormat? format = null)
	{
		Format = ((format == NullSymbolDisplayFormat) ? null : format);
	}

	/// <summary>
	/// Gets the symbol display string cache for the compilation.
	/// </summary>
	/// <param name="compilation">Compilation that this cache is for.</param>
	/// <param name="format">A singleton SymbolDisplayFormat to use, or null for the default.</param>
	/// <returns>A SymbolDisplayStringCache.</returns>
	public static SymbolDisplayStringCache GetOrCreate(Compilation compilation, SymbolDisplayFormat? format = null)
	{
		return s_byCompilationCache.GetOrCreateValue(compilation, CreateConcurrentDictionary).GetOrAdd(format ?? NullSymbolDisplayFormat, CreateSymbolDisplayStringCache);
		static ConcurrentDictionary<SymbolDisplayFormat, SymbolDisplayStringCache> CreateConcurrentDictionary(Compilation compilation)
		{
			return new ConcurrentDictionary<SymbolDisplayFormat, SymbolDisplayStringCache>();
		}
		static SymbolDisplayStringCache CreateSymbolDisplayStringCache(SymbolDisplayFormat? format)
		{
			return new SymbolDisplayStringCache(format);
		}
	}

	/// <summary>
	/// Gets the symbol's display string.
	/// </summary>
	/// <param name="symbol">Symbol to get the display string.</param>
	/// <returns>The symbol's display string.</returns>
	public string GetDisplayString(ISymbol symbol)
	{
		return SymbolToDisplayNames.GetOrAdd(symbol, (ISymbol s) => s.ToDisplayString(Format));
	}
}
