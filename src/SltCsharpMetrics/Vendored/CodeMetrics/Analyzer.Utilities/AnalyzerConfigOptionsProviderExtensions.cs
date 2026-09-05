using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Analyzer.Utilities;

internal static class AnalyzerConfigOptionsProviderExtensions
{
	public static bool IsEmpty(this AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider)
	{
		if (analyzerConfigOptionsProvider.GetType().GetField("_treeDict", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(analyzerConfigOptionsProvider) is ImmutableDictionary<object, AnalyzerConfigOptions> immutableDictionary)
		{
			return immutableDictionary.IsEmpty;
		}
		return false;
	}
}
