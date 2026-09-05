namespace Analyzer.Utilities;

/// <summary>
/// Provides bounded cache for analyzers.
/// Acts as a good alternative to <see cref="T:System.Runtime.CompilerServices.ConditionalWeakTable`2" />
/// when the cached value has a cyclic reference to the key preventing early garbage collection of entries.
/// </summary>
internal class BoundedCacheWithFactory<TKey, TValue> where TKey : class
{
	private sealed class Entry
	{
		public TKey Key { get; }

		public TValue Value { get; }

		public Entry(TKey key, TValue value)
		{
			Key = key;
			Value = value;
		}
	}

	private readonly List<WeakReference<Entry?>> _weakReferencedEntries = new List<WeakReference<Entry>>
	{
		new WeakReference<Entry>(null),
		new WeakReference<Entry>(null),
		new WeakReference<Entry>(null),
		new WeakReference<Entry>(null),
		new WeakReference<Entry>(null)
	};

	public TValue GetOrCreateValue(TKey key, Func<TKey, TValue> valueFactory)
	{
		lock (_weakReferencedEntries)
		{
			int num = -1;
			for (int i = 0; i < _weakReferencedEntries.Count; i++)
			{
				WeakReference<Entry> weakReference = _weakReferencedEntries[i];
				if (!weakReference.TryGetTarget(out var target) || target == null)
				{
					if (num == -1)
					{
						num = i;
					}
				}
				else if (object.Equals(target.Key, key))
				{
					_weakReferencedEntries.RemoveAt(i);
					_weakReferencedEntries.Add(weakReference);
					return target.Value;
				}
			}
			if (num == -1)
			{
				num = 0;
			}
			Entry entry = new Entry(key, valueFactory(key));
			_weakReferencedEntries[num].SetTarget(entry);
			return entry.Value;
		}
	}
}
