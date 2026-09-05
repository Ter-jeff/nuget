namespace Analyzer.Utilities.Extensions;

internal static class KeyValuePairExtensions
{
	public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value)
	{
		key = pair.Key;
		value = pair.Value;
	}

	public static KeyValuePair<TKey?, TValue?> AsNullable<TKey, TValue>(this KeyValuePair<TKey, TValue> pair)
	{
		return pair;
	}
}
