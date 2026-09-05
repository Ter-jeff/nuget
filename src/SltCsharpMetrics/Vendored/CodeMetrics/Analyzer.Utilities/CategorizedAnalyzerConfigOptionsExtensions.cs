using System.Diagnostics.CodeAnalysis;

namespace Analyzer.Utilities;

internal static class CategorizedAnalyzerConfigOptionsExtensions
{
	public delegate bool TryParseValue<T>(string value, [MaybeNullWhen(false)] out T parsedValue);

	public delegate bool TryParseValue<T, TArg>(string value, TArg arg, [MaybeNullWhen(false)] out T parsedValue);
}
