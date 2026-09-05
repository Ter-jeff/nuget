namespace System.Collections.Immutable;

internal static class ImmutableArrayExtensions
{
	/// <summary>
	/// Returns the number of elements in a sequence.
	/// </summary>
	/// <typeparam name="TSource">he type of the elements of <paramref name="source" />.</typeparam>
	/// <param name="source">A sequence that contains elements to be counted.</param>
	/// <returns>The number of elements in the input sequence.</returns>
	public static int Count<TSource>(this ImmutableArray<TSource> source)
	{
		return source.Length;
	}

	/// <summary>
	/// Determines whether a sequence contains, exactly, <paramref name="count" /> elements.
	/// </summary>
	/// <typeparam name="TSource">The type of the elements of source.</typeparam>
	/// <param name="source">The <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> to check for cardinality.</param>
	/// <param name="count">The number of elements to ensure exists.</param>
	/// <returns><see langword="true" /> the source sequence contains, exactly, <paramref name="count" /> elements; otherwise, <see langword="false" />.</returns>
	public static bool HasExactly<TSource>(this ImmutableArray<TSource> source, int count)
	{
		return source.Length == count;
	}

	/// <summary>
	/// Determines whether a sequence contains more than <paramref name="count" /> elements.
	/// </summary>
	/// <typeparam name="TSource">The type of the elements of source.</typeparam>
	/// <param name="source">The <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> to check for cardinality.</param>
	/// <param name="count">The number of elements to ensure exists.</param>
	/// <returns><see langword="true" /> the source sequence contains more than <paramref name="count" /> elements; otherwise, <see langword="false" />.</returns>
	public static bool HasMoreThan<TSource>(this ImmutableArray<TSource> source, int count)
	{
		return source.Length > count;
	}

	/// <summary>
	/// Determines whether a sequence contains fewer than <paramref name="count" /> elements.
	/// </summary>
	/// <typeparam name="TSource">The type of the elements of source.</typeparam>
	/// <param name="source">The <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> to check for cardinality.</param>
	/// <param name="count">The number of elements to ensure exists.</param>
	/// <returns><see langword="true" /> the source sequence contains less then <paramref name="count" /> elements; otherwise, <see langword="false" />.</returns>
	public static bool HasFewerThan<TSource>(this ImmutableArray<TSource> source, int count)
	{
		return source.Length < count;
	}

	/// <summary>
	/// Determines whether a sequence contains any elements.
	/// </summary>
	/// <typeparam name="T">The type of the elements of array.</typeparam>
	/// <typeparam name="TArg">The type of arg.</typeparam>
	/// <param name="array">The <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> whose elements to apply the predicate to.</param>
	/// <param name="predicate">A function to test each element for a condition.</param>
	/// <param name="arg">The argument to pass into the predicate.</param>
	/// <returns> true if any elements in the source sequence pass the test in the specified predicate otherwise, false.</returns>
	public static bool Any<T, TArg>(this ImmutableArray<T> array, Func<T, TArg, bool> predicate, TArg arg)
	{
		ImmutableArray<T>.Enumerator enumerator = array.GetEnumerator();
		while (enumerator.MoveNext())
		{
			T current = enumerator.Current;
			if (predicate(current, arg))
			{
				return true;
			}
		}
		return false;
	}
}
