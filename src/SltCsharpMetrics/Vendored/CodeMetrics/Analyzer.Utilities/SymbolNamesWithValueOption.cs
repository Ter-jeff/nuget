using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities;

public sealed class SymbolNamesWithValueOption<TValue> : IEquatable<SymbolNamesWithValueOption<TValue>?>
{
	internal readonly struct TestAccessor
	{
		private readonly SymbolNamesWithValueOption<TValue> _symbolNamesWithValueOption;

		internal ref readonly ImmutableDictionary<string, TValue> Names => ref _symbolNamesWithValueOption._names;

		internal ref readonly ImmutableDictionary<ISymbol, TValue> Symbols => ref _symbolNamesWithValueOption._symbols;

		internal ref readonly ImmutableDictionary<SymbolKind, ImmutableDictionary<string, TValue>> WildcardNamesBySymbolKind => ref _symbolNamesWithValueOption._wildcardNamesBySymbolKind;

		internal ref readonly ConcurrentDictionary<ISymbol, KeyValuePair<string?, TValue?>> WildcardMatchResult => ref _symbolNamesWithValueOption._wildcardMatchResult;

		internal ref readonly ConcurrentDictionary<ISymbol, string> SymbolToDeclarationId => ref _symbolNamesWithValueOption._symbolToDeclarationId;

		internal TestAccessor(SymbolNamesWithValueOption<TValue> symbolNamesWithValueOption)
		{
			_symbolNamesWithValueOption = symbolNamesWithValueOption;
		}
	}

	/// <summary>
	/// Represents the two parts of a symbol name option when the symbol name is tighted to some specific value.
	/// This allows to link a value to a symbol while following the symbol's documentation ID format.
	/// </summary>
	/// <example>
	/// On the rule CA1710, we allow user specific suffix to be registered for symbol names using the following format:
	/// MyClass-&gt;Suffix or T:MyNamespace.MyClass-&gt;Suffix or N:MyNamespace-&gt;Suffix.
	/// </example>
	public sealed class NameParts
	{
		public string SymbolName { get; }

		public TValue AssociatedValue { get; }

		public NameParts(string symbolName, TValue associatedValue)
		{
			SymbolName = symbolName.Trim();
			AssociatedValue = associatedValue;
		}
	}

	internal const SymbolKind AllKinds = SymbolKind.ErrorType;

	internal const char WildcardChar = '*';

	public static readonly SymbolNamesWithValueOption<TValue> Empty = new SymbolNamesWithValueOption<TValue>();

	private readonly ImmutableDictionary<string, TValue> _names;

	private readonly ImmutableDictionary<ISymbol, TValue> _symbols;

	/// <summary>
	/// Dictionary holding per symbol kind the wildcard entry with its suffix.
	/// The implementation only supports the following SymbolKind: Namespace, Type, Event, Field, Method, Property and ErrorType (as a way to hold the non-fully qualified types).
	/// </summary>
	/// <example>
	/// ErrorType -&gt;
	///     Symbol* -&gt; "some value"
	/// Namespace -&gt;
	///     Analyzer.Utilities -&gt; ""
	/// Type -&gt;
	///     Analyzer.Utilities.SymbolNamesWithValueOption -&gt; ""
	/// Event -&gt;
	///     Analyzer.Utilities.SymbolNamesWithValueOption.MyEvent -&gt; ""
	/// Field -&gt;
	///     Analyzer.Utilities.SymbolNamesWithValueOption.myField -&gt; ""
	/// Method -&gt;
	///     Analyzer.Utilities.SymbolNamesWithValueOption.MyMethod() -&gt; ""
	/// Property -&gt;
	///     Analyzer.Utilities.SymbolNamesWithValueOption.MyProperty -&gt; ""
	/// </example>
	private readonly ImmutableDictionary<SymbolKind, ImmutableDictionary<string, TValue>> _wildcardNamesBySymbolKind;

	/// <summary>
	/// Cache for the wildcard matching algorithm. The current implementation can be slow so we want to make sure that once a match is performed we save its result.
	/// </summary>
	private readonly ConcurrentDictionary<ISymbol, KeyValuePair<string?, TValue?>> _wildcardMatchResult = new ConcurrentDictionary<ISymbol, KeyValuePair<string, TValue>>();

	private readonly ConcurrentDictionary<ISymbol, string> _symbolToDeclarationId = new ConcurrentDictionary<ISymbol, string>();

	public bool IsEmpty => this == Empty;

