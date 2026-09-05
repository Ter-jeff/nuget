namespace Analyzer.Utilities;

/// <summary>
/// Describes a group of modifiers for symbol declaration.
/// </summary>
[Flags]
public enum SymbolModifiers
{
	None = 0,
	Static = 1,
	Shared = 1,
	Const = 2,
	ReadOnly = 4,
	Abstract = 8,
	Virtual = 0x10,
	Override = 0x20,
	Sealed = 0x40,
	Extern = 0x80,
	Async = 0x100
}
