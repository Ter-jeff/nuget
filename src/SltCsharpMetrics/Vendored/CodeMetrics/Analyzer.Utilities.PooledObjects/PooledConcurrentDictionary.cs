using System.Collections.Concurrent;

namespace Analyzer.Utilities.PooledObjects;

/// <summary>
/// <see cref="T:System.Collections.Concurrent.ConcurrentDictionary`2" /> that can be recycled via an object pool.
/// </summary>
internal sealed class PooledConcurrentDictionary<K, V> : ConcurrentDictionary<K, V>, IDisposable where K : notnull
{
	private readonly ObjectPool<PooledConcurrentDictionary<K, V>>? _pool;

	private static readonly ObjectPool<PooledConcurrentDictionary<K, V>> s_poolInstance = CreatePool();

	private static readonly ConcurrentDictionary<IEqualityComparer<K>, ObjectPool<PooledConcurrentDictionary<K, V>>> s_poolInstancesByComparer = new ConcurrentDictionary<IEqualityComparer<K>, ObjectPool<PooledConcurrentDictionary<K, V>>>();

	private PooledConcurrentDictionary(ObjectPool<PooledConcurrentDictionary<K, V>>? pool)
	{
		_pool = pool;
	}

	private PooledConcurrentDictionary(ObjectPool<PooledConcurrentDictionary<K, V>>? pool, IEqualityComparer<K> keyComparer)
		: base(keyComparer)
	{
		_pool = pool;
	}

	public void Dispose()
	{
		Free(CancellationToken.None);
	}

	public void Free(CancellationToken cancellationToken)
	{
		if (!cancellationToken.IsCancellationRequested)
		{
			Clear();
			_pool?.Free(this, cancellationToken);
		}
	}

	public static ObjectPool<PooledConcurrentDictionary<K, V>> CreatePool(IEqualityComparer<K>? keyComparer = null)
	{
		IEqualityComparer<K> keyComparer2 = keyComparer;
		ObjectPool<PooledConcurrentDictionary<K, V>> pool = null;
		pool = new ObjectPool<PooledConcurrentDictionary<K, V>>(() => (keyComparer2 == null) ? new PooledConcurrentDictionary<K, V>(pool) : new PooledConcurrentDictionary<K, V>(pool, keyComparer2), 128);
		return pool;
	}

	public static PooledConcurrentDictionary<K, V> GetInstance(IEqualityComparer<K>? keyComparer = null)
	{
		return ((keyComparer == null) ? s_poolInstance : s_poolInstancesByComparer.GetOrAdd(keyComparer, CreatePool)).Allocate();
	}

	public static PooledConcurrentDictionary<K, V> GetInstance(IEnumerable<KeyValuePair<K, V>> initializer, IEqualityComparer<K>? keyComparer = null)
	{
		PooledConcurrentDictionary<K, V> instance = GetInstance(keyComparer);
		foreach (KeyValuePair<K, V> item in initializer)
		{
			instance.TryAdd(item.Key, item.Value);
		}
		return instance;
	}
}