	private SymbolNamesWithValueOption(ImmutableDictionary<string, TValue> names, ImmutableDictionary<ISymbol, TValue> symbols, ImmutableDictionary<SymbolKind, ImmutableDictionary<string, TValue>> wildcardNamesBySymbolKind)
	{
		_names = names;
		_symbols = symbols;
		_wildcardNamesBySymbolKind = wildcardNamesBySymbolKind;
	}

	private SymbolNamesWithValueOption()
	{
		_names = ImmutableDictionary<string, TValue>.Empty;
		_symbols = ImmutableDictionary<ISymbol, TValue>.Empty;
		_wildcardNamesBySymbolKind = ImmutableDictionary<SymbolKind, ImmutableDictionary<string, TValue>>.Empty;
	}

	public static SymbolNamesWithValueOption<TValue> Create(ImmutableArray<string> symbolNames, Compilation compilation, string? optionalPrefix, Func<string, NameParts> getSymbolNamePartsFunc)
	{
		if (symbolNames.IsEmpty)
		{
			return Empty;
		}
		PooledDictionary<string, TValue> instance = PooledDictionary<string, TValue>.GetInstance();
		PooledDictionary<ISymbol, TValue> instance2 = PooledDictionary<ISymbol, TValue>.GetInstance();
		PooledDictionary<SymbolKind, PooledDictionary<string, TValue>> instance3 = PooledDictionary<SymbolKind, PooledDictionary<string, TValue>>.GetInstance();
		ImmutableArray<string>.Enumerator enumerator = symbolNames.GetEnumerator();
		while (enumerator.MoveNext())
		{
			string current = enumerator.Current;
			NameParts nameParts = getSymbolNamePartsFunc(current);
			int num = nameParts.SymbolName.Count((char c) => c == '*');
			if (num > 1)
			{
				continue;
			}
			if (num == 1)
			{
				string symbolName = nameParts.SymbolName;
				if (symbolName[symbolName.Length - 1] != '*' || nameParts.SymbolName.Length == 1)
				{
					continue;
				}
			}
			if (num == 1)
			{
				ProcessWildcardName(nameParts, instance3);
			}
			else if (nameParts.SymbolName.Equals(".ctor", StringComparison.Ordinal) || nameParts.SymbolName.Equals(".cctor", StringComparison.Ordinal) || (!nameParts.SymbolName.Contains(".", StringComparison.Ordinal) && !nameParts.SymbolName.Contains(":", StringComparison.Ordinal)))
			{
				ProcessName(nameParts, instance);
			}
			else
			{
				ProcessSymbolName(nameParts, compilation, optionalPrefix, instance2);
			}
		}
		if (instance.Count == 0 && instance2.Count == 0 && instance3.Count == 0)
		{
			return Empty;
		}
		return new SymbolNamesWithValueOption<TValue>(instance.ToImmutableDictionaryAndFree(), instance2.ToImmutableDictionaryAndFree(), instance3.ToImmutableDictionaryAndFree((KeyValuePair<SymbolKind, PooledDictionary<string, TValue>> x) => x.Key, (KeyValuePair<SymbolKind, PooledDictionary<string, TValue>> x) => x.Value.ToImmutableDictionaryAndFree(), instance3.Comparer));
		static void ProcessName(NameParts parts, PooledDictionary<string, TValue> namesBuilder)
		{
			if (!namesBuilder.ContainsKey(parts.SymbolName))
			{
				namesBuilder.Add(parts.SymbolName, parts.AssociatedValue);
			}
		}
		static void ProcessSymbolName(NameParts parts, Compilation compilation, string? optionalPrefix, PooledDictionary<ISymbol, TValue> symbolsBuilder)
		{
			ImmutableArray<ISymbol>.Enumerator enumerator2 = DocumentationCommentId.GetSymbolsForDeclarationId(((string.IsNullOrEmpty(optionalPrefix) || parts.SymbolName.StartsWith(optionalPrefix, StringComparison.Ordinal)) ? parts.SymbolName : (optionalPrefix + parts.SymbolName)).Replace("..ctor", ".#ctor", StringComparison.Ordinal).Replace("..cctor", ".#cctor", StringComparison.Ordinal), compilation).GetEnumerator();
			while (enumerator2.MoveNext())
			{
				ISymbol current2 = enumerator2.Current;
				if (current2 != null)
				{
					if (current2 is INamespaceSymbol namespaceSymbol && namespaceSymbol.ConstituentNamespaces.Length > 1)
					{
						ImmutableArray<INamespaceSymbol>.Enumerator enumerator3 = namespaceSymbol.ConstituentNamespaces.GetEnumerator();
						while (enumerator3.MoveNext())
						{
							INamespaceSymbol current3 = enumerator3.Current;
							if (!symbolsBuilder.ContainsKey(current3))
							{
								symbolsBuilder.Add(current3, parts.AssociatedValue);
							}
						}
					}
					if (!symbolsBuilder.ContainsKey(current2))
					{
						symbolsBuilder.Add(current2, parts.AssociatedValue);
					}
				}
			}
		}
		static void ProcessWildcardName(NameParts parts, PooledDictionary<SymbolKind, PooledDictionary<string, TValue>> wildcardNamesBuilder)
		{
			if (parts.SymbolName[1] != ':')
			{
				if (!wildcardNamesBuilder.TryGetValue(SymbolKind.ErrorType, out PooledDictionary<string, TValue> value))
				{
					value = PooledDictionary<string, TValue>.GetInstance();
					wildcardNamesBuilder.Add(SymbolKind.ErrorType, value);
				}
				PooledDictionary<string, TValue> pooledDictionary = value;
				string symbolName2 = parts.SymbolName;
				pooledDictionary.Add(symbolName2.Substring(0, symbolName2.Length - 1), parts.AssociatedValue);
			}
			else
			{
				SymbolKind? symbolKind = parts.SymbolName[0] switch
				{
					'E' => SymbolKind.Event, 
					'F' => SymbolKind.Field, 
					'M' => SymbolKind.Method, 
					'N' => SymbolKind.Namespace, 
					'P' => SymbolKind.Property, 
					'T' => SymbolKind.NamedType, 
					_ => null, 
				};
				if (symbolKind.HasValue)
				{
					if (!wildcardNamesBuilder.TryGetValue(symbolKind.Value, out PooledDictionary<string, TValue> value2))
					{
						value2 = PooledDictionary<string, TValue>.GetInstance();
						wildcardNamesBuilder.Add(symbolKind.Value, value2);
					}
					PooledDictionary<string, TValue> pooledDictionary2 = value2;
					string symbolName2 = parts.SymbolName;
					pooledDictionary2.Add(symbolName2.Substring(2, symbolName2.Length - 1 - 2), parts.AssociatedValue);
				}
			}
		}
	}

