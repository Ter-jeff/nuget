using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities;

internal abstract class AbstractCategorizedAnalyzerConfigOptions : ICategorizedAnalyzerConfigOptions
{
	private const string DotnetCodeQualityKeyPrefix = "dotnet_code_quality.";

	private const string BuildPropertyKeyPrefix = "build_property.";

	private readonly ConcurrentDictionary<OptionKey, (bool found, object? value)> _computedOptionValuesMap;

	public abstract bool IsEmpty { get; }

	protected AbstractCategorizedAnalyzerConfigOptions()
	{
		_computedOptionValuesMap = new ConcurrentDictionary<OptionKey, (bool, object)>();
	}

	protected abstract bool TryGetOptionValue(string optionKeyPrefix, string? optionKeySuffix, string optionName, [NotNullWhen(true)] out string? valueString);

	public T GetOptionValue<T>(string optionName, SyntaxTree? tree, DiagnosticDescriptor? rule, CategorizedAnalyzerConfigOptionsExtensions.TryParseValue<T> tryParseValue, T defaultValue, OptionKind kind = OptionKind.DotnetCodeQuality)
	{
		if (TryGetOptionValue(optionName, kind, rule, (string s, CategorizedAnalyzerConfigOptionsExtensions.TryParseValue<T> tryParseValue, [MaybeNullWhen(false)] out T parsedValue) => tryParseValue(s, out parsedValue), tryParseValue, defaultValue, out var value))
		{
			return value;
		}
		return defaultValue;
	}

	public T GetOptionValue<T, TArg>(string optionName, SyntaxTree? tree, DiagnosticDescriptor? rule, CategorizedAnalyzerConfigOptionsExtensions.TryParseValue<T, TArg> tryParseValue, TArg arg, T defaultValue, OptionKind kind = OptionKind.DotnetCodeQuality)
	{
		if (TryGetOptionValue(optionName, kind, rule, tryParseValue, arg, defaultValue, out var value))
		{
			return value;
		}
		return defaultValue;
	}

	private static string MapOptionKindToKeyPrefix(OptionKind optionKind)
	{
		return optionKind switch
		{
			OptionKind.DotnetCodeQuality => "dotnet_code_quality.", 
			OptionKind.BuildProperty => "build_property.", 
			_ => throw new NotImplementedException(), 
		};
	}

	public bool TryGetOptionValue<T, TArg>(string optionName, OptionKind kind, DiagnosticDescriptor? rule, CategorizedAnalyzerConfigOptionsExtensions.TryParseValue<T, TArg> tryParseValue, TArg arg, T defaultValue, out T value)
	{
		if (IsEmpty)
		{
			value = defaultValue;
			return false;
		}
		OptionKey orCreate = OptionKey.GetOrCreate(rule?.Id, optionName);
		if (!_computedOptionValuesMap.TryGetValue(orCreate, out (bool, object) value2))
		{
			value2 = _computedOptionValuesMap.GetOrAdd(orCreate, ComputeOptionValue(optionName, kind, rule, tryParseValue, arg));
		}
		if (value2.Item1)
		{
			value = (T)value2.Item2;
			return true;
		}
		value = defaultValue;
		return false;
	}

	private (bool found, object? value) ComputeOptionValue<T, TArg>(string optionName, OptionKind kind, DiagnosticDescriptor? rule, CategorizedAnalyzerConfigOptionsExtensions.TryParseValue<T, TArg> tryParseValue, TArg arg)
	{
		string optionName2 = optionName;
		CategorizedAnalyzerConfigOptionsExtensions.TryParseValue<T, TArg> tryParseValue2 = tryParseValue;
		TArg arg2 = arg;
		string optionKeyPrefix2 = MapOptionKindToKeyPrefix(kind);
		if (rule != null && (TryGetSpecificOptionValue(rule.Id, optionKeyPrefix2, out var specificOptionValue2) || TryGetSpecificOptionValue(rule.Category, optionKeyPrefix2, out specificOptionValue2) || TryGetAnySpecificOptionValue(rule.CustomTags, optionKeyPrefix2, out specificOptionValue2)))
		{
			return (found: true, value: specificOptionValue2);
		}
		if (TryGetGeneralOptionValue(optionKeyPrefix2, out specificOptionValue2))
		{
			return (found: true, value: specificOptionValue2);
		}
		return (found: false, value: null);
		bool TryGetAnySpecificOptionValue(IEnumerable<string> specificOptionKeys, string optionKeyPrefix, [MaybeNullWhen(false)] out T specificOptionValue)
		{
			foreach (string specificOptionKey in specificOptionKeys)
			{
				if (TryGetSpecificOptionValue(specificOptionKey, optionKeyPrefix, out specificOptionValue))
				{
					return true;
				}
			}
			specificOptionValue = default(T);
			return false;
		}
		bool TryGetGeneralOptionValue(string optionKeyPrefix, [MaybeNullWhen(false)] out T generalOptionValue)
		{
			if (TryGetOptionValue(optionKeyPrefix, null, optionName2, out string valueString))
			{
				return tryParseValue2(valueString, arg2, out generalOptionValue);
			}
			generalOptionValue = default(T);
			return false;
		}
		bool TryGetSpecificOptionValue(string specificOptionKey, string optionKeyPrefix, [MaybeNullWhen(false)] out T specificOptionValue)
		{
			if (TryGetOptionValue(optionKeyPrefix, specificOptionKey, optionName2, out string valueString2))
			{
				return tryParseValue2(valueString2, arg2, out specificOptionValue);
			}
			specificOptionValue = default(T);
			return false;
		}
	}
}
