namespace Analyzer.Utilities.PooledObjects.Extensions;

internal static class PooledHashSetExtensions
{
	public static void AddRange<T>(this PooledHashSet<T> builder, IEnumerable<T> set2)
	{
		foreach (T item in set2)
		{
			builder.Add(item);
		}
	}
}
