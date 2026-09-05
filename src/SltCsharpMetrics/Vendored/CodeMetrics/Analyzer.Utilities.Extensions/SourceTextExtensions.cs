using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace Analyzer.Utilities.Extensions;

/// <summary>
/// Class that contains extensions to <see cref="T:Microsoft.CodeAnalysis.Text.SourceText" />.
/// </summary>
internal static class SourceTextExtensions
{
	/// <summary>
	/// Reads the <paramref name="text" /> contents into a stream and returns the result of calling the
	/// <paramref name="parser" /> function on that stream.
	/// </summary>
	/// <typeparam name="T">Type to deserialize from the <paramref name="text" />.</typeparam>
	/// <param name="text">Abstraction for an additional file's contents.</param>
	/// <param name="parser">Function that will parse <paramref name="text" /> into <typeparamref name="T" />.</param>
	/// <returns>Output from <paramref name="parser" />.</returns>
	public static T Parse<T>(this SourceText text, Func<StreamReader, T> parser)
	{
		if (text == null)
		{
			throw new ArgumentNullException("text");
		}
		if (parser == null)
		{
			throw new ArgumentNullException("parser");
		}
		using MemoryStream memoryStream = new MemoryStream();
		using (StreamWriter textWriter = new StreamWriter(memoryStream, Encoding.UTF8, 1024, leaveOpen: true))
		{
			text.Write(textWriter);
		}
		memoryStream.Position = 0L;
		using StreamReader arg = new StreamReader(memoryStream);
		return parser(arg);
	}
}
