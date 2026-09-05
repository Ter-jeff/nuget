namespace Analyzer.Utilities;

/// <summary>
/// MSBuild property names that are required to be threaded as analyzer config options.
/// </summary>
/// <remarks>const fields in this type are automatically discovered and used to generate build_properties entries in the generated .globalconfig</remarks>
internal static class MSBuildPropertyOptionNames
{
	public const string TargetFramework = "TargetFramework";

	public const string TargetPlatformMinVersion = "TargetPlatformMinVersion";

	public const string UsingMicrosoftNETSdkWeb = "UsingMicrosoftNETSdkWeb";

	public const string ProjectTypeGuids = "ProjectTypeGuids";

	public const string InvariantGlobalization = "InvariantGlobalization";

	public const string PlatformNeutralAssembly = "PlatformNeutralAssembly";

	public const string EnforceExtendedAnalyzerRules = "EnforceExtendedAnalyzerRules";
}
