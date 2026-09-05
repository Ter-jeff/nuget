namespace Analyzer.Utilities;

[Flags]
internal enum ValueUsageInfo
{
	/// <summary>
	/// Represents default value indicating no usage.
	/// </summary>
	None = 0,
	/// <summary>
	/// Represents a value read.
	/// For example, reading the value of a local/field/parameter.
	/// </summary>
	Read = 1,
	/// <summary>
	/// Represents a value write.
	/// For example, assigning a value to a local/field/parameter.
	/// </summary>
	Write = 2,
	/// <summary>
	/// Represents a reference being taken for the symbol.
	/// For example, passing an argument to an "in", "ref" or "out" parameter.
	/// </summary>
	Reference = 4,
	/// <summary>
	/// Represents a name-only reference that neither reads nor writes the underlying value.
	/// For example, 'nameof(x)' or reference to a symbol 'x' in a documentation comment
	/// does not read or write the underlying value stored in 'x'.
	/// </summary>
	Name = 8,
	/// <summary>
	/// Represents a value read and/or write.
	/// For example, an increment or compound assignment operation.
	/// </summary>
	ReadWrite = 3,
	/// <summary>
	/// Represents a readable reference being taken to the value.
	/// For example, passing an argument to an "in" or "ref readonly" parameter.
	/// </summary>
	ReadableReference = 5,
	/// <summary>
	/// Represents a readable reference being taken to the value.
	/// For example, passing an argument to an "out" parameter.
	/// </summary>
	WritableReference = 6,
	/// <summary>
	/// Represents a value read or write.
	/// For example, passing an argument to a "ref" parameter.
	/// </summary>
	ReadableWritableReference = 7
}
