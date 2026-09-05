using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Text;

namespace Analyzer.Utilities;

/// <summary>
///     Provides <see langword="static" /> methods for parsing words from text.
/// </summary>
internal sealed class WordParser
{
	private const char NullChar = '\0';

	private readonly WordParserOptions _options;

	private readonly StringBuilder _buffer;

	private readonly string _text;

	private string? _peekedWord;

	private int _index;

	private char _prefix;

	private bool SkipMnemonics => (_options & WordParserOptions.IgnoreMnemonicsIndicators) == WordParserOptions.IgnoreMnemonicsIndicators;

	private bool SplitCompoundWords => (_options & WordParserOptions.SplitCompoundWords) == WordParserOptions.SplitCompoundWords;

	/// <summary>
	///     Initializes a new instance of the <see cref="T:Analyzer.Utilities.WordParser" /> class with the specified text and options.
	/// </summary>
	/// <param name="text">
	///     A <see cref="T:System.String" /> containing the text to parse.
	/// </param>
	/// <param name="options">
	///     One or more of the <see cref="T:Analyzer.Utilities.WordParserOptions" /> specifying parsing and delimiting options.
	/// </param>
	/// <exception cref="T:System.ArgumentNullException">
	///     <paramref name="text" /> is <see langword="null" />.
	/// </exception>
	/// <exception cref="T:System.ArgumentException">
	///     <paramref name="options" /> is not one or more of the <see cref="T:Analyzer.Utilities.WordParserOptions" /> values.
	/// </exception>
	public WordParser(string text, WordParserOptions options)
		: this(text, options, '\0')
	{
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="T:Analyzer.Utilities.WordParser" /> class with the specified text, options and prefix.
	/// </summary>
	/// <param name="text">
	///     A <see cref="T:System.String" /> containing the text to parse.
	/// </param>
	/// <param name="options">
	///     One or more of the <see cref="T:Analyzer.Utilities.WordParserOptions" /> specifying parsing and delimiting options.
	/// </param>
	/// <param name="prefix">
	///     A <see cref="T:System.Char" /> representing an optional prefix of <paramref name="text" />, that if present,
	///     will be returned as a separate token.
	/// </param>
	/// <exception cref="T:System.ArgumentNullException">
	///     <paramref name="text" /> is <see langword="null" />.
	/// </exception>
	/// <exception cref="T:System.ArgumentException">
	///     <paramref name="options" /> is not one or more of the <see cref="T:Analyzer.Utilities.WordParserOptions" /> values.
	/// </exception>
	public WordParser(string text, WordParserOptions options, char prefix)
	{
		if (((options < WordParserOptions.None) || options > (WordParserOptions.IgnoreMnemonicsIndicators | WordParserOptions.SplitCompoundWords)) ? true : false)
		{
			throw new ArgumentException($"'{options}' is invalid for enum type '{"WordParserOptions"}'", "options");
		}
		_text = text ?? throw new ArgumentNullException("text");
		_options = options;
		_buffer = new StringBuilder(text.Length);
		_prefix = prefix;
	}

	/// <summary>
	///     Returns the words contained in the specified text, delimiting based on the specified options.
	/// </summary>
	/// <param name="text">
	///     A <see cref="T:System.String" /> containing the text to parse.
	/// </param>
	/// <param name="options">
	///     One or more of the <see cref="T:Analyzer.Utilities.WordParserOptions" /> specifying parsing and delimiting options.
	/// </param>
	/// <returns>
	///     A <see cref="T:System.Collections.ObjectModel.Collection`1" /> of strings containing the words contained in <paramref name="text" />.
	/// </returns>
	/// <exception cref="T:System.ArgumentNullException">
	///     <paramref name="text" /> is <see langword="null" />.
	/// </exception>
	/// <exception cref="T:System.ArgumentException">
	///     <paramref name="options" /> is not one or more of the <see cref="T:Analyzer.Utilities.WordParserOptions" /> values.
	/// </exception>
	internal static Collection<string> Parse(string text, WordParserOptions options)
	{
		return Parse(text, options, '\0');
	}

	/// <summary>
	///     Returns the words contained in the specified text, delimiting based on the specified options.
	/// </summary>
	/// <param name="text">
	///     A <see cref="T:System.String" /> containing the text to parse.
	/// </param>
	/// <param name="options">
	///     One or more of the <see cref="T:Analyzer.Utilities.WordParserOptions" /> specifying parsing and delimiting options.
	/// </param>
	/// <param name="prefix">
	///     A <see cref="T:System.Char" /> representing an optional prefix of <paramref name="text" />, that if present,
	///     will be returned as a separate token.
	/// </param>
	/// <returns>
	///     A <see cref="T:System.Collections.ObjectModel.Collection`1" /> of strings containing the words contained in <paramref name="text" />.
	/// </returns>
	/// <exception cref="T:System.ArgumentNullException">
	///     <paramref name="text" /> is <see langword="null" />.
	/// </exception>
	/// <exception cref="T:System.ArgumentException">
	///     <paramref name="options" /> is not one or more of the <see cref="T:Analyzer.Utilities.WordParserOptions" /> values.
	/// </exception>
	internal static Collection<string> Parse(string text, WordParserOptions options, char prefix)
	{
		WordParser wordParser = new WordParser(text, options, prefix);
		Collection<string> collection = new Collection<string>();
		string item;
		while ((item = wordParser.NextWord()) != null)
		{
			collection.Add(item);
		}
		return collection;
	}

	/// <summary>
	///     Returns a value indicating whether at least one of the specified words occurs, using a case-insensitive ordinal comparison, within the specified text.
	/// </summary>
	/// <param name="text">
	///     A <see cref="T:System.String" /> containing the text to check.
	/// </param>
	/// <param name="options">
	///     One or more of the <see cref="T:Analyzer.Utilities.WordParserOptions" /> specifying parsing and delimiting options.
	/// </param>
	/// <param name="words">
	///     A <see cref="T:System.String" /> array containing the words to seek.
	/// </param>
	/// <returns>
	///     <see langword="true" /> if at least one of the elements within <paramref name="words" /> occurs within <paramref name="text" />, otherwise, <see langword="false" />.
	/// </returns>
	/// <exception cref="T:System.ArgumentNullException">
	///     <paramref name="text" /> is <see langword="null" />.
	///     <para>
	///      -or-
	///     </para>
	///     <paramref name="words" /> is <see langword="null" />.
	/// </exception>
	/// <exception cref="T:System.ArgumentException">
	///     <paramref name="options" /> is not one or more of the <see cref="T:Analyzer.Utilities.WordParserOptions" /> values.
	/// </exception>
	public static bool ContainsWord(string text, WordParserOptions options, ImmutableArray<string> words)
	{
		return ContainsWord(text, options, '\0', words);
	}

	/// <summary>
	///     Returns a value indicating whether at least one of the specified words occurs, using a case-insensitive ordinal comparison, within the specified text.
	/// </summary>
	/// <param name="text">
	///     A <see cref="T:System.String" /> containing the text to check.
	/// </param>
	/// <param name="options">
	///     One or more of the <see cref="T:Analyzer.Utilities.WordParserOptions" /> specifying parsing and delimiting options.
	/// </param>
	/// <param name="prefix">
	///     A <see cref="T:System.Char" /> representing an optional prefix of <paramref name="text" />, that if present,
	///     will be returned as a separate token.
	/// </param>
	/// <param name="words">
	///     A <see cref="T:System.String" /> array containing the words to seek.
	/// </param>
	/// <returns>
	///     <see langword="true" /> if at least one of the elements within <paramref name="words" /> occurs within <paramref name="text" />, otherwise, <see langword="false" />.
	/// </returns>
	/// <exception cref="T:System.ArgumentNullException">
	///     <paramref name="text" /> is <see langword="null" />.
	///     <para>
	///      -or-
	///     </para>
	///     <paramref name="words" /> is <see langword="null" />.
	/// </exception>
	/// <exception cref="T:System.ArgumentException">
	///     <paramref name="options" /> is not one or more of the <see cref="T:Analyzer.Utilities.WordParserOptions" /> values.
	/// </exception>
	internal static bool ContainsWord(string text, WordParserOptions options, char prefix, ImmutableArray<string> words)
	{
		if (words.IsDefault)
		{
			throw new ArgumentNullException("words");
		}
		WordParser wordParser = new WordParser(text, options, prefix);
		string a;
		while ((a = wordParser.NextWord()) != null)
		{
			ImmutableArray<string>.Enumerator enumerator = words.GetEnumerator();
			while (enumerator.MoveNext())
			{
				string current = enumerator.Current;
				if (string.Equals(a, current, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
		}
		return false;
	}

	/// <summary>
	///     Returns the next word in the text.
	/// </summary>
	/// <returns>
	///     A <see cref="T:System.String" /> containing the next word or <see langword="null" /> if there are no more words.
	/// </returns>
	public string? NextWord()
	{
		if (_peekedWord == null)
		{
			return NextWordCore();
		}
		string? peekedWord = _peekedWord;
		_peekedWord = null;
		return peekedWord;
	}

	/// <summary>
	///     Returns the next word in the text without consuming it.
	/// </summary>
	/// <returns>
	///     A <see cref="T:System.String" /> containing the next word or <see langword="null" /> if there are no more words.
	/// </returns>
	public string? PeekWord()
	{
		if (_peekedWord == null)
		{
			_peekedWord = NextWordCore();
		}
		return _peekedWord;
	}

	private string? NextWordCore()
	{
		_buffer.Length = 0;
		if (ParseNext())
		{
			return _buffer.ToString();
		}
		return null;
	}

	private bool ParseNext()
	{
		if (TryParsePrefix())
		{
			return true;
		}
		char c = '\0';
		char c2;
		while ((c2 = Peek()) != 0)
		{
			if (!TryParseWord(c2))
			{
				if (c != 0)
				{
					Unread();
					Skip();
					return true;
				}
				Skip();
			}
			else
			{
				c2 = Peek();
				if (!IsIntraWordPunctuation(c2))
				{
					return true;
				}
				c = c2;
				Read();
			}
		}
		if (c != 0)
		{
			Unread();
			return true;
		}
		return false;
	}

	private bool TryParseWord(char c)
	{
		if (SplitCompoundWords)
		{
			if (IsUpper(c))
			{
				ParseUppercase();
				return true;
			}
			if (IsLower(c))
			{
				ParseLowercase();
				return true;
			}
			if (IsDigit(c))
			{
				ParseNumeric();
				return true;
			}
			if (IsLetterWithoutCase(c))
			{
				ParseWithoutCase();
				return true;
			}
			if (c == '#' && IsHexDigit(Peek(2)))
			{
				ParseHex();
				return true;
			}
		}
		else if (IsLetterOrDigit(c))
		{
			ParseWholeWord();
			return true;
		}
		return false;
	}

	private bool TryParsePrefix()
	{
		if (_prefix == '\0')
		{
			return false;
		}
		if (Peek() == _prefix && !IsLower(Peek(2)))
		{
			Read();
			_prefix = '\0';
			return true;
		}
		_prefix = '\0';
		return false;
	}

	private void ParseWholeWord()
	{
		char c;
		do
		{
			Read();
			c = Peek();
		}
		while (IsLetterOrDigit(c));
	}

	private void ParseInteger()
	{
		char c;
		do
		{
			Read();
			c = Peek();
		}
		while (IsDigit(c));
	}

	private void ParseHex()
	{
		char c;
		do
		{
			Read();
			c = Peek();
		}
		while (IsHexDigit(c));
	}

	private void ParseNumeric()
	{
		char c = Peek();
		if (c == '0')
		{
			c = Peek(2);
			if ((c == 'x' || c == 'X') && IsHexDigit(Peek(3)))
			{
				Read();
				Read();
				ParseHex();
				return;
			}
		}
		ParseInteger();
	}

	private void ParseLowercase()
	{
		char c;
		do
		{
			Read();
			c = Peek();
		}
		while (IsLower(c));
	}

	private void ParseUppercase()
	{
		Read();
		char c = Peek();
		if (IsUpper(c))
		{
			ParseAllCaps();
		}
		else if (IsLower(c))
		{
			ParseLowercase();
		}
	}

	private void ParseWithoutCase()
	{
		char c;
		do
		{
			Read();
			c = Peek();
		}
		while (IsLetterWithoutCase(c));
	}

	private void ParseAllCaps()
	{
		char c;
		do
		{
			Read();
			c = Peek();
		}
		while (IsUpper(c));
		if (c == 's')
		{
			Read();
			c = Peek();
		}
		while (IsLower(c))
		{
			Unread();
			c = Peek();
		}
	}

	private void Read()
	{
		char value = Peek();
		_buffer.Append(value);
		Skip();
	}

	private void Skip()
	{
		while (_index < _text.Length)
		{
			char c = _text[_index++];
			if (!IsIgnored(c))
			{
				break;
			}
		}
	}

	private char Peek()
	{
		return Peek(1);
	}

	private char Peek(int lookAhead)
	{
		for (int i = _index; i < _text.Length; i++)
		{
			char c = _text[i];
			if (!IsIgnored(c) && --lookAhead == 0)
			{
				return c;
			}
		}
		return '\0';
	}

	private void Unread()
	{
		while (_index >= 0)
		{
			char c = _text[--_index];
			if (!IsIgnored(c))
			{
				break;
			}
		}
		_buffer.Length--;
	}

	private bool IsIgnored(char c)
	{
		if (SkipMnemonics)
		{
			if (c == '&' || c == '_')
			{
				return true;
			}
			return false;
		}
		return false;
	}

	private static bool IsLower(char c)
	{
		return char.IsLower(c);
	}

	private static bool IsUpper(char c)
	{
		return char.IsUpper(c);
	}

	private static bool IsLetterOrDigit(char c)
	{
		return char.IsLetterOrDigit(c);
	}

	private static bool IsLetterWithoutCase(char c)
	{
		if (char.IsLetter(c) && !char.IsUpper(c))
		{
			return !char.IsLower(c);
		}
		return false;
	}

	private static bool IsDigit(char c)
	{
		return char.IsDigit(c);
	}

	private static bool IsHexDigit(char c)
	{
		switch (c)
		{
		case 'A':
		case 'B':
		case 'C':
		case 'D':
		case 'E':
		case 'F':
		case 'a':
		case 'b':
		case 'c':
		case 'd':
		case 'e':
		case 'f':
			return true;
		default:
			return IsDigit(c);
		}
	}

	private static bool IsIntraWordPunctuation(char c)
	{
		switch (c)
		{
		case '\'':
		case '-':
		case '\u00ad':
		case '’':
			return true;
		default:
			return false;
		}
	}
}
