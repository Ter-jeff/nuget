using Analyzer.Utilities.PooledObjects;

namespace System.Collections.Immutable;

internal static class ImmutableHashSetExtensions
{
	public static ImmutableHashSet<T> AddRange<T>(this ImmutableHashSet<T> set1, ImmutableHashSet<T> set2)
	{
		using PooledHashSet<T> pooledHashSet = PooledHashSet<T>.GetInstance();
		foreach (T item in set1)
		{
			pooledHashSet.Add(item);
		}
		foreach (T item2 in set2)
		{
			pooledHashSet.Add(item2);
		}
		if (pooledHashSet.Count == set1.Count)
		{
			return set1;
		}
		if (pooledHashSet.Count == set2.Count)
		{
			return set2;
		}
		return pooledHashSet.ToImmutable();
	}

	public static ImmutableHashSet<T> IntersectSet<T>(this ImmutableHashSet<T> set1, ImmutableHashSet<T> set2)
	{
		if (set1.IsEmpty || set2.IsEmpty)
		{
			return ImmutableHashSet<T>.Empty;
		}
		if (set1.Count == 1)
		{
			if (!set2.Contains(set1.First()))
			{
				return ImmutableHashSet<T>.Empty;
			}
			return set1;
		}
		if (set2.Count == 1)
		{
			if (!set1.Contains(set2.First()))
			{
				return ImmutableHashSet<T>.Empty;
			}
			return set2;
		}
		using PooledHashSet<T> pooledHashSet = PooledHashSet<T>.GetInstance();
		foreach (T item in set1)
		{
			if (set2.Contains(item))
			{
				pooledHashSet.Add(item);
			}
		}
		if (pooledHashSet.Count == set1.Count)
		{
			return set1;
		}
		if (pooledHashSet.Count == set2.Count)
		{
			return set2;
		}
		return pooledHashSet.ToImmutable();
	}

	public static bool IsSubsetOfSet<T>(this ImmutableHashSet<T> set1, ImmutableHashSet<T> set2)
	{
		if (set1.Count > set2.Count)
		{
			return false;
		}
		foreach (T item in set1)
		{
			if (!set2.Contains(item))
			{
				return false;
			}
		}
		return true;
	}

	public static void AddIfNotNull<T>(this ImmutableHashSet<T>.Builder builder, T? item) where T : class
	{
		if (item != null)
		{
			builder.Add(item);
		}
	}
}
