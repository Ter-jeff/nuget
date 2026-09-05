using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Analyzer.Utilities;

internal static class RoslynDebug
{
	/// <inheritdoc cref="M:System.Diagnostics.Debug.Assert(System.Boolean)" />
	[Conditional("DEBUG")]
	public static void Assert([DoesNotReturnIf(false)] bool b)
	{
	}

	/// <inheritdoc cref="M:System.Diagnostics.Debug.Assert(System.Boolean,System.String)" />
	[Conditional("DEBUG")]
	public static void Assert([DoesNotReturnIf(false)] bool b, string message)
	{
	}
}
