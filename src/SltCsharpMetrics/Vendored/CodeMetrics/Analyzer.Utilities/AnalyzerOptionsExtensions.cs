using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.Options;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Analyzer.Utilities;

public static class AnalyzerOptionsExtensions
{
	private static readonly ConditionalWeakTable<AnalyzerOptions, ICategorizedAnalyzerConfigOptions> s_cachedOptions = new ConditionalWeakTable<AnalyzerOptions, ICategorizedAnalyzerConfigOptions>();

	private static readonly ImmutableHashSet<OutputKind> s_defaultOutputKinds = ImmutableHashSet.CreateRange(Enum.GetValues(typeof(OutputKind)).Cast<OutputKind>());

	private static bool TryGetSyntaxTreeForOption(ISymbol symbol, [NotNullWhen(true)] out SyntaxTree? tree)
	{
		SymbolKind kind = symbol.Kind;
		if (kind != SymbolKind.Assembly)
		{
			if (kind != SymbolKind.Namespace)
			{
				if (kind == SymbolKind.Parameter)
				{
					return TryGetSyntaxTreeForOption(symbol.ContainingSymbol, out tree);
				}
			}
			else if (((INamespaceSymbol)symbol).IsGlobalNamespace)
			{
				goto IL_0024;
			}
			tree = symbol.Locations[0].SourceTree;
			return tree != null;
		}
		goto IL_0024;
		IL_0024:
		tree = null;
		return false;
	}

	public static SymbolVisibilityGroup GetSymbolVisibilityGroupOption(this AnalyzerOptions options, DiagnosticDescriptor rule, ISymbol symbol, Compilation compilation, SymbolVisibilityGroup defaultValue)
	{
		if (!TryGetSyntaxTreeForOption(symbol, out SyntaxTree tree))
		{
			return defaultValue;
		}
		return options.GetSymbolVisibilityGroupOption(rule, tree, compilation, defaultValue);
	}

	private static SymbolVisibilityGroup GetSymbolVisibilityGroupOption(this AnalyzerOptions options, DiagnosticDescriptor rule, SyntaxTree tree, Compilation compilation, SymbolVisibilityGroup defaultValue)
	{
		return options.GetFlagsEnumOptionValue("api_surface", rule, tree, compilation, defaultValue);
	}

	private static SymbolModifiers GetRequiredModifiersOption(this AnalyzerOptions options, DiagnosticDescriptor rule, ISymbol symbol, Compilation compilation, SymbolModifiers defaultValue)
	{
		if (!TryGetSyntaxTreeForOption(symbol, out SyntaxTree tree))
		{
			return defaultValue;
		}
		return options.GetRequiredModifiersOption(rule, tree, compilation, defaultValue);
	}

	private static SymbolModifiers GetRequiredModifiersOption(this AnalyzerOptions options, DiagnosticDescriptor rule, SyntaxTree tree, Compilation compilation, SymbolModifiers defaultValue)
	{
		return options.GetFlagsEnumOptionValue("required_modifiers", rule, tree, compilation, defaultValue);
	}

	public static EnumValuesPrefixTrigger GetEnumValuesPrefixTriggerOption(this AnalyzerOptions options, DiagnosticDescriptor rule, ISymbol symbol, Compilation compilation, EnumValuesPrefixTrigger defaultValue)
	{
		if (!TryGetSyntaxTreeForOption(symbol, out SyntaxTree tree))
		{
			return defaultValue;
		}
		return options.GetEnumValuesPrefixTriggerOption(rule, tree, compilation, defaultValue);
	}

	private static EnumValuesPrefixTrigger GetEnumValuesPrefixTriggerOption(this AnalyzerOptions options, DiagnosticDescriptor rule, SyntaxTree tree, Compilation compilation, EnumValuesPrefixTrigger defaultValue)
	{
		return options.GetNonFlagsEnumOptionValue("enum_values_prefix_trigger", rule, tree, compilation, defaultValue);
	}

	public static ImmutableHashSet<OutputKind> GetOutputKindsOption(this AnalyzerOptions options, DiagnosticDescriptor rule, SyntaxTree tree, Compilation compilation)
	{
		return options.GetOutputKindsOption(rule, tree, compilation, s_defaultOutputKinds);
	}

	public static ImmutableHashSet<OutputKind> GetOutputKindsOption(this AnalyzerOptions options, DiagnosticDescriptor rule, SyntaxTree tree, Compilation compilation, ImmutableHashSet<OutputKind> defaultValue)
	{
		return options.GetNonFlagsEnumOptionValue("output_kind", rule, tree, compilation, defaultValue);
	}

