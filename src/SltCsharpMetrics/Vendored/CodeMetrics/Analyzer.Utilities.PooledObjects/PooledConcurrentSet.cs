using System.Collections;

namespace Analyzer.Utilities.PooledObjects;

/// <summary>
/// <see cref="T:System.Collections.Concurrent.ConcurrentDictionary`2" /> that can be recycled via an object pool.
/// </summary>
internal sealed class PooledConcurrentSet<T> : ICollection<T>, IEnumerable<T>, IEnumerable, IDisposable where T : notnull
{
	public readonly struct KeyEnumerator
	{
		private readonly IEnumerator<KeyValuePair<T, byte>> _kvpEnumerator;

		public T Current => _kvpEnumerator.Current.Key;

		internal KeyEnumerator(IEnumerable<KeyValuePair<T, byte>> data)
		{
			_kvpEnumerator = data.GetEnumerator();
		}

		public bool MoveNext()
		{
			return _kvpEnumerator.MoveNext();
		}

		public void Reset()
		{
			_kvpEnumerator.Reset();
		}
	}

	private readonly PooledConcurrentDictionary<T, byte> _dictionary;

	/// <summary>
	/// Obtain the number of elements in the set.
	/// </summary>
	/// <returns>The number of elements in the set.</returns>
	public int Count => _dictionary.Count;

	/// <summary>
	/// Determine whether the set is empty.</summary>
	/// <returns>true if the set is empty; otherwise, false.</returns>
	public bool IsEmpty => _dictionary.IsEmpty;

	public bool IsReadOnly => false;

	private PooledConcurrentSet(PooledConcurrentDictionary<T, byte> dictionary)
	{
		_dictionary = dictionary;
	}

	public void Dispose()
	{
		Free(CancellationToken.None);
	}

	public void Free(CancellationToken cancellationToken)
	{
		_dictionary.Free(cancellationToken);
	}

	public static PooledConcurrentSet<T> GetInstance(IEqualityComparer<T>? comparer = null)
	{
		return new PooledConcurrentSet<T>(PooledConcurrentDictionary<T, byte>.GetInstance(comparer));
	}

	public static PooledConcurrentSet<T> GetInstance(IEnumerable<T> initializer, IEqualityComparer<T>? comparer = null)
	{
		PooledConcurrentSet<T> instance = GetInstance(comparer);
		foreach (T item in initializer)
		{
			instance.Add(item);
		}
		return instance;
	}

	/// <summary>
	/// Attempts to add a <paramref name="value" /> to the set.
	/// </summary>
	/// <param name="value">The value to add.</param>
	/// <returns>true if the value was added to the set. If the value already exists, this method returns false.</returns>
	public bool Add(T value)
	{
		return _dictionary.TryAdd(value, 0);
	}

	/// <summary>
	/// Adds the given <paramref name="values" /> to the set.
	/// </summary>
	public void AddRange(IEnumerable<T>? values)
	{
		if (values == null)
		{
			return;
		}
		foreach (T value in values)
		{
			Add(value);
		}
	}

	/// <summary>
	/// Attempts to remove a value from the set.
	/// </summary>
	/// <param name="item">The value to remove.</param>
	/// <returns>true if the value was removed successfully; otherwise false.</returns>
	public bool Remove(T item)
	{
		byte value;
		return _dictionary.TryRemove(item, out value);
	}

	/// <summary>
	/// Clears all the elements from the set.
	/// </summary>
	public void Clear()
	{
		_dictionary.Clear();
	}

	/// <summary>
	/// Returns true if the given <paramref name="item" /> is present in the set.
	/// </summary>
	public bool Contains(T item)
	{
		return _dictionary.ContainsKey(item);
	}

	public void CopyTo(T[] array, int arrayIndex)
	{
		throw new NotImplementedException();
	}

	/// <summary>
	/// Obtain an enumerator that iterates through the elements in the set.
	/// </summary>
	/// <returns>An enumerator for the set.</returns>
	public KeyEnumerator GetEnumerator()
	{
		return new KeyEnumerator(_dictionary);
	}

	private IEnumerator<T> GetEnumeratorCore()
	{
		foreach (KeyValuePair<T, byte> item in _dictionary)
		{
			yield return item.Key;
		}
	}

	IEnumerator<T> IEnumerable<T>.GetEnumerator()
	{
		return GetEnumeratorCore();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumeratorCore();
	}

	void ICollection<T>.Add(T item)
	{
		Add(item);
	}
}
