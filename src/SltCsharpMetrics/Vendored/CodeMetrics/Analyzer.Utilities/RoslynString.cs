using System.Diagnostics.CodeAnalysis;

namespace Analyzer.Utilities;

internal static class RoslynString
{
	/// <inheritdoc cref="M:System.String.IsNullOrEmpty(System.String)" />
	public static bool IsNullOrEmpty([NotNullWhen(false)] string? value)
	{
		return string.IsNullOrEmpty(value);
	}

	/// <inheritdoc cref="M:System.String.IsNullOrWhiteSpace(System.String)" />
	public static bool IsNullOrWhiteSpace([NotNullWhen(false)] string? value)
	{
		return string.IsNullOrWhiteSpace(value);
	}
}