	public static ImmutableHashSet<SymbolKind> GetAnalyzedSymbolKindsOption(this AnalyzerOptions options, DiagnosticDescriptor rule, ISymbol symbol, Compilation compilation, ImmutableHashSet<SymbolKind> defaultSymbolKinds)
	{
		if (!TryGetSyntaxTreeForOption(symbol, out SyntaxTree tree))
		{
			return defaultSymbolKinds;
		}
		return options.GetAnalyzedSymbolKindsOption(rule, tree, compilation, defaultSymbolKinds);
	}

	private static ImmutableHashSet<SymbolKind> GetAnalyzedSymbolKindsOption(this AnalyzerOptions options, DiagnosticDescriptor rule, SyntaxTree tree, Compilation compilation, ImmutableHashSet<SymbolKind> defaultSymbolKinds)
	{
		return options.GetNonFlagsEnumOptionValue("analyzed_symbol_kinds", rule, tree, compilation, defaultSymbolKinds);
	}

	private static TEnum GetFlagsEnumOptionValue<TEnum>(this AnalyzerOptions options, string optionName, DiagnosticDescriptor rule, SyntaxTree tree, Compilation compilation, TEnum defaultValue) where TEnum : struct
	{
		return options.GetOrComputeCategorizedAnalyzerConfigOptions(compilation).GetOptionValue(optionName, tree, rule, delegate(string value, out TEnum result)
		{
			return Enum.TryParse<TEnum>(value, ignoreCase: true, out result);
		}, defaultValue);
	}

	private static ImmutableHashSet<TEnum> GetNonFlagsEnumOptionValue<TEnum>(this AnalyzerOptions options, string optionName, DiagnosticDescriptor rule, SyntaxTree tree, Compilation compilation, ImmutableHashSet<TEnum> defaultValue) where TEnum : struct
	{
		return options.GetOrComputeCategorizedAnalyzerConfigOptions(compilation).GetOptionValue(optionName, tree, rule, TryParseValue, defaultValue);
		static bool TryParseValue(string value, out ImmutableHashSet<TEnum> result)
		{
			ImmutableHashSet<TEnum>.Builder builder = ImmutableHashSet.CreateBuilder<TEnum>();
			string[] array = value.Split(',');
			for (int i = 0; i < array.Length; i++)
			{
				if (Enum.TryParse<TEnum>(array[i], ignoreCase: true, out var result2))
				{
					builder.Add(result2);
				}
			}
			result = builder.ToImmutable();
			return builder.Count > 0;
		}
	}

	private static TEnum GetNonFlagsEnumOptionValue<TEnum>(this AnalyzerOptions options, string optionName, DiagnosticDescriptor rule, SyntaxTree tree, Compilation compilation, TEnum defaultValue) where TEnum : struct
	{
		return options.GetFlagsEnumOptionValue(optionName, rule, tree, compilation, defaultValue);
	}

	public static bool GetBoolOptionValue(this AnalyzerOptions options, string optionName, DiagnosticDescriptor rule, ISymbol symbol, Compilation compilation, bool defaultValue)
	{
		if (!TryGetSyntaxTreeForOption(symbol, out SyntaxTree tree))
		{
			return defaultValue;
		}
		return options.GetBoolOptionValue(optionName, rule, tree, compilation, defaultValue);
	}

	public static bool GetBoolOptionValue(this AnalyzerOptions options, string optionName, DiagnosticDescriptor? rule, SyntaxTree tree, Compilation compilation, bool defaultValue)
	{
		return options.GetOrComputeCategorizedAnalyzerConfigOptions(compilation).GetOptionValue(optionName, tree, rule, bool.TryParse, defaultValue);
	}

	public static uint GetUnsignedIntegralOptionValue(this AnalyzerOptions options, string optionName, DiagnosticDescriptor rule, SyntaxTree tree, Compilation compilation, uint defaultValue)
	{
		return options.GetOrComputeCategorizedAnalyzerConfigOptions(compilation).GetOptionValue(optionName, tree, rule, uint.TryParse, defaultValue);
	}

	public static string GetStringOptionValue(this AnalyzerOptions options, string optionName, DiagnosticDescriptor rule, SyntaxTree tree, Compilation compilation)
	{
		return options.GetOrComputeCategorizedAnalyzerConfigOptions(compilation).GetOptionValue(optionName, tree, rule, TryParseValue, string.Empty);
		static bool TryParseValue(string value, out string result)
		{
			result = value;
			return !string.IsNullOrEmpty(value);
		}
	}

