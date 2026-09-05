using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Analyzer.Utilities.PooledObjects;

internal sealed class PooledHashSet<T> : HashSet<T>, IDisposable
{
	private readonly ObjectPool<PooledHashSet<T>>? _pool;

	private static readonly ObjectPool<PooledHashSet<T>> s_poolInstance = CreatePool();

	private static readonly ConcurrentDictionary<IEqualityComparer<T>, ObjectPool<PooledHashSet<T>>> s_poolInstancesByComparer = new ConcurrentDictionary<IEqualityComparer<T>, ObjectPool<PooledHashSet<T>>>();

	private PooledHashSet(ObjectPool<PooledHashSet<T>>? pool, IEqualityComparer<T>? comparer)
		: base(comparer)
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

	public ImmutableHashSet<T> ToImmutableAndFree()
	{
		ImmutableHashSet<T> result;
		if (base.Count == 0)
		{
			result = ImmutableHashSet<T>.Empty;
		}
		else
		{
			result = this.ToImmutableHashSet(base.Comparer);
			Clear();
		}
		_pool?.Free(this, CancellationToken.None);
		return result;
	}

	public ImmutableHashSet<T> ToImmutable()
	{
		if (base.Count != 0)
		{
			return this.ToImmutableHashSet(base.Comparer);
		}
		return ImmutableHashSet<T>.Empty;
	}

	public static ObjectPool<PooledHashSet<T>> CreatePool(IEqualityComparer<T>? comparer = null)
	{
		IEqualityComparer<T> comparer2 = comparer;
		ObjectPool<PooledHashSet<T>> pool = null;
		pool = new ObjectPool<PooledHashSet<T>>(() => new PooledHashSet<T>(pool, comparer2), 128);
		return pool;
	}

	public static PooledHashSet<T> GetInstance(IEqualityComparer<T>? comparer = null)
	{
		return ((comparer == null) ? s_poolInstance : s_poolInstancesByComparer.GetOrAdd(comparer, CreatePool)).Allocate();
	}

	public static PooledHashSet<T> GetInstance(IEnumerable<T> initializer, IEqualityComparer<T>? comparer = null)
	{
		PooledHashSet<T> instance = GetInstance(comparer);
		foreach (T item in initializer)
		{
			instance.Add(item);
		}
		return instance;
	}
}
