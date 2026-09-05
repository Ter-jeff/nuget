using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities;

/// <summary>
/// Provides and caches well known types in a compilation.
/// </summary>
public class WellKnownTypeProvider
{
	private static readonly BoundedCacheWithFactory<Compilation, WellKnownTypeProvider> s_providerCache = new BoundedCacheWithFactory<Compilation, WellKnownTypeProvider>();

	/// <summary>
	/// All the referenced assembly symbols.
	/// </summary>
	/// <remarks>
	/// Seems to be less memory intensive than:
	/// foreach (Compilation.Assembly.Modules)
	///     foreach (Module.ReferencedAssemblySymbols)
	/// </remarks>
	private readonly Lazy<ImmutableArray<IAssemblySymbol>> _referencedAssemblies;

	/// <summary>
	/// Mapping of full name to <see cref="T:Microsoft.CodeAnalysis.INamedTypeSymbol" />.
	/// </summary>
	private readonly ConcurrentDictionary<string, INamedTypeSymbol?> _fullNameToTypeMap;

	/// <summary>
	/// Static cache of full type names (with namespaces) to namespace name parts,
	/// so we can query <see cref="P:Microsoft.CodeAnalysis.IAssemblySymbol.NamespaceNames" />.
	/// </summary>
	/// <remarks>
	/// Example: "System.Collections.Generic.List`1" =&gt; [ "System", "Collections", "Generic" ]
	///
	/// https://github.com/dotnet/roslyn/blob/9e786147b8cb884af454db081bb747a5bd36a086/src/Compilers/CSharp/Portable/Symbols/AssemblySymbol.cs#L455
	/// suggests the TypeNames collection can be checked to avoid expensive operations. But realizing TypeNames seems to be
	/// as memory intensive as unnecessary calls GetTypeByMetadataName() in some cases. So we'll go with namespace names.
	/// </remarks>
	private static readonly ConcurrentDictionary<string, ImmutableArray<string>> _fullTypeNameToNamespaceNames = new ConcurrentDictionary<string, ImmutableArray<string>>(StringComparer.Ordinal);

	public Compilation Compilation { get; }

	private WellKnownTypeProvider(Compilation compilation)
	{
		Compilation = compilation;
		_fullNameToTypeMap = new ConcurrentDictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
		_referencedAssemblies = new Lazy<ImmutableArray<IAssemblySymbol>>(() => Compilation.Assembly.Modules.SelectMany((IModuleSymbol m) => m.ReferencedAssemblySymbols).Distinct((IEqualityComparer<IAssemblySymbol>?)SymbolEqualityComparer.Default).ToImmutableArray(), LazyThreadSafetyMode.ExecutionAndPublication);
	}

	public static WellKnownTypeProvider GetOrCreate(Compilation compilation)
	{
		return s_providerCache.GetOrCreateValue(compilation, CreateWellKnownTypeProvider);
		static WellKnownTypeProvider CreateWellKnownTypeProvider(Compilation compilation)
		{
			return new WellKnownTypeProvider(compilation);
		}
	}

	/// <summary>
	/// Attempts to get the type by the full type name.
	/// </summary>
	/// <param name="fullTypeName">Namespace + type name, e.g. "System.Exception".</param>
	/// <param name="namedTypeSymbol">Named type symbol, if any.</param>
	/// <returns>True if found in the compilation, false otherwise.</returns>
	public bool TryGetOrCreateTypeByMetadataName(string fullTypeName, [NotNullWhen(true)] out INamedTypeSymbol? namedTypeSymbol)
	{
		if (_fullNameToTypeMap.TryGetValue(fullTypeName, out namedTypeSymbol))
		{
			return namedTypeSymbol != null;
		}
		return TryGetOrCreateTypeByMetadataNameSlow(fullTypeName, out namedTypeSymbol);
	}

	private bool TryGetOrCreateTypeByMetadataNameSlow(string fullTypeName, [NotNullWhen(true)] out INamedTypeSymbol? namedTypeSymbol)
	{
		string fullTypeName2 = fullTypeName;
		namedTypeSymbol = _fullNameToTypeMap.GetOrAdd(fullTypeName2, delegate(string fullyQualifiedMetadataName)
		{
			INamedTypeSymbol namedTypeSymbol2 = null;
			ImmutableArray<string> set = ((string.IsInterned(fullTypeName2) == null) ? GetNamespaceNamesFromFullTypeName(fullTypeName2) : _fullTypeNameToNamespaceNames.GetOrAdd(fullTypeName2, GetNamespaceNamesFromFullTypeName));
			if (IsSubsetOfCollection(set, Compilation.Assembly.NamespaceNames))
			{
				namedTypeSymbol2 = Compilation.Assembly.GetTypeByMetadataName(fullyQualifiedMetadataName);
			}
			if (namedTypeSymbol2 == null)
			{
				ImmutableArray<IAssemblySymbol>.Enumerator enumerator = _referencedAssemblies.Value.GetEnumerator();
				while (enumerator.MoveNext())
				{
					IAssemblySymbol current = enumerator.Current;
					if (IsSubsetOfCollection(set, current.NamespaceNames))
					{
						INamedTypeSymbol typeByMetadataName = current.GetTypeByMetadataName(fullyQualifiedMetadataName);
						if (typeByMetadataName != null)
						{
							SymbolVisibility resultantVisibility = typeByMetadataName.GetResultantVisibility();
							if (resultantVisibility == SymbolVisibility.Public || (resultantVisibility == SymbolVisibility.Internal && current.GivesAccessTo(Compilation.Assembly)))
							{
								if (namedTypeSymbol2 != null)
								{
									return (INamedTypeSymbol?)null;
								}
								namedTypeSymbol2 = typeByMetadataName;
							}
						}
					}
				}
			}
			return namedTypeSymbol2;
		});
		return namedTypeSymbol != null;
	}