	public static SymbolNamesWithValueOption<Unit> GetNullCheckValidationMethodsOption(this AnalyzerOptions options, DiagnosticDescriptor rule, SyntaxTree tree, Compilation compilation)
	{
		return options.GetSymbolNamesWithValueOption("null_check_validation_methods", rule, tree, compilation, (string name) => new SymbolNamesWithValueOption<Unit>.NameParts(name, Unit.Default), "M:");
	}

	public static SymbolNamesWithValueOption<Unit> GetAdditionalStringFormattingMethodsOption(this AnalyzerOptions options, DiagnosticDescriptor rule, SyntaxTree tree, Compilation compilation)
	{
		return options.GetSymbolNamesWithValueOption("additional_string_formatting_methods", rule, tree, compilation, (string name) => new SymbolNamesWithValueOption<Unit>.NameParts(name, Unit.Default), "M:");
	}

	public static bool IsConfiguredToSkipAnalysis(this AnalyzerOptions options, DiagnosticDescriptor rule, ISymbol symbol, Compilation compilation)
	{
		return options.IsConfiguredToSkipAnalysis(rule, symbol, symbol, compilation);
	}

	public static bool IsConfiguredToSkipAnalysis(this AnalyzerOptions options, DiagnosticDescriptor rule, ISymbol symbol, ISymbol containingContextSymbol, Compilation compilation)
	{
		SymbolNamesWithValueOption<Unit> symbolNamesWithValueOption = GetExcludedSymbolNamesWithValueOption(options, rule, containingContextSymbol, compilation);
		SymbolNamesWithValueOption<Unit> symbolNamesWithValueOption2 = GetExcludedTypeNamesWithDerivedTypesOption(options, rule, containingContextSymbol, compilation);
		if (symbolNamesWithValueOption.IsEmpty && symbolNamesWithValueOption2.IsEmpty)
		{
			return false;
		}
		while (symbol != null)
		{
			if (symbolNamesWithValueOption.Contains(symbol))
			{
				return true;
			}
			if (symbol is INamedTypeSymbol type && !symbolNamesWithValueOption2.IsEmpty)
			{
				foreach (INamedTypeSymbol baseTypesAndThi in type.GetBaseTypesAndThis())
				{
					if (symbolNamesWithValueOption2.Contains(baseTypesAndThi))
					{
						return true;
					}
				}
			}
			symbol = symbol.ContainingSymbol;
		}
		return false;
		static SymbolNamesWithValueOption<Unit> GetExcludedSymbolNamesWithValueOption(AnalyzerOptions options, DiagnosticDescriptor rule, ISymbol symbol, Compilation compilation)
		{
			if (!TryGetSyntaxTreeForOption(symbol, out SyntaxTree tree2))
			{
				return SymbolNamesWithValueOption<Unit>.Empty;
			}
			return options.GetSymbolNamesWithValueOption("excluded_symbol_names", rule, tree2, compilation, (string name) => new SymbolNamesWithValueOption<Unit>.NameParts(name, Unit.Default));
		}
		static SymbolNamesWithValueOption<Unit> GetExcludedTypeNamesWithDerivedTypesOption(AnalyzerOptions options, DiagnosticDescriptor rule, ISymbol symbol, Compilation compilation)
		{
			if (!TryGetSyntaxTreeForOption(symbol, out SyntaxTree tree))
			{
				return SymbolNamesWithValueOption<Unit>.Empty;
			}
			return options.GetSymbolNamesWithValueOption("excluded_type_names_with_derived_types", rule, tree, compilation, (string name) => new SymbolNamesWithValueOption<Unit>.NameParts(name, Unit.Default), "T:");
		}
	}

	public static SymbolNamesWithValueOption<Unit> GetDisallowedSymbolNamesWithValueOption(this AnalyzerOptions options, DiagnosticDescriptor rule, ISymbol symbol, Compilation compilation)
	{
		return options.GetDisallowedSymbolNamesWithValueOption(rule, symbol.Locations[0].SourceTree, compilation);
	}

	private static SymbolNamesWithValueOption<Unit> GetDisallowedSymbolNamesWithValueOption(this AnalyzerOptions options, DiagnosticDescriptor rule, SyntaxTree? tree, Compilation compilation)
	{
		return options.GetSymbolNamesWithValueOption("disallowed_symbol_names", rule, tree, compilation, (string name) => new SymbolNamesWithValueOption<Unit>.NameParts(name, Unit.Default));
	}

