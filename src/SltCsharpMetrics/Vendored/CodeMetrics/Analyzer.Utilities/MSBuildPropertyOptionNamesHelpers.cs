using System.Diagnostics;

namespace Analyzer.Utilities;

internal static class MSBuildPropertyOptionNamesHelpers
{
	[Conditional("DEBUG")]
	public static void VerifySupportedPropertyOptionName(string propertyOptionName)
	{
	}
}
