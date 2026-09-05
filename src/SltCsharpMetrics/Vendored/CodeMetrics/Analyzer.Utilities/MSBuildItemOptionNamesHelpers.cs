using System.Collections.Immutable;
using System.Diagnostics;

namespace Analyzer.Utilities;

internal static class MSBuildItemOptionNamesHelpers
{
	public const char ValuesSeparator = ',';

	private static readonly char[] s_itemMetadataValuesSeparators = new char[1] { ',' };

	public static string GetPropertyNameForItemOptionName(string itemOptionName)
	{
		return "_" + itemOptionName + "List";
	}

	[Conditional("DEBUG")]
	public static void VerifySupportedItemOptionName(string itemOptionName)
	{
	}

	public static ImmutableArray<string> ParseItemOptionValue(string? itemOptionValue)
	{
		if (itemOptionValue == null)
		{
			return ImmutableArray<string>.Empty;
		}
		return ProduceTrimmedArray(itemOptionValue).ToImmutableArray();
	}

	private static IEnumerable<string> ProduceTrimmedArray(string itemOptionValue)
	{
		string[] array = itemOptionValue.Split(s_itemMetadataValuesSeparators, StringSplitOptions.RemoveEmptyEntries);
		foreach (string text in array)
		{
			yield return text.Trim();
		}
	}
}
