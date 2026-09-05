namespace Analyzer.Utilities;

internal static class ValueUsageInfoExtensions
{
	public static bool IsReadFrom(this ValueUsageInfo valueUsageInfo)
	{
		return (valueUsageInfo & ValueUsageInfo.Read) != 0;
	}

	public static bool IsWrittenTo(this ValueUsageInfo valueUsageInfo)
	{
		return (valueUsageInfo & ValueUsageInfo.Write) != 0;
	}

	public static bool IsNameOnly(this ValueUsageInfo valueUsageInfo)
	{
		return (valueUsageInfo & ValueUsageInfo.Name) != 0;
	}

	public static bool IsReference(this ValueUsageInfo valueUsageInfo)
	{
		return (valueUsageInfo & ValueUsageInfo.Reference) != 0;
	}
}
