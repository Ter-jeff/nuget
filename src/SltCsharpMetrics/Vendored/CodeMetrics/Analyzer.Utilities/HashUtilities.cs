using System.Collections.Immutable;

namespace Analyzer.Utilities;

internal static class HashUtilities
{
	internal static int GetHashCodeOrDefault<T>(this T? obj) where T : class
	{
		return obj?.GetHashCode() ?? 0;
	}

	internal static int GetHashCodeOrDefault<T>(this T? obj) where T : struct
	{
		return obj?.GetHashCode() ?? 0;
	}

	internal static int Combine<T>(ImmutableArray<T> array)
	{
		RoslynHashCode hashCode = default(RoslynHashCode);
		Combine(array, ref hashCode);
		return hashCode.ToHashCode();
	}

	internal static void Combine<T>(ImmutableArray<T> array, ref RoslynHashCode hashCode)
	{
		ImmutableArray<T>.Enumerator enumerator = array.GetEnumerator();
		while (enumerator.MoveNext())
		{
			T current = enumerator.Current;
			hashCode.Add(current);
		}
	}

	internal static int Combine<T>(ImmutableStack<T> stack)
	{
		RoslynHashCode hashCode = default(RoslynHashCode);
		Combine(stack, ref hashCode);
		return hashCode.ToHashCode();
	}

	internal static void Combine<T>(ImmutableStack<T> stack, ref RoslynHashCode hashCode)
	{
		ImmutableStack<T>.Enumerator enumerator = stack.GetEnumerator();
		while (enumerator.MoveNext())
		{
			T current = enumerator.Current;
			hashCode.Add(current);
		}
	}

	internal static int Combine<T>(ImmutableHashSet<T> set)
	{
		RoslynHashCode hashCode = default(RoslynHashCode);
		Combine(set, ref hashCode);
		return hashCode.ToHashCode();
	}

	internal static void Combine<T>(ImmutableHashSet<T> set, ref RoslynHashCode hashCode)
	{
		foreach (T item in set)
		{
			hashCode.Add(item);
		}
	}

	internal static int Combine<TKey, TValue>(ImmutableDictionary<TKey, TValue> dictionary) where TKey : notnull
	{
		RoslynHashCode hashCode = default(RoslynHashCode);
		Combine(dictionary, ref hashCode);
		return hashCode.ToHashCode();
	}

	internal static void Combine<TKey, TValue>(ImmutableDictionary<TKey, TValue> dictionary, ref RoslynHashCode hashCode) where TKey : notnull
	{
		foreach (var (value, value2) in dictionary)
		{
			hashCode.Add(value);
			hashCode.Add(value2);
		}
	}
}