	/// <summary>
	/// Gets a type by its full type name.
	/// </summary>
	/// <param name="fullTypeName">Namespace + type name, e.g. "System.Exception".</param>
	/// <returns>The <see cref="T:Microsoft.CodeAnalysis.INamedTypeSymbol" /> if found, null otherwise.</returns>
	public INamedTypeSymbol? GetOrCreateTypeByMetadataName(string fullTypeName)
	{
		TryGetOrCreateTypeByMetadataName(fullTypeName, out INamedTypeSymbol namedTypeSymbol);
		return namedTypeSymbol;
	}

	/// <summary>
	/// Determines if <paramref name="typeSymbol" /> is a <see cref="T:System.Threading.Tasks.Task`1" /> with its type
	/// argument satisfying <paramref name="typeArgumentPredicate" />.
	/// </summary>
	/// <param name="typeSymbol">Type potentially representing a <see cref="T:System.Threading.Tasks.Task`1" />.</param>
	/// <param name="typeArgumentPredicate">Predicate to check the <paramref name="typeSymbol" />'s type argument.</param>
	/// <returns>True if <paramref name="typeSymbol" /> is a <see cref="T:System.Threading.Tasks.Task`1" /> with its
	/// type argument satisfying <paramref name="typeArgumentPredicate" />, false otherwise.</returns>
	internal bool IsTaskOfType([NotNullWhen(true)] ITypeSymbol? typeSymbol, Func<ITypeSymbol, bool> typeArgumentPredicate)
	{
		if (typeSymbol != null && typeSymbol.OriginalDefinition != null && SymbolEqualityComparer.Default.Equals(typeSymbol.OriginalDefinition, GetOrCreateTypeByMetadataName("System.Threading.Tasks.Task`1")) && typeSymbol is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.TypeArguments.Length == 1)
		{
			return typeArgumentPredicate(namedTypeSymbol.TypeArguments[0]);
		}
		return false;
	}

	private static ImmutableArray<string> GetNamespaceNamesFromFullTypeName(string fullTypeName)
	{
		using ArrayBuilder<string> arrayBuilder = ArrayBuilder<string>.GetInstance();
		int num = 0;
		for (int i = 0; i < fullTypeName.Length; i++)
		{
			if (fullTypeName[i] == '.')
			{
				int num2 = num;
				arrayBuilder.Add(fullTypeName.Substring(num2, i - num2));
				num = i + 1;
			}
			else if (!IsIdentifierPartCharacter(fullTypeName[i]))
			{
				break;
			}
		}
		return arrayBuilder.ToImmutable();
	}

	/// <summary>
	/// Returns true if the Unicode character can be a part of an identifier.
	/// </summary>
	/// <param name="ch">The Unicode character.</param>
	private static bool IsIdentifierPartCharacter(char ch)
	{
		if (ch < 'a')
		{
			if (ch < 'A')
			{
				if (ch >= '0')
				{
					return ch <= '9';
				}
				return false;
			}
			if (ch <= 'Z' || ch == '_')
			{
				return true;
			}
			return false;
		}
		if (ch <= 'z')
		{
			return true;
		}
		if (ch <= '\u007f')
		{
			return false;
		}
		switch (CharUnicodeInfo.GetUnicodeCategory(ch))
		{
		case UnicodeCategory.UppercaseLetter:
		case UnicodeCategory.LowercaseLetter:
		case UnicodeCategory.TitlecaseLetter:
		case UnicodeCategory.ModifierLetter:
		case UnicodeCategory.OtherLetter:
		case UnicodeCategory.NonSpacingMark:
		case UnicodeCategory.SpacingCombiningMark:
		case UnicodeCategory.DecimalDigitNumber:
		case UnicodeCategory.LetterNumber:
		case UnicodeCategory.Format:
		case UnicodeCategory.ConnectorPunctuation:
			return true;
		default:
			return false;
		}
	}

	private static bool IsSubsetOfCollection<T>(ImmutableArray<T> set1, ICollection<T> set2)
	{
		if (set1.Length > set2.Count)
		{
			return false;
		}
		for (int i = 0; i < set1.Length; i++)
		{
			if (!set2.Contains(set1[i]))
			{
				return false;
			}
		}
		return true;
	}
}