	public static SymbolNamesWithValueOption<string?> GetAdditionalRequiredSuffixesOption(this AnalyzerOptions options, DiagnosticDescriptor rule, ISymbol symbol, Compilation compilation)
	{
		return options.GetAdditionalRequiredSuffixesOption(rule, symbol.Locations[0].SourceTree, compilation);
	}

	private static SymbolNamesWithValueOption<string?> GetAdditionalRequiredSuffixesOption(this AnalyzerOptions options, DiagnosticDescriptor rule, SyntaxTree? tree, Compilation compilation)
	{
		return options.GetSymbolNamesWithValueOption("additional_required_suffixes", rule, tree, compilation, GetParts, "T:");
		static SymbolNamesWithValueOption<string?>.NameParts GetParts(string name)
		{
			string[] array = name.Split(new string[1] { "->" }, StringSplitOptions.RemoveEmptyEntries);
			if (array.Length != 2)
			{
				return new SymbolNamesWithValueOption<string>.NameParts(name, null);
			}
			string text = array[1].Trim();
			if (text.Length >= 2 && text[0] == '{')
			{
				if (text[text.Length - 1] == '}')
				{
					for (int i = 1; i < text.Length - 2; i++)
					{
						if (text[i] != ' ')
						{
							return new SymbolNamesWithValueOption<string>.NameParts(array[0], text);
						}
					}
					return new SymbolNamesWithValueOption<string>.NameParts(array[0], string.Empty);
				}
			}
			return new SymbolNamesWithValueOption<string>.NameParts(array[0], text);
		}
	}

	public static SymbolNamesWithValueOption<INamedTypeSymbol?> GetAdditionalRequiredGenericInterfaces(this AnalyzerOptions options, DiagnosticDescriptor rule, ISymbol symbol, Compilation compilation)
	{
		return options.GetAdditionalRequiredGenericInterfaces(rule, symbol.Locations[0].SourceTree, compilation);
	}

	private static SymbolNamesWithValueOption<INamedTypeSymbol?> GetAdditionalRequiredGenericInterfaces(this AnalyzerOptions options, DiagnosticDescriptor rule, SyntaxTree? tree, Compilation compilation)
	{
		Compilation compilation2 = compilation;
		return options.GetSymbolNamesWithValueOption("additional_required_generic_interfaces", rule, tree, compilation2, (string x) => GetParts(x, compilation2), "T:");
		static SymbolNamesWithValueOption<INamedTypeSymbol?>.NameParts GetParts(string name, Compilation compilation)
		{
			string[] array = name.Split(new string[1] { "->" }, StringSplitOptions.RemoveEmptyEntries);
			if (array.Length != 2)
			{
				return new SymbolNamesWithValueOption<INamedTypeSymbol>.NameParts(name, null);
			}
			string text = array[1].Trim();
			if (!text.StartsWith("T:", StringComparison.Ordinal))
			{
				text = "T:" + text;
			}
			ImmutableArray<ISymbol> symbolsForDeclarationId = DocumentationCommentId.GetSymbolsForDeclarationId(text, compilation);
			if (symbolsForDeclarationId.Length != 1 || !(symbolsForDeclarationId[0] is INamedTypeSymbol { TypeKind: TypeKind.Interface, IsGenericType: not false } namedTypeSymbol))
			{
				return new SymbolNamesWithValueOption<INamedTypeSymbol>.NameParts(array[0], null);
			}
			return new SymbolNamesWithValueOption<INamedTypeSymbol>.NameParts(array[0], namedTypeSymbol);
		}
	}

	public static SymbolNamesWithValueOption<Unit> GetInheritanceExcludedSymbolNamesOption(this AnalyzerOptions options, DiagnosticDescriptor rule, SyntaxTree tree, Compilation compilation, string defaultForcedValue)
	{
		return options.GetSymbolNamesWithValueOption("additional_inheritance_excluded_symbol_names", rule, tree, compilation, (string name) => new SymbolNamesWithValueOption<Unit>.NameParts(name, Unit.Default), null, null, defaultForcedValue);
	}

	public static SymbolNamesWithValueOption<Unit> GetAdditionalUseResultsMethodsOption(this AnalyzerOptions options, DiagnosticDescriptor rule, SyntaxTree tree, Compilation compilation)
	{
		return options.GetSymbolNamesWithValueOption("additional_use_results_methods", rule, tree, compilation, (string name) => new SymbolNamesWithValueOption<Unit>.NameParts(name, Unit.Default), "M:");
	}

