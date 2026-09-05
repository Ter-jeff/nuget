namespace Microsoft.CodeAnalysis;

internal static class WellKnownDiagnosticTagsExtensions
{
	public const string EnabledRuleInAggressiveMode = "EnabledRuleInAggressiveMode";

	public const string Dataflow = "Dataflow";

	public const string CompilationEnd = "CompilationEnd";

	public static readonly string[] DataflowAndTelemetry = new string[2] { "Dataflow", "Telemetry" };

	public static readonly string[] DataflowAndTelemetryEnabledInAggressiveMode = new string[3] { "Dataflow", "Telemetry", "EnabledRuleInAggressiveMode" };

	public static readonly string[] Telemetry = new string[1] { "Telemetry" };

	public static readonly string[] TelemetryEnabledInAggressiveMode = new string[2] { "Telemetry", "EnabledRuleInAggressiveMode" };

	public static readonly string[] CompilationEndAndTelemetry = new string[2] { "CompilationEnd", "Telemetry" };
}
