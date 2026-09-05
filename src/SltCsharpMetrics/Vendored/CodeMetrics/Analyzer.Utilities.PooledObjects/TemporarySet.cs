namespace Analyzer.Utilities.PooledObjects;

internal struct TemporarySet<T>
{
	public readonly struct Enumerable
	{
		private readonly HashSet<T> _set;

		public Enumerable(HashSet<T> set)
		{
			_set = set;
		}

		public Enumerator GetEnumerator()
		{
			return new Enumerator(_set.GetEnumerator());
		}
	}

	public struct Enumerator
	{
		private HashSet<T>.Enumerator _enumerator;

		public T Current => _enumerator.Current;

		public Enumerator(HashSet<T>.Enumerator enumerator)
		{
			_enumerator = enumerator;
		}

		public bool MoveNext()
		{
			return _enumerator.MoveNext();
		}
	}

	public static readonly TemporarySet<T> Empty;

	/// <summary>
	/// An empty set used for creating non-null enumerators when no items have been added to the set.
	/// </summary>
	private static readonly HashSet<T> EmptyHashSet = new HashSet<T>();

	private PooledHashSet<T>? _storage;

	public readonly Enumerable NonConcurrentEnumerable => new Enumerable(_storage ?? EmptyHashSet);

	public void Free(CancellationToken cancellationToken)
	{
		Interlocked.Exchange(ref _storage, null)?.Free(cancellationToken);
	}

	private PooledHashSet<T> GetOrCreateStorage(CancellationToken cancellationToken)
	{
		PooledHashSet<T> pooledHashSet = _storage;
		if (pooledHashSet == null)
		{
			PooledHashSet<T> instance = PooledHashSet<T>.GetInstance();
			pooledHashSet = Interlocked.CompareExchange(ref _storage, instance, null) ?? instance;
			if (pooledHashSet != instance)
			{
				instance.Free(cancellationToken);
			}
		}
		return pooledHashSet;
	}

	public bool Add(T item, CancellationToken cancellationToken)
	{
		PooledHashSet<T> orCreateStorage = GetOrCreateStorage(cancellationToken);
		lock (orCreateStorage)
		{
			return orCreateStorage.Add(item);
		}
	}

	public readonly bool Contains(T item)
	{
		PooledHashSet<T> storage = _storage;
		if (storage == null)
		{
			return false;
		}
		lock (storage)
		{
			return storage.Contains(item);
		}
	}

	public readonly bool Contains_NonConcurrent(T item)
	{
		return _storage?.Contains(item) ?? false;
	}

	public readonly bool Any_NonConcurrent()
	{
		PooledHashSet<T> storage = _storage;
		if (storage == null)
		{
			return false;
		}
		return storage.Count > 0;
	}

	public readonly Enumerator GetEnumerator_NonConcurrent()
	{
		return new Enumerator((_storage ?? EmptyHashSet).GetEnumerator());
	}

	public readonly IEnumerable<T> AsEnumerable_NonConcurrent()
	{
		return (_storage ?? EmptyHashSet).AsEnumerable();
	}
}