	public static SymbolNamesWithValueOption<Unit> GetEnumerationMethodsOption(this AnalyzerOptions options, DiagnosticDescriptor rule, SyntaxTree tree, Compilation compilation)
	{
		return options.GetSymbolNamesWithValueOption("enumeration_methods", rule, tree, compilation, (string name) => new SymbolNamesWithValueOption<Unit>.NameParts(name, Unit.Default), "M:");
	}

	public static SymbolNamesWithValueOption<Unit> GetLinqChainMethodsOption(this AnalyzerOptions options, DiagnosticDescriptor rule, SyntaxTree tree, Compilation compilation)
	{
		return options.GetSymbolNamesWithValueOption("linq_chain_methods", rule, tree, compilation, (string name) => new SymbolNamesWithValueOption<Unit>.NameParts(name, Unit.Default), "M:");
	}

	private static SymbolNamesWithValueOption<TValue> GetSymbolNamesWithValueOption<TValue>(this AnalyzerOptions options, string optionName, DiagnosticDescriptor rule, SyntaxTree? tree, Compilation compilation, Func<string, SymbolNamesWithValueOption<TValue>.NameParts> getTypeAndSuffixFunc, string? namePrefix = null, string? optionDefaultValue = null, string? optionForcedValue = null)
	{
		string optionDefaultValue2 = optionDefaultValue;
		string optionForcedValue2 = optionForcedValue;
		Compilation compilation2 = compilation;
		Func<string, SymbolNamesWithValueOption<TValue>.NameParts> getTypeAndSuffixFunc2 = getTypeAndSuffixFunc;
		string namePrefix2 = namePrefix;
		return options.GetOrComputeCategorizedAnalyzerConfigOptions(compilation2).GetOptionValue(optionName, tree, rule, TryParse, (compilation2, getTypeAndSuffixFunc2, namePrefix2, optionForcedValue2), GetDefaultValue());
		SymbolNamesWithValueOption<TValue> GetDefaultValue()
		{
			string text = string.Empty;
			if (!string.IsNullOrEmpty(optionDefaultValue2))
			{
				text = optionDefaultValue2;
			}
			if (!RoslynString.IsNullOrEmpty(optionForcedValue2) && (text == null || !text.Contains(optionForcedValue2, StringComparison.Ordinal)))
			{
				text = optionForcedValue2 + "|" + text;
			}
			if (!TryParse(text, (compilation: compilation2, getTypeAndSuffixFunc: getTypeAndSuffixFunc2, namePrefix: namePrefix2, optionForcedValue: optionForcedValue2), out var option2))
			{
				return SymbolNamesWithValueOption<TValue>.Empty;
			}
			return option2;
		}
		static bool TryParse(string s, (Compilation compilation, Func<string, SymbolNamesWithValueOption<TValue>.NameParts> getTypeAndSuffixFunc, string? namePrefix, string? optionForcedValue) arg, out SymbolNamesWithValueOption<TValue> option)
		{
			string text2 = s;
			if (!RoslynString.IsNullOrEmpty(arg.optionForcedValue) && (text2 == null || !text2.Contains(arg.optionForcedValue, StringComparison.Ordinal)))
			{
				text2 = arg.optionForcedValue + "|" + text2;
			}
			if (string.IsNullOrEmpty(text2))
			{
				option = SymbolNamesWithValueOption<TValue>.Empty;
				return false;
			}
			ImmutableArray<string> symbolNames = text2.Split(new char[1] { '|' }, StringSplitOptions.RemoveEmptyEntries).ToImmutableArray();
			option = SymbolNamesWithValueOption<TValue>.Create(symbolNames, arg.compilation, arg.namePrefix, arg.getTypeAndSuffixFunc);
			return true;
		}
	}

	public static string? GetMSBuildPropertyValue(this AnalyzerOptions options, string optionName, Compilation compilation)
	{
		SyntaxTree syntaxTree = compilation.SyntaxTrees.FirstOrDefault();
		if (syntaxTree == null)
		{
			return null;
		}
		return options.GetOrComputeCategorizedAnalyzerConfigOptions(compilation).GetOptionValue(optionName, syntaxTree, null, delegate(string value, out string? result)
		{
			result = value;
			return true;
		}, null, OptionKind.BuildProperty);
	}

