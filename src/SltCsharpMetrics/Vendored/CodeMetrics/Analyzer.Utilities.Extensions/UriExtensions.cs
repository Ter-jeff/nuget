using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities.Extensions;

internal static class UriExtensions
{
	private static readonly ImmutableHashSet<string> s_uriWords = ImmutableHashSet.Create((IEqualityComparer<string>?)StringComparer.OrdinalIgnoreCase, new string[3] { "uri", "urn", "url" });

	public static bool ParameterNamesContainUriWordSubstring(this IEnumerable<IParameterSymbol> parameters, CancellationToken cancellationToken)
	{
		foreach (IParameterSymbol parameter in parameters)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (parameter.SymbolNameContainsUriWordSubstring(cancellationToken))
			{
				return true;
			}
		}
		return false;
	}

	public static bool SymbolNameContainsUriWordSubstring(this ISymbol symbol, CancellationToken cancellationToken)
	{
		foreach (string s_uriWord in s_uriWords)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (symbol.Name.Contains(s_uriWord, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	public static IEnumerable<IParameterSymbol> GetParametersThatContainUriWords(this IEnumerable<IParameterSymbol> parameters, CancellationToken cancellationToken)
	{
		foreach (IParameterSymbol parameter in parameters)
		{
			if (parameter.SymbolNameContainsUriWords(cancellationToken))
			{
				yield return parameter;
			}
		}
	}

	public static bool SymbolNameContainsUriWords(this ISymbol symbol, CancellationToken cancellationToken)
	{
		if (symbol.Name == null || !symbol.SymbolNameContainsUriWordSubstring(cancellationToken))
		{
			return false;
		}
		WordParser wordParser = new WordParser(symbol.Name, WordParserOptions.SplitCompoundWords);
		string item;
		while ((item = wordParser.NextWord()) != null)
		{
			if (s_uriWords.Contains(item))
			{
				return true;
			}
		}
		return false;
	}
}
