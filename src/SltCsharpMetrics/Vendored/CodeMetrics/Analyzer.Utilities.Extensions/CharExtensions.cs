namespace Analyzer.Utilities.Extensions;

internal static class CharExtensions
{
	public static bool IsAscii(this char c)
	{
		return (uint)c <= 127u;
	}

	/// <summary>
	/// Returns whether the char is a printable ascii character [x0020, x007e].
	/// </summary>
	public static bool IsPrintableAscii(this char c)
	{
		if ((uint)c >= 32u)
		{
			return (uint)c <= 126u;
		}
		return false;
	}
}
