using System.Collections.Concurrent;

namespace Analyzer.Utilities;

internal readonly struct OptionKey : IEquatable<OptionKey>
{
	private static readonly ConcurrentDictionary<(string ruleId, string optionName), OptionKey> s_keys = new ConcurrentDictionary<(string, string), OptionKey>();

	private static int s_lastOrdinal;

	private readonly int _ordinal;

	public string Name { get; }

	private OptionKey(string name)
	{
		Name = name ?? throw new ArgumentNullException("name");
		_ordinal = Interlocked.Increment(ref s_lastOrdinal);
	}

	public static OptionKey GetOrCreate(string? ruleId, string optionName)
	{
		return s_keys.GetOrAdd((ruleId ?? "", optionName), ((string ruleId, string optionName) pair) => new OptionKey((pair.ruleId != null) ? (pair.ruleId + "." + pair.optionName) : pair.optionName));
	}

	public static bool operator ==(OptionKey left, OptionKey right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(OptionKey left, OptionKey right)
	{
		return !(left == right);
	}

	public override bool Equals(object? obj)
	{
		if (obj is OptionKey other)
		{
			return Equals(other);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return _ordinal;
	}

	public bool Equals(OptionKey other)
	{
		return _ordinal == other._ordinal;
	}
}
