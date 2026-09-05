namespace Analyzer.Utilities;

/// <summary>
/// MSBuild item names that are required to be threaded as analyzer config options.
/// The analyzer config option will have the following key/value:
/// - Key: Item name prefixed with an '_' and suffixed with a 'List' to reduce chances of conflicts with any existing project property.
/// - Value: Concatenated item metadata values, separated by a ',' character. See https://github.com/dotnet/sdk/issues/12706#issuecomment-668219422 for details.
/// </summary>
internal static class MSBuildItemOptionNames
{
	public const string SupportedPlatform = "SupportedPlatform";
}