	public bool Contains(ISymbol symbol)
	{
		string firstMatchName;
		TValue firstMatchValue;
		if (!_symbols.ContainsKey(symbol) && !_names.ContainsKey(symbol.Name))
		{
			return TryGetFirstWildcardMatch(symbol, out firstMatchName, out firstMatchValue);
		}
		return true;
	}

	/// <summary>
	/// Gets the value associated with the specified symbol in the option specification.
	/// </summary>
	public bool TryGetValue(ISymbol symbol, [MaybeNullWhen(false)] out TValue value)
	{
		if (_symbols.TryGetValue(symbol, out value) || _names.TryGetValue(symbol.Name, out value))
		{
			return true;
		}
		if (TryGetFirstWildcardMatch(symbol, out string _, out value))
		{
			return true;
		}
		value = default(TValue);
		return false;
	}

	public override bool Equals(object? obj)
	{
		return Equals(obj as SymbolNamesWithValueOption<TValue>);
	}

	public bool Equals(SymbolNamesWithValueOption<TValue>? other)
	{
		if (other != null && _names.IsEqualTo(other._names) && _symbols.IsEqualTo(other._symbols))
		{
			return _wildcardNamesBySymbolKind.IsEqualTo<SymbolKind, ImmutableDictionary<string, TValue>>(other._wildcardNamesBySymbolKind);
		}
		return false;
	}

	public override int GetHashCode()
	{
		RoslynHashCode hashCode = default(RoslynHashCode);
		HashUtilities.Combine(_names, ref hashCode);
		HashUtilities.Combine(_symbols, ref hashCode);
		HashUtilities.Combine(_wildcardNamesBySymbolKind, ref hashCode);
		return hashCode.ToHashCode();
	}

