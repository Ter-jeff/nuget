using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Analyzer.Utilities.PooledObjects;

internal sealed class PooledDictionary<K, V> : Dictionary<K, V>, IDisposable where K : notnull
{
	private readonly ObjectPool<PooledDictionary<K, V>>? _pool;

	private static readonly ObjectPool<PooledDictionary<K, V>> s_poolInstance = CreatePool();

	private static readonly ConcurrentDictionary<IEqualityComparer<K>, ObjectPool<PooledDictionary<K, V>>> s_poolInstancesByComparer = new ConcurrentDictionary<IEqualityComparer<K>, ObjectPool<PooledDictionary<K, V>>>();

	private PooledDictionary(ObjectPool<PooledDictionary<K, V>>? pool, IEqualityComparer<K>? keyComparer)
		: base(keyComparer)
	{
		_pool = pool;
	}

	public void Dispose()
	{
		Free(CancellationToken.None);
	}

	public ImmutableDictionary<K, V> ToImmutableDictionaryAndFree()
	{
		ImmutableDictionary<K, V> result;
		if (base.Count == 0)
		{
			result = ImmutableDictionary<K, V>.Empty;
		}
		else
		{
			result = this.ToImmutableDictionary(base.Comparer);
			Clear();
		}
		_pool?.Free(this, CancellationToken.None);
		return result;
	}

	public ImmutableDictionary<TKey, TValue> ToImmutableDictionaryAndFree<TKey, TValue>(Func<KeyValuePair<K, V>, TKey> keySelector, Func<KeyValuePair<K, V>, TValue> elementSelector, IEqualityComparer<TKey> comparer) where TKey : notnull
	{
		ImmutableDictionary<TKey, TValue> result;
		if (base.Count == 0)
		{
			result = ImmutableDictionary<TKey, TValue>.Empty;
		}
		else
		{
			result = this.ToImmutableDictionary<KeyValuePair<K, V>, TKey, TValue>(keySelector, elementSelector, comparer);
			Clear();
		}
		_pool?.Free(this, CancellationToken.None);
		return result;
	}

	public void Free(CancellationToken cancellationToken)
	{
		if (!cancellationToken.IsCancellationRequested)
		{
			Clear();
			_pool?.Free(this, cancellationToken);
		}
	}

	public static ObjectPool<PooledDictionary<K, V>> CreatePool(IEqualityComparer<K>? keyComparer = null)
	{
		IEqualityComparer<K> keyComparer2 = keyComparer;
		ObjectPool<PooledDictionary<K, V>> pool = null;
		pool = new ObjectPool<PooledDictionary<K, V>>(() => new PooledDictionary<K, V>(pool, keyComparer2), 128);
		return pool;
	}

	public static PooledDictionary<K, V> GetInstance(IEqualityComparer<K>? keyComparer = null)
	{
		return ((keyComparer == null) ? s_poolInstance : s_poolInstancesByComparer.GetOrAdd(keyComparer, CreatePool)).Allocate();
	}

	public static PooledDictionary<K, V> GetInstance(IEnumerable<KeyValuePair<K, V>> initializer, IEqualityComparer<K>? keyComparer = null)
	{
		PooledDictionary<K, V> instance = GetInstance(keyComparer);
		foreach (KeyValuePair<K, V> item in initializer)
		{
			instance.Add(item.Key, item.Value);
		}
		return instance;
	}
}