	public static ImmutableArray<string> GetMSBuildItemMetadataValues(this AnalyzerOptions options, string itemOptionName, Compilation compilation)
	{
		SyntaxTree syntaxTree = compilation.SyntaxTrees.FirstOrDefault();
		if (syntaxTree == null)
		{
			return ImmutableArray<string>.Empty;
		}
		string propertyNameForItemOptionName = MSBuildItemOptionNamesHelpers.GetPropertyNameForItemOptionName(itemOptionName);
		return MSBuildItemOptionNamesHelpers.ParseItemOptionValue(options.GetOrComputeCategorizedAnalyzerConfigOptions(compilation).GetOptionValue(propertyNameForItemOptionName, syntaxTree, null, delegate(string value, out string? result)
		{
			result = value;
			return true;
		}, null, OptionKind.BuildProperty));
	}

	/// <summary>
	/// Returns true if the given source symbol has required visibility based on options:
	///   1. If user has explicitly configured candidate <see cref="T:Analyzer.Utilities.SymbolVisibilityGroup" /> in editor config options and
	///      given symbol's visibility is one of the candidate visibilities.
	///   2. Otherwise, if user has not configured visibility, and given symbol's visibility
	///      matches the given default symbol visibility.
	/// </summary>
	public static bool MatchesConfiguredVisibility(this AnalyzerOptions options, DiagnosticDescriptor rule, ISymbol symbol, Compilation compilation, SymbolVisibilityGroup defaultRequiredVisibility = SymbolVisibilityGroup.Public)
	{
		return options.MatchesConfiguredVisibility(rule, symbol, symbol, compilation, defaultRequiredVisibility);
	}

	/// <summary>
	/// Returns true if the given symbol has required visibility based on options in context of the given containing symbol:
	///   1. If user has explicitly configured candidate <see cref="T:Analyzer.Utilities.SymbolVisibilityGroup" /> in editor config options and
	///      given symbol's visibility is one of the candidate visibilities.
	///   2. Otherwise, if user has not configured visibility, and given symbol's visibility
	///      matches the given default symbol visibility.
	/// </summary>
	public static bool MatchesConfiguredVisibility(this AnalyzerOptions options, DiagnosticDescriptor rule, ISymbol symbol, ISymbol containingContextSymbol, Compilation compilation, SymbolVisibilityGroup defaultRequiredVisibility = SymbolVisibilityGroup.Public)
	{
		SymbolVisibilityGroup symbolVisibilityGroupOption = options.GetSymbolVisibilityGroupOption(rule, containingContextSymbol, compilation, defaultRequiredVisibility);
		if (symbolVisibilityGroupOption != SymbolVisibilityGroup.All)
		{
			return symbolVisibilityGroupOption.Contains(symbol.GetResultantVisibility());
		}
		return true;
	}

	/// <summary>
	/// Returns true if the given symbol has required symbol modifiers based on options:
	///   1. If user has explicitly configured candidate <see cref="T:Analyzer.Utilities.SymbolModifiers" /> in editor config options and
	///      given symbol has all the required modifiers.
	///   2. Otherwise, if user has not configured modifiers.
	/// </summary>
	public static bool MatchesConfiguredModifiers(this AnalyzerOptions options, DiagnosticDescriptor rule, ISymbol symbol, Compilation compilation, SymbolModifiers defaultRequiredModifiers = SymbolModifiers.None)
	{
		SymbolModifiers requiredModifiersOption = options.GetRequiredModifiersOption(rule, symbol, compilation, defaultRequiredModifiers);
		return symbol.GetSymbolModifiers().Contains(requiredModifiersOption);
	}

	private static ICategorizedAnalyzerConfigOptions GetOrComputeCategorizedAnalyzerConfigOptions(this AnalyzerOptions options, Compilation compilation)
	{
		if (s_cachedOptions.TryGetValue(options, out ICategorizedAnalyzerConfigOptions value))
		{
			return value;
		}
		return GetOrComputeCategorizedAnalyzerConfigOptions_Slow(options, compilation);
		static ICategorizedAnalyzerConfigOptions GetOrComputeCategorizedAnalyzerConfigOptions_Slow(AnalyzerOptions options, Compilation compilation)
		{
			Compilation compilation2 = compilation;
			return s_cachedOptions.GetValue(options, (AnalyzerOptions options) => AggregateCategorizedAnalyzerConfigOptions.Create(options.AnalyzerConfigOptionsProvider, compilation2));
		}
	}
}
