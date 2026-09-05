namespace Analyzer.Utilities;

/// <summary>
/// Provides bounded cache for analyzers.
/// Acts as a good alternative to <see cref="T:System.Runtime.CompilerServices.ConditionalWeakTable`2" />
/// when the cached value has a cyclic reference to the key preventing early garbage collection of entries.
/// </summary>
internal sealed class BoundedCache<TKey, TValue> : BoundedCacheWithFactory<TKey, TValue> where TKey : class where TValue : new()
{
	public TValue GetOrCreateValue(TKey key)
	{
		return GetOrCreateValue(key, CreateDefaultValue);
		static TValue CreateDefaultValue(TKey _)
		{
			return new TValue();
		}
	}
}
