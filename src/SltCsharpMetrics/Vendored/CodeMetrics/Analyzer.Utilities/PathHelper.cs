namespace Analyzer.Utilities;

internal static class PathHelper
{
	private static readonly char[] DirectorySeparatorCharacters = new char[2]
	{
		Path.DirectorySeparatorChar,
		Path.AltDirectorySeparatorChar
	};

	public static ReadOnlySpan<char> GetFileName(string? path)
	{
		if (RoslynString.IsNullOrEmpty(path))
		{
			return ReadOnlySpan<char>.Empty;
		}
		int num = path.LastIndexOfAny(DirectorySeparatorCharacters);
		if (num < 0)
		{
			return path.AsSpan();
		}
		ReadOnlySpan<char> readOnlySpan = path.AsSpan();
		int num2 = num + 1;
		return readOnlySpan.Slice(num2, readOnlySpan.Length - num2);
	}
}