	private bool TryGetFirstWildcardMatch(ISymbol symbol, [NotNullWhen(true)] out string? firstMatchName, [MaybeNullWhen(false)] out TValue firstMatchValue)
	{
		ISymbol symbol2 = symbol;
		switch (symbol2.Kind)
		{
		case SymbolKind.Assembly:
		case SymbolKind.ErrorType:
		case SymbolKind.NetModule:
			firstMatchName = null;
			firstMatchValue = default(TValue);
			return false;
		default:
			throw new ArgumentException($"Unsupported symbol kind '{symbol2.Kind}' for symbol '{symbol2}'");
		case SymbolKind.Event:
		case SymbolKind.Field:
		case SymbolKind.Method:
		case SymbolKind.NamedType:
		case SymbolKind.Namespace:
		case SymbolKind.Property:
		{
			if (_wildcardNamesBySymbolKind.IsEmpty)
			{
				firstMatchName = null;
				firstMatchValue = default(TValue);
				return false;
			}
			string key;
			TValue value2;
			if (_wildcardMatchResult.TryGetValue(symbol2, out KeyValuePair<string, TValue> value))
			{
				KeyValuePair<string, TValue> keyValuePair = value;
				keyValuePair.Deconstruct(out key, out value2);
				firstMatchName = key;
				firstMatchValue = value2;
				return firstMatchName != null;
			}
			string symbolDeclarationId = _symbolToDeclarationId.GetOrAdd(symbol2, GetDeclarationId);
			if (_wildcardNamesBySymbolKind.TryGetValue(symbol2.Kind, out ImmutableDictionary<string, TValue> value3))
			{
				KeyValuePair<string, TValue> prefixedFirstMatchOrDefault = value3.FirstOrDefault((KeyValuePair<string, TValue> kvp) => symbolDeclarationId.StartsWith(kvp.Key, StringComparison.Ordinal));
				if (!string.IsNullOrWhiteSpace(prefixedFirstMatchOrDefault.Key))
				{
					KeyValuePair<string, TValue> keyValuePair = prefixedFirstMatchOrDefault;
					keyValuePair.Deconstruct(out key, out value2);
					firstMatchName = key;
					firstMatchValue = value2;
					_wildcardMatchResult.AddOrUpdate(symbol2, prefixedFirstMatchOrDefault.AsNullable(), (ISymbol s, KeyValuePair<string?, TValue?> match) => prefixedFirstMatchOrDefault.AsNullable());
					return true;
				}
			}
			if (_wildcardNamesBySymbolKind.TryGetValue(SymbolKind.ErrorType, out ImmutableDictionary<string, TValue> value4))
			{
				KeyValuePair<string, TValue> unprefixedFirstMatchOrDefault = value4.FirstOrDefault((KeyValuePair<string, TValue> kvp) => symbolDeclarationId.StartsWith(kvp.Key, StringComparison.Ordinal));
				if (!string.IsNullOrWhiteSpace(unprefixedFirstMatchOrDefault.Key))
				{
					KeyValuePair<string, TValue> keyValuePair = unprefixedFirstMatchOrDefault;
					keyValuePair.Deconstruct(out key, out value2);
					firstMatchName = key;
					firstMatchValue = value2;
					_wildcardMatchResult.AddOrUpdate(symbol2, unprefixedFirstMatchOrDefault.AsNullable(), (ISymbol s, KeyValuePair<string?, TValue?> match) => unprefixedFirstMatchOrDefault.AsNullable());
					return true;
				}
			}
			if (_wildcardNamesBySymbolKind.TryGetValue(SymbolKind.ErrorType, out ImmutableDictionary<string, TValue> value5))
			{
				KeyValuePair<string, TValue> partialFirstMatchOrDefault = value5.FirstOrDefault((KeyValuePair<string, TValue> kvp) => symbol2.Name.StartsWith(kvp.Key, StringComparison.Ordinal));
				if (!string.IsNullOrWhiteSpace(partialFirstMatchOrDefault.Key))
				{
					KeyValuePair<string, TValue> keyValuePair = partialFirstMatchOrDefault;
					keyValuePair.Deconstruct(out key, out value2);
					firstMatchName = key;
					firstMatchValue = value2;
					_wildcardMatchResult.AddOrUpdate(symbol2, partialFirstMatchOrDefault.AsNullable(), (ISymbol s, KeyValuePair<string?, TValue?> match) => partialFirstMatchOrDefault.AsNullable());
					return true;
				}
			}
			firstMatchName = null;
			firstMatchValue = default(TValue);
			_wildcardMatchResult.AddOrUpdate(symbol2, new KeyValuePair<string, TValue>(null, default(TValue)), (ISymbol s, KeyValuePair<string?, TValue?> match) => new KeyValuePair<string, TValue>(null, default(TValue)));
			return false;
		}
		}
		static string GetDeclarationId(ISymbol symbol)
		{
			string text = DocumentationCommentId.CreateDeclarationId(symbol);
			return text.Substring(2, text.Length - 2).Replace(".#ctor", "..ctor", StringComparison.Ordinal).Replace(".#cctor", "..cctor", StringComparison.Ordinal);
		}
	}

	internal TestAccessor GetTestAccessor()
	{
		return new TestAccessor(this);
	}
}
