using System.Diagnostics;

namespace Analyzer.Utilities.PooledObjects;

/// <summary>
/// Generic implementation of object pooling pattern with predefined pool size limit. The main
/// purpose is that limited number of frequently used objects can be kept in the pool for
/// further recycling.
///
/// Notes: 
/// 1) it is not the goal to keep all returned objects. Pool is not meant for storage. If there
///    is no space in the pool, extra returned objects will be dropped.
///
/// 2) it is implied that if object was obtained from a pool, the caller will return it back in
///    a relatively short time. Keeping checked out objects for long durations is ok, but 
///    reduces usefulness of pooling. Just new up your own.
///
/// Not returning objects to the pool in not detrimental to the pool's work, but is a bad practice. 
/// Rationale: 
///    If there is no intent for reusing the object, do not use pool - just use "new". 
/// </summary>
internal sealed class ObjectPool<T> where T : class
{
	[DebuggerDisplay("{Value,nq}")]
	private struct Element
	{
		internal T? Value;
	}

	/// <remarks>
	/// Not using System.Func{T} because this file is linked into the (debugger) Formatter,
	/// which does not have that type (since it compiles against .NET 2.0).
	/// </remarks>
	internal delegate T Factory();

	private T? _firstItem;

	private readonly Element[] _items;

	private readonly Factory _factory;

	internal ObjectPool(Factory factory)
		: this(factory, Environment.ProcessorCount * 2)
	{
	}

	internal ObjectPool(Factory factory, int size)
	{
		_factory = factory;
		_items = new Element[size - 1];
	}

	private T CreateInstance()
	{
		return _factory();
	}

	/// <summary>
	/// Produces an instance.
	/// </summary>
	/// <remarks>
	/// Search strategy is a simple linear probing which is chosen for it cache-friendliness.
	/// Note that Free will try to store recycled objects close to the start thus statistically 
	/// reducing how far we will typically search.
	/// </remarks>
	internal T Allocate()
	{
		T val = _firstItem;
		if (val == null || val != Interlocked.CompareExchange(ref _firstItem, null, val))
		{
			val = AllocateSlow();
		}
		return val;
	}

	private T AllocateSlow()
	{
		Element[] items = _items;
		for (int i = 0; i < items.Length; i++)
		{
			T value = items[i].Value;
			if (value != null && value == Interlocked.CompareExchange(ref items[i].Value, null, value))
			{
				return value;
			}
		}
		return CreateInstance();
	}

	/// <summary>
	/// Returns objects to the pool.
	/// </summary>
	/// <remarks>
	/// Search strategy is a simple linear probing which is chosen for it cache-friendliness.
	/// Note that Free will try to store recycled objects close to the start thus statistically 
	/// reducing how far we will typically search in Allocate.
	/// </remarks>
	internal void Free(T obj, CancellationToken cancellationToken)
	{
		if (!cancellationToken.IsCancellationRequested)
		{
			if (_firstItem == null)
			{
				_firstItem = obj;
			}
			else
			{
				FreeSlow(obj);
			}
		}
	}

	private void FreeSlow(T obj)
	{
		Element[] items = _items;
		for (int i = 0; i < items.Length; i++)
		{
			if (items[i].Value == null)
			{
				items[i].Value = obj;
				break;
			}
		}
	}

	/// <summary>
	/// Removes an object from leak tracking.  
	///
	/// This is called when an object is returned to the pool.  It may also be explicitly 
	/// called if an object allocated from the pool is intentionally not being returned
	/// to the pool.  This can be of use with pooled arrays if the consumer wants to 
	/// return a larger array to the pool than was originally allocated.
	/// </summary>
	[Conditional("DEBUG")]
	internal static void ForgetTrackedObject(T old, T? replacement = null)
	{
	}

	[Conditional("DEBUG")]
	private void Validate(object obj)
	{
		Element[] items = _items;
		for (int i = 0; i < items.Length && items[i].Value != null; i++)
		{
		}
	}
}
