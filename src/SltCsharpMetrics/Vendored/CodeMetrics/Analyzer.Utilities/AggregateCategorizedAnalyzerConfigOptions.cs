using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Analyzer.Utilities;

/// <summary>
/// Aggregate analyzer configuration options:
///
/// <list type="number">
/// <item><description>Per syntax tree options from <see cref="T:Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider" />.</description></item>
/// <item><description>Options from an <strong>.editorconfig</strong> file passed in as an additional file (back compat).</description></item>
/// </list>
///
/// <inheritdoc cref="T:Analyzer.Utilities.ICategorizedAnalyzerConfigOptions" />
/// </summary>
internal sealed class AggregateCategorizedAnalyzerConfigOptions : ICategorizedAnalyzerConfigOptions
{
	public static readonly AggregateCategorizedAnalyzerConfigOptions Empty = new AggregateCategorizedAnalyzerConfigOptions(null, ImmutableDictionary<SyntaxTree, Lazy<SyntaxTreeCategorizedAnalyzerConfigOptions>>.Empty);

	private readonly Lazy<SyntaxTreeCategorizedAnalyzerConfigOptions>? _globalOptions;

	private readonly ImmutableDictionary<SyntaxTree, Lazy<SyntaxTreeCategorizedAnalyzerConfigOptions>> _perTreeOptions;

	public bool IsEmpty => this == Empty;

	private AggregateCategorizedAnalyzerConfigOptions(Lazy<SyntaxTreeCategorizedAnalyzerConfigOptions>? globalOptions, ImmutableDictionary<SyntaxTree, Lazy<SyntaxTreeCategorizedAnalyzerConfigOptions>> perTreeOptions)
	{
		_globalOptions = globalOptions;
		_perTreeOptions = perTreeOptions;
	}

	public static AggregateCategorizedAnalyzerConfigOptions Create(AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider, Compilation compilation)
	{
		AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider2 = analyzerConfigOptionsProvider;
		analyzerConfigOptionsProvider2 = analyzerConfigOptionsProvider2 ?? throw new ArgumentNullException("analyzerConfigOptionsProvider");
		if (analyzerConfigOptionsProvider2.IsEmpty())
		{
			return Empty;
		}
		Lazy<SyntaxTreeCategorizedAnalyzerConfigOptions> globalOptions = new Lazy<SyntaxTreeCategorizedAnalyzerConfigOptions>(() => SyntaxTreeCategorizedAnalyzerConfigOptions.Create(analyzerConfigOptionsProvider2.GlobalOptions));
		PooledDictionary<SyntaxTree, Lazy<SyntaxTreeCategorizedAnalyzerConfigOptions>> instance = PooledDictionary<SyntaxTree, Lazy<SyntaxTreeCategorizedAnalyzerConfigOptions>>.GetInstance();
		foreach (SyntaxTree tree2 in compilation.SyntaxTrees)
		{
			instance.Add(tree2, new Lazy<SyntaxTreeCategorizedAnalyzerConfigOptions>(() => Create(tree2, analyzerConfigOptionsProvider2)));
		}
		return new AggregateCategorizedAnalyzerConfigOptions(globalOptions, instance.ToImmutableDictionaryAndFree());
		static SyntaxTreeCategorizedAnalyzerConfigOptions Create(SyntaxTree tree, AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider)
		{
			return SyntaxTreeCategorizedAnalyzerConfigOptions.Create(analyzerConfigOptionsProvider.GetOptions(tree));
		}
	}

	public T GetOptionValue<T>(string optionName, SyntaxTree? tree, DiagnosticDescriptor? rule, CategorizedAnalyzerConfigOptionsExtensions.TryParseValue<T> tryParseValue, T defaultValue, OptionKind kind = OptionKind.DotnetCodeQuality)
	{
		if (TryGetOptionValue(optionName, kind, tree, rule, (string s, CategorizedAnalyzerConfigOptionsExtensions.TryParseValue<T> tryParseValue, [MaybeNullWhen(false)] out T parsedValue) => tryParseValue(s, out parsedValue), tryParseValue, defaultValue, out var value))
		{
			return value;
		}
		return defaultValue;
	}

	public T GetOptionValue<T, TArg>(string optionName, SyntaxTree? tree, DiagnosticDescriptor? rule, CategorizedAnalyzerConfigOptionsExtensions.TryParseValue<T, TArg> tryParseValue, TArg arg, T defaultValue, OptionKind kind = OptionKind.DotnetCodeQuality)
	{
		if (TryGetOptionValue(optionName, kind, tree, rule, tryParseValue, arg, defaultValue, out var value))
		{
			return value;
		}
		return defaultValue;
	}

	private bool TryGetOptionValue<T, TArg>(string optionName, OptionKind kind, SyntaxTree? tree, DiagnosticDescriptor? rule, CategorizedAnalyzerConfigOptionsExtensions.TryParseValue<T, TArg> tryParseValue, TArg arg, T defaultValue, [MaybeNullWhen(false)] out T value)
	{
		value = defaultValue;
		if (this == Empty)
		{
			return false;
		}
		if (tree == null)
		{
			if (_globalOptions == null)
			{
				return false;
			}
			return _globalOptions.Value.TryGetOptionValue(optionName, kind, rule, tryParseValue, arg, defaultValue, out value);
		}
		if (_perTreeOptions.TryGetValue(tree, out Lazy<SyntaxTreeCategorizedAnalyzerConfigOptions> value2))
		{
			return value2.Value.TryGetOptionValue(optionName, kind, rule, tryParseValue, arg, defaultValue, out value);
		}
		return false;
	}
}
