using System.Collections.Concurrent;

namespace Analyzer.Utilities.PooledObjects;

/// <summary>
/// Pooled <see cref="T:System.Collections.Generic.SortedSet`1" />.
/// </summary>
/// <typeparam name="T">Type of elements in the set.</typeparam>
internal sealed class PooledSortedSet<T> : SortedSet<T>, IDisposable
{
	private readonly ObjectPool<PooledSortedSet<T>>? _pool;

	private static readonly ObjectPool<PooledSortedSet<T>> s_poolInstance = CreatePool();

	private static readonly ConcurrentDictionary<IComparer<T>, ObjectPool<PooledSortedSet<T>>> s_poolInstancesByComparer = new ConcurrentDictionary<IComparer<T>, ObjectPool<PooledSortedSet<T>>>();

	public PooledSortedSet(ObjectPool<PooledSortedSet<T>>? pool, IComparer<T>? comparer = null)
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

	private static ObjectPool<PooledSortedSet<T>> CreatePool(IComparer<T>? comparer = null)
	{
		IComparer<T> comparer2 = comparer;
		ObjectPool<PooledSortedSet<T>> pool = null;
		pool = new ObjectPool<PooledSortedSet<T>>(() => new PooledSortedSet<T>(pool, comparer2), 128);
		return pool;
	}

	/// <summary>
	/// Gets a pooled instance of a <see cref="T:Analyzer.Utilities.PooledObjects.PooledSortedSet`1" /> with an optional comparer.
	/// </summary>
	/// <param name="comparer">Singleton (or at least a bounded number) comparer to use, or null for the element type's default comparer.</param>
	/// <returns>An empty <see cref="T:Analyzer.Utilities.PooledObjects.PooledSortedSet`1" />.</returns>
	public static PooledSortedSet<T> GetInstance(IComparer<T>? comparer = null)
	{
		return ((comparer == null) ? s_poolInstance : s_poolInstancesByComparer.GetOrAdd(comparer, CreatePool)).Allocate();
	}

	/// <summary>
	/// Gets a pooled instance of a <see cref="T:Analyzer.Utilities.PooledObjects.PooledSortedSet`1" /> with the given initializer and an optional comparer.
	/// </summary>
	/// <param name="initializer">Initializer for the set.</param>
	/// <param name="comparer">Comparer to use, or null for the element type's default comparer.</param>
	/// <returns>An empty <see cref="T:Analyzer.Utilities.PooledObjects.PooledSortedSet`1" />.</returns>
	public static PooledSortedSet<T> GetInstance(IEnumerable<T> initializer, IComparer<T>? comparer = null)
	{
		PooledSortedSet<T> instance = GetInstance(comparer);
		foreach (T item in initializer)
		{
			instance.Add(item);
		}
		return instance;
	}
}
