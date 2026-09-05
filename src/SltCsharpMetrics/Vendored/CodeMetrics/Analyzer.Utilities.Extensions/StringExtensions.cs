using System.Text;

namespace Analyzer.Utilities.Extensions;

internal static class StringExtensions
{
	public static bool HasSuffix(this string str, string suffix)
	{
		if (str == null)
		{
			throw new ArgumentNullException("str");
		}
		if (suffix == null)
		{
			throw new ArgumentNullException("suffix");
		}
		return str.EndsWith(suffix, StringComparison.Ordinal);
	}

	public static string WithoutSuffix(this string str, string suffix)
	{
		if (str == null)
		{
			throw new ArgumentNullException("str");
		}
		if (suffix == null)
		{
			throw new ArgumentNullException("suffix");
		}
		if (!str.HasSuffix(suffix))
		{
			throw new ArgumentException($"The string {str} does not end with the suffix {suffix}.", "str");
		}
		int length = suffix.Length;
		return str.Substring(0, str.Length - length);
	}

	public static bool IsASCII(this string value)
	{
		return Encoding.UTF8.GetByteCount(value) == value.Length;
	}
}
