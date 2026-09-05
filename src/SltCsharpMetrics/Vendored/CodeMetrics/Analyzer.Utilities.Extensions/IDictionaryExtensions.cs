namespace Analyzer.Utilities.Extensions;

internal static class IDictionaryExtensions
{
	public static void AddKeyValueIfNotNull<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey? key, TValue? value) where TKey : class where TValue : class
	{
		if (key != null && value != null)
		{
			dictionary.Add(key, value);
		}
	}

	public static void AddRange<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, IEnumerable<KeyValuePair<TKey, TValue>> items) where TKey : notnull
	{
		foreach (KeyValuePair<TKey, TValue> item in items)
		{
			dictionary.Add(item);
		}
	}

	public static bool IsEqualTo<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, IReadOnlyDictionary<TKey, TValue> other) where TKey : notnull
	{
		IReadOnlyDictionary<TKey, TValue> other2 = other;
		IReadOnlyDictionary<TKey, TValue> dictionary2 = dictionary;
		if (dictionary2.Count == other2.Count)
		{
			return dictionary2.Keys.All(delegate(TKey key)
			{
				if (other2.ContainsKey(key))
				{
					TValue val = dictionary2[key];
					if (val == null)
					{
						return false;
					}
					return val.Equals(other2[key]);
				}
				return false;
			});
		}
		return false;
	}
}
