using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities.Extensions;

internal static class MethodKindEx
{
	public const MethodKind LocalFunction = MethodKind.LocalFunction;

	/// <summary>
	/// This will only compile if <see cref="F:Analyzer.Utilities.Extensions.MethodKindEx.LocalFunction" /> and <see cref="F:Microsoft.CodeAnalysis.MethodKind.LocalFunction" /> have the
	/// same value.
	/// </summary>
	/// <remarks>
	/// <para>The subtraction in <see cref="F:Analyzer.Utilities.Extensions.MethodKindEx.LocalFunctionValueAssertion1" /> will overflow if <see cref="F:Microsoft.CodeAnalysis.MethodKind.LocalFunction" /> is greater, and the conversion
	/// to an unsigned value after negation in <see cref="F:Analyzer.Utilities.Extensions.MethodKindEx.LocalFunctionValueAssertion2" /> will overflow if <see cref="F:Analyzer.Utilities.Extensions.MethodKindEx.LocalFunction" /> is greater.</para>
	/// </remarks>
	private const uint LocalFunctionValueAssertion1 = 0u;

	/// <summary>
	/// This will only compile if <see cref="F:Analyzer.Utilities.Extensions.MethodKindEx.LocalFunction" /> and <see cref="F:Microsoft.CodeAnalysis.MethodKind.LocalFunction" /> have the
	/// same value.
	/// </summary>
	/// <remarks>
	/// <para>The subtraction in <see cref="F:Analyzer.Utilities.Extensions.MethodKindEx.LocalFunctionValueAssertion1" /> will overflow if <see cref="F:Microsoft.CodeAnalysis.MethodKind.LocalFunction" /> is greater, and the conversion
	/// to an unsigned value after negation in <see cref="F:Analyzer.Utilities.Extensions.MethodKindEx.LocalFunctionValueAssertion2" /> will overflow if <see cref="F:Analyzer.Utilities.Extensions.MethodKindEx.LocalFunction" /> is greater.</para>
	/// </remarks>
	private const uint LocalFunctionValueAssertion2 = 0u;
}
