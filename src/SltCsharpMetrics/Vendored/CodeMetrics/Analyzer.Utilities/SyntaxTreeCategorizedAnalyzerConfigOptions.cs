using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Analyzer.Utilities;

/// <summary>
/// Analyzer configuration options for a given syntax tree from <see cref="T:Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions" />
/// </summary>
internal sealed class SyntaxTreeCategorizedAnalyzerConfigOptions : AbstractCategorizedAnalyzerConfigOptions
{
	private readonly AnalyzerConfigOptions? _analyzerConfigOptions;

	private static readonly ConditionalWeakTable<ImmutableDictionary<string, string>, SyntaxTreeCategorizedAnalyzerConfigOptions> s_perTreeOptionsCache = new ConditionalWeakTable<ImmutableDictionary<string, string>, SyntaxTreeCategorizedAnalyzerConfigOptions>();

	public static readonly SyntaxTreeCategorizedAnalyzerConfigOptions Empty = new SyntaxTreeCategorizedAnalyzerConfigOptions(null);

	public override bool IsEmpty => this == Empty;

	private SyntaxTreeCategorizedAnalyzerConfigOptions(AnalyzerConfigOptions? analyzerConfigOptions)
	{
		_analyzerConfigOptions = analyzerConfigOptions;
	}

	public static SyntaxTreeCategorizedAnalyzerConfigOptions Create(AnalyzerConfigOptions? analyzerConfigOptions)
	{
		AnalyzerConfigOptions analyzerConfigOptions2 = analyzerConfigOptions;
		if (analyzerConfigOptions2 == null)
		{
			return Empty;
		}
		ImmutableDictionary<string, string> immutableDictionary = TryGetBackingOptionsDictionary(analyzerConfigOptions2);
		if (immutableDictionary == null)
		{
			return new SyntaxTreeCategorizedAnalyzerConfigOptions(analyzerConfigOptions2);
		}
		if (immutableDictionary.IsEmpty)
		{
			return Empty;
		}
		return s_perTreeOptionsCache.GetValue(immutableDictionary, (ImmutableDictionary<string, string> _) => new SyntaxTreeCategorizedAnalyzerConfigOptions(analyzerConfigOptions2));
		static ImmutableDictionary<string, string>? TryGetBackingOptionsDictionary(AnalyzerConfigOptions analyzerConfigOptions)
		{
			Type type = analyzerConfigOptions.GetType();
			return (type.GetField("_backing", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(analyzerConfigOptions) as ImmutableDictionary<string, string>) ?? (type.GetField("_analyzerOptions", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(analyzerConfigOptions) as ImmutableDictionary<string, string>);
		}
	}

	protected override bool TryGetOptionValue(string optionKeyPrefix, string? optionKeySuffix, string optionName, [NotNullWhen(true)] out string? valueString)
	{
		if (IsEmpty)
		{
			valueString = null;
			return false;
		}
		string text = optionKeyPrefix;
		if (optionKeySuffix != null)
		{
			text = text + optionKeySuffix + ".";
		}
		text += optionName;
		return _analyzerConfigOptions.TryGetValue(text, out valueString);
	}
}
