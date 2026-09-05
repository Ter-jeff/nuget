using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Analyzer.Utilities.Extensions;

internal static class IEnumerableExtensions
{
	private sealed class ComparisonComparer<T> : Comparer<T>
	{
		private readonly Comparison<T> _compare;

		public ComparisonComparer(Comparison<T> compare)
		{
			_compare = compare;
		}

		public override int Compare([AllowNull] T x, [AllowNull] T y)
		{
			if (x == null)
			{
				if (y != null)
				{
					return -1;
				}
				return 0;
			}
			if (y == null)
			{
				return 1;
			}
			return _compare(x, y);
		}
	}

	private static readonly Func<object?, bool> s_notNullTest = (object? x) => x != null;

	public static IEnumerable<T> Concat<T>(this IEnumerable<T> source, T value)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		return ConcatImpl(source, value);
		static IEnumerable<T> ConcatImpl(IEnumerable<T> source, T value)
		{
			foreach (T item in source)
			{
				yield return item;
			}
			yield return value;
		}
	}

	public static ISet<T> ToSet<T>(this IEnumerable<T> source)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		return (source as ISet<T>) ?? new HashSet<T>(source);
	}

	public static IEnumerable<T> OrderBy<T>(this IEnumerable<T> source, IComparer<T> comparer)
	{
		return source.OrderBy((T t) => t, comparer);
	}

	public static IEnumerable<T> OrderBy<T>(this IEnumerable<T> source, Comparison<T> compare)
	{
		return source.OrderBy(new ComparisonComparer<T>(compare));
	}

	public static IEnumerable<T> Order<T>(this IEnumerable<T> source) where T : IComparable<T>
	{
		return source.OrderBy((T t1, T t2) => t1.CompareTo(t2));
	}

	public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source) where T : class
	{
		if (source == null)
		{
			return ImmutableArray<T>.Empty;
		}
		return source.Where<T>((Func<T, bool>)s_notNullTest);
	}

	public static ImmutableArray<TSource> WhereAsArray<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> selector)
	{
		ImmutableArray<TSource>.Builder builder = ImmutableArray.CreateBuilder<TSource>();
		bool flag = false;
		foreach (TSource item in source)
		{
			if (selector(item))
			{
				flag = true;
				builder.Add(item);
			}
		}
		if (flag)
		{
			return builder.ToImmutable();
		}
		return ImmutableArray<TSource>.Empty;
	}

	public static void Dispose<T>(this IEnumerable<T?> collection) where T : class, IDisposable
	{
		foreach (T item in collection)
		{
			item?.Dispose();
		}
	}

	/// <summary>
	/// Determines whether a sequence contains, exactly, <paramref name="count" /> elements.
	/// </summary>
	/// <typeparam name="TSource">The type of the elements of source.</typeparam>
	/// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to check for cardinality.</param>
	/// <param name="count">The number of elements to ensure exists.</param>
	/// <returns><see langword="true" /> the source sequence contains, exactly, <paramref name="count" /> elements; otherwise, <see langword="false" />.</returns>
	/// <exception cref="T:System.ArgumentNullException"><paramref name="source" /> is null.</exception>
	public static bool HasExactly<TSource>(this IEnumerable<TSource> source, int count)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		if (source is ICollection<TSource> collection)
		{
			return collection.Count == count;
		}
		if (source is ICollection collection2)
		{
			return collection2.Count == count;
		}
		using IEnumerator<TSource> enumerator = source.GetEnumerator();
		while (count-- > 0)
		{
			if (!enumerator.MoveNext())
			{
				return false;
			}
		}
		return !enumerator.MoveNext();
	}

	/// <summary>
	/// Determines whether a sequence contains more than <paramref name="count" /> elements.
	/// </summary>
	/// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
	/// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to check for cardinality.</param>
	/// <param name="count">The number of elements to ensure exists.</param>
	/// <returns><see langword="true" /> the source sequence contains more than <paramref name="count" /> elements; otherwise, <see langword="false" />.</returns>
	/// <exception cref="T:System.ArgumentNullException"><paramref name="source" /> is null.</exception>
	public static bool HasMoreThan<TSource>(this IEnumerable<TSource> source, int count)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		if (source is ICollection<TSource> collection)
		{
			return collection.Count > count;
		}
		if (source is ICollection collection2)
		{
			return collection2.Count > count;
		}
		using IEnumerator<TSource> enumerator = source.GetEnumerator();
		while (count-- > 0)
		{
			if (!enumerator.MoveNext())
			{
				return false;
			}
		}
		return enumerator.MoveNext();
	}

	/// <summary>
	/// Determines whether a sequence contains fewer than <paramref name="count" /> elements.
	/// </summary>
	/// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
	/// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to check for cardinality.</param>
	/// <param name="count">The number of elements to ensure exists.</param>
	/// <returns><see langword="true" /> the source sequence contains less than <paramref name="count" /> elements; otherwise, <see langword="false" />.</returns>
	/// <exception cref="T:System.ArgumentNullException"><paramref name="source" /> is null.</exception>
	public static bool HasFewerThan<TSource>(this IEnumerable<TSource> source, int count)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		if (source is ICollection<TSource> collection)
		{
			return collection.Count < count;
		}
		if (source is ICollection collection2)
		{
			return collection2.Count < count;
		}
		using IEnumerator<TSource> enumerator = source.GetEnumerator();
		while (count > 0 && enumerator.MoveNext())
		{
			count--;
		}
		return count > 0;
	}
}
