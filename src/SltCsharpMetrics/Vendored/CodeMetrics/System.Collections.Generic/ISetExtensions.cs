namespace System.Collections.Generic;

internal static class ISetExtensions
{
	public static void AddRange<T>(this ISet<T> set, IEnumerable<T> values)
	{
		foreach (T value in values)
		{
			set.Add(value);
		}
	}
}
