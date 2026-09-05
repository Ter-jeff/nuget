namespace Analyzer.Utilities;

/// <summary>
///   Defines the word parsing and delimiting options for use with <see cref="M:Analyzer.Utilities.WordParser.Parse(System.String,Analyzer.Utilities.WordParserOptions)" />.
/// </summary>
[Flags]
internal enum WordParserOptions
{
	/// <summary>
	///   Indicates the default options for word parsing.
	/// </summary>
	None = 0,
	/// <summary>
	///   Indicates that <see cref="M:Analyzer.Utilities.WordParser.Parse(System.String,Analyzer.Utilities.WordParserOptions)" /> should ignore the mnemonic indicator characters (&amp;) embedded within words.
	/// </summary>
	IgnoreMnemonicsIndicators = 1,
	/// <summary>
	///   Indicates that <see cref="M:Analyzer.Utilities.WordParser.Parse(System.String,Analyzer.Utilities.WordParserOptions)" /> should split compound words.
	/// </summary>
	SplitCompoundWords = 2
}
