namespace Analyzer.Utilities;

internal static class FxCopWellKnownDiagnosticTags
{
	public const string PortedFromFxCop = "PortedFromFxCop";

	public static readonly string[] PortedFxCopRule = new string[2] { "PortedFromFxCop", "Telemetry" };

	public static readonly string[] PortedFxCopRuleEnabledInAggressiveMode = new string[3] { "PortedFromFxCop", "Telemetry", "EnabledRuleInAggressiveMode" };

	public static readonly string[] PortedFxCopDataflowRule = new string[3] { "PortedFromFxCop", "Dataflow", "Telemetry" };

	public static readonly string[] PortedFxCopDataflowRuleEnabledInAggressiveMode = new string[4] { "PortedFromFxCop", "Dataflow", "Telemetry", "EnabledRuleInAggressiveMode" };
}
