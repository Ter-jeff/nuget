namespace Analyzer.Utilities.PooledObjects;

internal struct TemporaryDictionary<TKey, TValue> where TKey : notnull
{
	public readonly struct Enumerable
	{
		private readonly Dictionary<TKey, TValue> _dictionary;

		public Enumerable(Dictionary<TKey, TValue> dictionary)
		{
			_dictionary = dictionary;
		}

		public Enumerator GetEnumerator()
		{
			return new Enumerator(_dictionary.GetEnumerator());
		}
	}

	public struct Enumerator
	{
		private Dictionary<TKey, TValue>.Enumerator _enumerator;

		public KeyValuePair<TKey, TValue> Current => _enumerator.Current;

		public Enumerator(Dictionary<TKey, TValue>.Enumerator enumerator)
		{
			_enumerator = enumerator;
		}

		public bool MoveNext()
		{
			return _enumerator.MoveNext();
		}
	}

	public static readonly TemporaryDictionary<TKey, TValue> Empty;

	/// <summary>
	/// An empty dictionary used for creating non-null enumerators when no items have been added to the dictionary.
	/// </summary>
	private static readonly Dictionary<TKey, TValue> EmptyDictionary = new Dictionary<TKey, TValue>();

	private PooledDictionary<TKey, TValue>? _storage;

	public readonly Enumerable NonConcurrentEnumerable => new Enumerable(_storage ?? EmptyDictionary);

	public void Free(CancellationToken cancellationToken)
	{
		Interlocked.Exchange(ref _storage, null)?.Free(cancellationToken);
	}

	private PooledDictionary<TKey, TValue> GetOrCreateStorage(CancellationToken cancellationToken)
	{
		PooledDictionary<TKey, TValue> pooledDictionary = _storage;
		if (pooledDictionary == null)
		{
			PooledDictionary<TKey, TValue> instance = PooledDictionary<TKey, TValue>.GetInstance();
			pooledDictionary = Interlocked.CompareExchange(ref _storage, instance, null) ?? instance;
			if (pooledDictionary != instance)
			{
				instance.Free(cancellationToken);
			}
		}
		return pooledDictionary;
	}

	internal void Add(TKey key, TValue value, CancellationToken cancellationToken)
	{
		PooledDictionary<TKey, TValue> orCreateStorage = GetOrCreateStorage(cancellationToken);
		lock (orCreateStorage)
		{
			orCreateStorage.Add(key, value);
		}
	}
}
