namespace Analyzer.Utilities;

/// <summary>
/// Describes a group of effective <see cref="T:Analyzer.Utilities.Extensions.SymbolVisibility" /> for symbols.
/// </summary>
[Flags]
public enum SymbolVisibilityGroup
{
	None = 0,
	Public = 1,
	Internal = 2,
	Private = 4,
	Friend = 2,
	All = 7
}
