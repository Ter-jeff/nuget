using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Analyzer.Utilities.Extensions;

internal static class IMethodSymbolExtensions
{
	/// <summary>
	/// Set of well-known collection add method names.
	/// Used in <see cref="M:Analyzer.Utilities.Extensions.IMethodSymbolExtensions.IsCollectionAddMethod(Microsoft.CodeAnalysis.IMethodSymbol,System.Collections.Immutable.ImmutableHashSet{Microsoft.CodeAnalysis.INamedTypeSymbol})" /> heuristic.
	/// </summary>
	private static readonly ImmutableHashSet<string> s_collectionAddMethodNameVariants = ImmutableHashSet.Create((IEqualityComparer<string>?)StringComparer.Ordinal, new string[5] { "Add", "AddOrUpdate", "GetOrAdd", "TryAdd", "TryUpdate" });

	/// <summary>
	/// PERF: Cache from method symbols to their topmost block operations to enable interprocedural flow analysis
	/// across analyzers and analyzer callbacks to re-use the operations, semanticModel and control flow graph.
	/// </summary>
	/// <remarks>Also see <see cref="F:Analyzer.Utilities.Extensions.IOperationExtensions.s_operationToCfgCache" /></remarks>
	private static readonly BoundedCache<Compilation, ConcurrentDictionary<IMethodSymbol, IBlockOperation?>> s_methodToTopmostOperationBlockCache = new BoundedCache<Compilation, ConcurrentDictionary<IMethodSymbol, IBlockOperation>>();

	/// <summary>
	/// Checks if the given method overrides <see cref="M:System.Object.Equals(System.Object)" />.
	/// </summary>
	public static bool IsObjectEqualsOverride(this IMethodSymbol method)
	{
		if (method != null && method.IsOverride && method.Name == "Equals" && method.ReturnType.SpecialType == SpecialType.System_Boolean && method.Parameters.Length == 1 && method.Parameters[0].Type.SpecialType == SpecialType.System_Object)
		{
			return IsObjectMethodOverride(method);
		}
		return false;
	}

	/// <summary>
	/// Checks if the given method is <see cref="M:System.Object.Equals(System.Object)" />.
	/// </summary>
	public static bool IsObjectEquals(this IMethodSymbol method)
	{
		if (method != null && method.ContainingType.SpecialType == SpecialType.System_Object && method.IsVirtual && method.Name == "Equals" && method.ReturnType.SpecialType == SpecialType.System_Boolean && method.Parameters.Length == 1)
		{
			return method.Parameters[0].Type.SpecialType == SpecialType.System_Object;
		}
		return false;
	}

	/// <summary>
	/// Checks if the given <paramref name="method" /> is <see cref="M:System.Object.Equals(System.Object,System.Object)" /> or <see cref="M:System.Object.ReferenceEquals(System.Object,System.Object)" />.
	/// </summary>
	public static bool IsStaticObjectEqualsOrReferenceEquals(this IMethodSymbol method)
	{
		if (method != null && method.IsStatic && method.ContainingType.SpecialType == SpecialType.System_Object && method.Parameters.Length == 2 && method.ReturnType.SpecialType == SpecialType.System_Boolean && method.Parameters[0].Type.SpecialType == SpecialType.System_Object && method.Parameters[1].Type.SpecialType == SpecialType.System_Object)
		{
			if (!(method.Name == "Equals"))
			{
				return method.Name == "ReferenceEquals";
			}
			return true;
		}
		return false;
	}

	/// <summary>
	/// Checks if the given method overrides Object.GetHashCode.
	/// </summary>
	public static bool IsGetHashCodeOverride(this IMethodSymbol method)
	{
		if (method != null && method.IsOverride && method.Name == "GetHashCode" && method.ReturnType.SpecialType == SpecialType.System_Int32 && method.Parameters.IsEmpty)
		{
			return IsObjectMethodOverride(method);
		}
		return false;
	}

	/// <summary>
	/// Checks if the given method overrides Object.ToString.
	/// </summary>
	public static bool IsToStringOverride(this IMethodSymbol method)
	{
		if (method != null && method.IsOverride && method.ReturnType.SpecialType == SpecialType.System_String && method.Name == "ToString" && method.Parameters.IsEmpty)
		{
			return IsObjectMethodOverride(method);
		}
		return false;
	}

	/// <summary>
	/// Checks if the given method overrides a method from System.Object
	/// </summary>
	private static bool IsObjectMethodOverride(IMethodSymbol method)
	{
		for (IMethodSymbol overriddenMethod = method.OverriddenMethod; overriddenMethod != null; overriddenMethod = overriddenMethod.OverriddenMethod)
		{
			if (overriddenMethod.ContainingType.SpecialType == SpecialType.System_Object)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Checks if the given method is a Finalizer implementation.
	/// </summary>
	public static bool IsFinalizer(this IMethodSymbol method)
	{
		if (method.MethodKind == MethodKind.Destructor)
		{
			return true;
		}
		if (method.Name != "Finalize" || !method.Parameters.IsEmpty || !method.ReturnsVoid)
		{
			return false;
		}
		IMethodSymbol methodSymbol = method.OverriddenMethod;
		if (method.ContainingType.SpecialType == SpecialType.System_Object)
		{
			return true;
		}
		if (methodSymbol == null)
		{
			return false;
		}
		for (IMethodSymbol overriddenMethod = methodSymbol.OverriddenMethod; overriddenMethod != null; overriddenMethod = overriddenMethod.OverriddenMethod)
		{
			methodSymbol = overriddenMethod;
		}
		return methodSymbol.ContainingType.SpecialType == SpecialType.System_Object;
	}

	/// <summary>
	/// Checks if the given method is an implementation of the given interface method
	/// Substituted with the given typeargument.
	/// </summary>
	public static bool IsImplementationOfInterfaceMethod(this IMethodSymbol method, ITypeSymbol? typeArgument, [NotNullWhen(true)] INamedTypeSymbol? interfaceType, string interfaceMethodName)
	{
		if (((typeArgument == null) ? interfaceType : interfaceType?.Construct(typeArgument))?.GetMembers(interfaceMethodName).FirstOrDefault() is IMethodSymbol interfaceMember)
		{
			return SymbolEqualityComparer.Default.Equals(method, method.ContainingType.FindImplementationForInterfaceMember(interfaceMember));
		}
		return false;
	}

	/// <summary>
	/// Checks if the given method implements IDisposable.Dispose()
	/// </summary>
	public static bool IsDisposeImplementation(this IMethodSymbol method, Compilation compilation)
	{
		INamedTypeSymbol orCreateTypeByMetadataName = compilation.GetOrCreateTypeByMetadataName("System.IDisposable");
		return method.IsDisposeImplementation(orCreateTypeByMetadataName);
	}

	/// <summary>
	/// Checks if the given method implements IAsyncDisposable.Dispose()
	/// </summary>
	public static bool IsAsyncDisposeImplementation(this IMethodSymbol method, Compilation compilation)
	{
		INamedTypeSymbol orCreateTypeByMetadataName = compilation.GetOrCreateTypeByMetadataName("System.IAsyncDisposable");
		INamedTypeSymbol orCreateTypeByMetadataName2 = compilation.GetOrCreateTypeByMetadataName("System.Threading.Tasks.ValueTask");
		return method.IsAsyncDisposeImplementation(orCreateTypeByMetadataName, orCreateTypeByMetadataName2);
	}

	/// <summary>
	/// Checks if the given method implements <see cref="M:System.IDisposable.Dispose" /> or overrides an implementation of <see cref="M:System.IDisposable.Dispose" />.
	/// </summary>
	public static bool IsDisposeImplementation([NotNullWhen(true)] this IMethodSymbol? method, [NotNullWhen(true)] INamedTypeSymbol? iDisposable)
	{
		if (method == null)
		{
			return false;
		}
		if (method.IsOverride)
		{
			return method.OverriddenMethod.IsDisposeImplementation(iDisposable);
		}
		if (method.ReturnsVoid && method.Parameters.IsEmpty)
		{
			return method.IsImplementationOfInterfaceMethod(null, iDisposable, "Dispose");
		}
		return false;
	}

	/// <summary>
	/// Checks if the given method implements "IAsyncDisposable.Dispose" or overrides an implementation of "IAsyncDisposable.Dispose".
	/// </summary>
	public static bool IsAsyncDisposeImplementation([NotNullWhen(true)] this IMethodSymbol? method, [NotNullWhen(true)] INamedTypeSymbol? iAsyncDisposable, [NotNullWhen(true)] INamedTypeSymbol? valueTaskType)
	{
		if (method == null)
		{
			return false;
		}
		if (method.IsOverride)
		{
			return method.OverriddenMethod.IsAsyncDisposeImplementation(iAsyncDisposable, valueTaskType);
		}
		if (SymbolEqualityComparer.Default.Equals(method.ReturnType, valueTaskType) && method.Parameters.IsEmpty)
		{
			return method.IsImplementationOfInterfaceMethod(null, iAsyncDisposable, "DisposeAsync");
		}
		return false;
	}

	/// <summary>
	/// Checks if the given method has the signature "void Dispose()".
	/// </summary>
	private static bool HasDisposeMethodSignature(this IMethodSymbol method)
	{
		if (method.Name == "Dispose" && method.MethodKind == MethodKind.Ordinary && method.ReturnsVoid)
		{
			return method.Parameters.IsEmpty;
		}
		return false;
	}

	/// <summary>
	/// Checks if the given method matches Dispose method convention and can be recognized by "using".
	/// </summary>
	public static bool HasDisposeSignatureByConvention(this IMethodSymbol method)
	{
		if (method.HasDisposeMethodSignature() && !method.IsStatic)
		{
			return !method.IsPrivate();
		}
		return false;
	}

	/// <summary>
	/// Checks if the given method has the signature "void Dispose(bool)".
	/// </summary>
	public static bool HasDisposeBoolMethodSignature(this IMethodSymbol method)
	{
		if (method.Name == "Dispose" && method.MethodKind == MethodKind.Ordinary && method.ReturnsVoid && method.Parameters.Length == 1)
		{
			IParameterSymbol parameterSymbol = method.Parameters[0];
			if (parameterSymbol.Type != null && parameterSymbol.Type.SpecialType == SpecialType.System_Boolean)
			{
				return parameterSymbol.RefKind == RefKind.None;
			}
			return false;
		}
		return false;
	}

	/// <summary>
	/// Checks if the given method has the signature "void Close()".
	/// </summary>
	private static bool HasDisposeCloseMethodSignature(this IMethodSymbol method)
	{
		if (method.Name == "Close" && method.MethodKind == MethodKind.Ordinary && method.ReturnsVoid)
		{
			return method.Parameters.IsEmpty;
		}
		return false;
	}

	/// <summary>
	/// Checks if the given method has the signature "Task CloseAsync()".
	/// </summary>
	private static bool HasDisposeCloseAsyncMethodSignature(this IMethodSymbol method, INamedTypeSymbol? taskType)
	{
		if (taskType != null && method.Parameters.IsEmpty && method.Name == "CloseAsync")
		{
			return SymbolEqualityComparer.Default.Equals(method.ReturnType, taskType);
		}
		return false;
	}

	/// <summary>
	/// Checks if the given method has the signature "Task DisposeAsync()" or "ValueTask DisposeAsync()" or "ConfiguredValueTaskAwaitable DisposeAsync()".
	/// </summary>
	private static bool HasDisposeAsyncMethodSignature(this IMethodSymbol method, INamedTypeSymbol? task, INamedTypeSymbol? valueTask, INamedTypeSymbol? configuredValueTaskAwaitable)
	{
		if (method.Name == "DisposeAsync" && method.MethodKind == MethodKind.Ordinary && method.Parameters.IsEmpty)
		{
			if (!SymbolEqualityComparer.Default.Equals(method.ReturnType, task) && !SymbolEqualityComparer.Default.Equals(method.ReturnType, valueTask))
			{
				return SymbolEqualityComparer.Default.Equals(method.ReturnType, configuredValueTaskAwaitable);
			}
			return true;
		}
		return false;
	}

	/// <summary>
	/// Checks if the given method has the signature "override Task DisposeCoreAsync(bool)" or "override Task DisposeAsyncCore(bool)".
	/// </summary>
	private static bool HasOverriddenDisposeCoreAsyncMethodSignature(this IMethodSymbol method, [NotNullWhen(true)] INamedTypeSymbol? task)
	{
		if ((method.Name == "DisposeAsyncCore" || method.Name == "DisposeCoreAsync") && method.MethodKind == MethodKind.Ordinary && method.IsOverride && SymbolEqualityComparer.Default.Equals(method.ReturnType, task) && method.Parameters.Length == 1)
		{
			return method.Parameters[0].Type.SpecialType == SpecialType.System_Boolean;
		}
		return false;
	}

	/// <summary>
	/// Checks if the given method has the signature "virtual ValueTask DisposeCoreAsync()" or "virtual ValueTask DisposeAsyncCore()".
	/// </summary>
	private static bool HasVirtualDisposeCoreAsyncMethodSignature(this IMethodSymbol method, [NotNullWhen(true)] INamedTypeSymbol? valueTask)
	{
		if ((method.Name == "DisposeAsyncCore" || method.Name == "DisposeCoreAsync") && method.MethodKind == MethodKind.Ordinary && method.IsVirtual && SymbolEqualityComparer.Default.Equals(method.ReturnType, valueTask))
		{
			return method.Parameters.Length == 0;
		}
		return false;
	}

	/// <summary>
	/// Gets the <see cref="T:Analyzer.Utilities.DisposeMethodKind" /> for the given method.
	/// </summary>
	public static DisposeMethodKind GetDisposeMethodKind(this IMethodSymbol method, Compilation compilation)
	{
		INamedTypeSymbol orCreateTypeByMetadataName = compilation.GetOrCreateTypeByMetadataName("System.IDisposable");
		INamedTypeSymbol orCreateTypeByMetadataName2 = compilation.GetOrCreateTypeByMetadataName("System.IAsyncDisposable");
		INamedTypeSymbol orCreateTypeByMetadataName3 = compilation.GetOrCreateTypeByMetadataName("System.Runtime.CompilerServices.ConfiguredAsyncDisposable");
		INamedTypeSymbol orCreateTypeByMetadataName4 = compilation.GetOrCreateTypeByMetadataName("System.Threading.Tasks.Task");
		INamedTypeSymbol orCreateTypeByMetadataName5 = compilation.GetOrCreateTypeByMetadataName("System.Threading.Tasks.ValueTask");
		INamedTypeSymbol orCreateTypeByMetadataName6 = compilation.GetOrCreateTypeByMetadataName("System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable");
		return method.GetDisposeMethodKind(orCreateTypeByMetadataName, orCreateTypeByMetadataName2, orCreateTypeByMetadataName3, orCreateTypeByMetadataName4, orCreateTypeByMetadataName5, orCreateTypeByMetadataName6);
	}

	/// <summary>
	/// Gets the <see cref="T:Analyzer.Utilities.DisposeMethodKind" /> for the given method.
	/// </summary>
	public static DisposeMethodKind GetDisposeMethodKind(this IMethodSymbol method, INamedTypeSymbol? iDisposable, INamedTypeSymbol? iAsyncDisposable, INamedTypeSymbol? configuredAsyncDisposable, INamedTypeSymbol? task, INamedTypeSymbol? valueTask, INamedTypeSymbol? configuredValueTaskAwaitable)
	{
		if (method.ContainingType.IsDisposable(iDisposable, iAsyncDisposable, configuredAsyncDisposable))
		{
			if (method.IsDisposeImplementation(iDisposable) || (SymbolEqualityComparer.Default.Equals(method.ContainingType, iDisposable) && method.HasDisposeMethodSignature()) || (method.ContainingType.IsRefLikeType && method.HasDisposeSignatureByConvention()))
			{
				return DisposeMethodKind.Dispose;
			}
			if (method.HasDisposeBoolMethodSignature())
			{
				return DisposeMethodKind.DisposeBool;
			}
			if (method.IsAsyncDisposeImplementation(iAsyncDisposable, valueTask) || method.HasDisposeAsyncMethodSignature(task, valueTask, configuredValueTaskAwaitable))
			{
				return DisposeMethodKind.DisposeAsync;
			}
			if (method.HasOverriddenDisposeCoreAsyncMethodSignature(task))
			{
				return DisposeMethodKind.DisposeCoreAsync;
			}
			if (method.HasVirtualDisposeCoreAsyncMethodSignature(valueTask))
			{
				return DisposeMethodKind.DisposeCoreAsync;
			}
			if (method.HasDisposeCloseMethodSignature())
			{
				return DisposeMethodKind.Close;
			}
			if (method.HasDisposeCloseAsyncMethodSignature(task))
			{
				return DisposeMethodKind.CloseAsync;
			}
		}
		return DisposeMethodKind.None;
	}

	/// <summary>
	/// Checks if the given method implements 'System.Runtime.Serialization.IDeserializationCallback.OnDeserialization' or overrides an implementation of 'System.Runtime.Serialization.IDeserializationCallback.OnDeserialization'/&gt;.
	/// </summary>
	public static bool IsOnDeserializationImplementation([NotNullWhen(true)] this IMethodSymbol? method, [NotNullWhen(true)] INamedTypeSymbol? iDeserializationCallback)
	{
		if (method == null)
		{
			return false;
		}
		if (method.IsOverride)
		{
			return method.OverriddenMethod.IsOnDeserializationImplementation(iDeserializationCallback);
		}
		if (method.ReturnsVoid && method.Parameters.Length == 1 && method.Parameters[0].Type.SpecialType == SpecialType.System_Object)
		{
			return method.IsImplementationOfInterfaceMethod(null, iDeserializationCallback, "OnDeserialization");
		}
		return false;
	}

	public static bool IsSerializationConstructor([NotNullWhen(true)] this IMethodSymbol? method, INamedTypeSymbol? serializationInfoType, INamedTypeSymbol? streamingContextType)
	{
		if (method.IsConstructor() && method.Parameters.Length == 2 && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, serializationInfoType))
		{
			return SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, streamingContextType);
		}
		return false;
	}

	public static bool IsGetObjectData([NotNullWhen(true)] this IMethodSymbol? method, INamedTypeSymbol? serializationInfoType, INamedTypeSymbol? streamingContextType)
	{
		if (method?.Name == "GetObjectData" && method.ReturnsVoid && method.Parameters.Length == 2 && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, serializationInfoType))
		{
			return SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, streamingContextType);
		}
		return false;
	}

	/// <summary>
	/// Checks if the method is a property getter.
	/// </summary>
	public static bool IsPropertyGetter(this IMethodSymbol method)
	{
		if (method.MethodKind == MethodKind.PropertyGet)
		{
			ISymbol? associatedSymbol = method.AssociatedSymbol;
			if (associatedSymbol == null)
			{
				return false;
			}
			return associatedSymbol.GetParameters().Length == 0;
		}
		return false;
	}

	/// <summary>
	/// Checks if the method is the getter for an indexer.
	/// </summary>
	public static bool IsIndexerGetter(this IMethodSymbol method)
	{
		if (method.MethodKind == MethodKind.PropertyGet)
		{
			return method.AssociatedSymbol.IsIndexer();
		}
		return false;
	}

	/// <summary>
	/// Checks if the method is an accessor for a property.
	/// </summary>
	public static bool IsPropertyAccessor(this IMethodSymbol method)
	{
		MethodKind methodKind = method.MethodKind;
		if ((uint)(methodKind - 11) <= 1u)
		{
			return true;
		}
		return false;
	}

	/// <summary>
	/// Checks if the method is an accessor for an event.
	/// </summary>
	public static bool IsEventAccessor(this IMethodSymbol method)
	{
		MethodKind methodKind = method.MethodKind;
		if ((uint)(methodKind - 5) <= 2u)
		{
			return true;
		}
		return false;
	}

	public static bool IsOperator(this IMethodSymbol methodSymbol)
	{
		MethodKind methodKind = methodSymbol.MethodKind;
		if (methodKind == MethodKind.UserDefinedOperator || methodKind == MethodKind.BuiltinOperator)
		{
			return true;
		}
		return false;
	}

	public static bool HasOptionalParameters(this IMethodSymbol methodSymbol)
	{
		return methodSymbol.Parameters.Any((IParameterSymbol p) => p.IsOptional);
	}

	public static IEnumerable<IMethodSymbol> GetOverloads(this IMethodSymbol? method)
	{
		IEnumerable<IMethodSymbol> enumerable = method?.ContainingType?.GetMembers(method.Name).OfType<IMethodSymbol>();
		if (enumerable == null)
		{
			yield break;
		}
		foreach (IMethodSymbol item in enumerable)
		{
			if (!SymbolEqualityComparer.Default.Equals(item, method))
			{
				yield return item;
			}
		}
	}

	/// <summary>
	/// Determine if the specific method is an Add method that adds to a collection.
	/// </summary>
	/// <param name="method">The method to test.</param>
	/// <param name="iCollectionTypes">Collection types.</param>
	/// <returns>'true' if <paramref name="method" /> is believed to be the add method of a collection.</returns>
	/// <remarks>
	/// We use the following heuristic to determine if a method is a collection add method:
	/// 1. Method's enclosing type implements any of the given <paramref name="iCollectionTypes" />.
	/// 2. Any of the following name heuristics are met:
	///     a. Method's name is from one of the well-known add method names from <see cref="F:Analyzer.Utilities.Extensions.IMethodSymbolExtensions.s_collectionAddMethodNameVariants" /> ("Add", "AddOrUpdate", "GetOrAdd", "TryAdd", or "TryUpdate")
	///     b. Method's name begins with "Add" (FxCop compat)
	/// </remarks>
	public static bool IsCollectionAddMethod(this IMethodSymbol method, ImmutableHashSet<INamedTypeSymbol> iCollectionTypes)
	{
		ImmutableHashSet<INamedTypeSymbol> iCollectionTypes2 = iCollectionTypes;
		if (iCollectionTypes2.IsEmpty)
		{
			return false;
		}
		if (!s_collectionAddMethodNameVariants.Contains(method.Name) && !method.Name.StartsWith("Add", StringComparison.Ordinal))
		{
			return false;
		}
		return method.ContainingType.AllInterfaces.Any((INamedTypeSymbol i) => iCollectionTypes2.Contains(i.OriginalDefinition));
	}

	/// <summary>
	/// Determine if the specific method is a Task.FromResult method that wraps a result in a task.
	/// </summary>
	/// <param name="method">The method to test.</param>
	/// <param name="taskType">Task type.</param>
	public static bool IsTaskFromResultMethod(this IMethodSymbol method, [NotNullWhen(true)] INamedTypeSymbol? taskType)
	{
		if (method.Name.Equals("FromResult", StringComparison.Ordinal))
		{
			return SymbolEqualityComparer.Default.Equals(method.ContainingType, taskType);
		}
		return false;
	}

	/// <summary>
	/// Determine if the specific method is a Task.ConfigureAwait(bool) method.
	/// </summary>
	/// <param name="method">The method to test.</param>
	/// <param name="genericTaskType">Generic task type.</param>
	public static bool IsTaskConfigureAwaitMethod(this IMethodSymbol method, [NotNullWhen(true)] INamedTypeSymbol? genericTaskType)
	{
		if (method.Name.Equals("ConfigureAwait", StringComparison.Ordinal) && method.Parameters.Length == 1 && method.Parameters[0].Type.SpecialType == SpecialType.System_Boolean)
		{
			return SymbolEqualityComparer.Default.Equals(method.ContainingType.OriginalDefinition, genericTaskType);
		}
		return false;
	}

	/// <summary>
	/// Determine if the specific method is a TaskAsyncEnumerableExtensions.ConfigureAwait(this IAsyncDisposable, bool) extension method.
	/// </summary>
	/// <param name="method">The method to test.</param>
	/// <param name="asyncDisposableType">IAsyncDisposable named type.</param>
	/// <param name="taskAsyncEnumerableExtensions">System.Threading.Tasks.TaskAsyncEnumerableExtensions named type.</param>
	public static bool IsAsyncDisposableConfigureAwaitMethod(this IMethodSymbol method, [NotNullWhen(true)] INamedTypeSymbol? asyncDisposableType, [NotNullWhen(true)] INamedTypeSymbol? taskAsyncEnumerableExtensions)
	{
		if (method.IsExtensionMethod && method.Name.Equals("ConfigureAwait", StringComparison.Ordinal) && method.Parameters.Length == 2 && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, asyncDisposableType) && method.Parameters[1].Type.SpecialType == SpecialType.System_Boolean && SymbolEqualityComparer.Default.Equals(method.ContainingType.OriginalDefinition, taskAsyncEnumerableExtensions))
		{
			return taskAsyncEnumerableExtensions.IsStatic;
		}
		return false;
	}

	/// <summary>
	/// Returns the topmost <see cref="T:Microsoft.CodeAnalysis.Operations.IBlockOperation" /> for given <paramref name="method" />.
	/// </summary>
	public static IBlockOperation? GetTopmostOperationBlock(this IMethodSymbol method, Compilation compilation, CancellationToken cancellationToken = default(CancellationToken))
	{
		IMethodSymbol method2 = method;
		Compilation compilation2 = compilation;
		return s_methodToTopmostOperationBlockCache.GetOrCreateValue(compilation2).GetOrAdd(method2, ComputeTopmostOperationBlock);
		IBlockOperation? ComputeTopmostOperationBlock(IMethodSymbol unused)
		{
			if (!SymbolEqualityComparer.Default.Equals(method2.ContainingAssembly, compilation2.Assembly))
			{
				return null;
			}
			ImmutableArray<SyntaxReference>.Enumerator enumerator = method2.DeclaringSyntaxReferences.GetEnumerator();
			while (enumerator.MoveNext())
			{
				SyntaxNode syntaxNode = enumerator.Current.GetSyntax(cancellationToken);
				if (compilation2.Language == "Visual Basic")
				{
					syntaxNode = syntaxNode.Parent;
				}
				SemanticModel semanticModel = compilation2.GetSemanticModel(syntaxNode.SyntaxTree);
				foreach (SyntaxNode item in syntaxNode.DescendantNodesAndSelf())
				{
					if (semanticModel.GetOperation(item, cancellationToken) is IBlockOperation result)
					{
						return result;
					}
				}
			}
			return null;
		}
	}

	public static bool IsLambdaOrLocalFunctionOrDelegate(this IMethodSymbol method)
	{
		MethodKind methodKind = method.MethodKind;
		if (methodKind == MethodKind.AnonymousFunction || methodKind == MethodKind.DelegateInvoke || methodKind == MethodKind.LocalFunction)
		{
			return true;
		}
		return false;
	}

	public static bool IsLambdaOrLocalFunction(this IMethodSymbol method)
	{
		MethodKind methodKind = method.MethodKind;
		if (methodKind == MethodKind.AnonymousFunction || methodKind == MethodKind.LocalFunction)
		{
			return true;
		}
		return false;
	}

	public static int GetParameterIndex(this IMethodSymbol methodSymbol, IParameterSymbol parameterSymbol)
	{
		for (int i = 0; i < methodSymbol.Parameters.Length; i++)
		{
			if (SymbolEqualityComparer.Default.Equals(parameterSymbol, methodSymbol.Parameters[i]))
			{
				return i;
			}
		}
		throw new ArgumentException("Invalid parameter", "parameterSymbol");
	}

	/// <summary>
	/// Returns true for void returning methods with two parameters, where
	/// the first parameter is of <see cref="T:System.Object" /> type and the second
	/// parameter inherits from or equals <see cref="T:System.EventArgs" /> type or
	/// whose name ends with 'EventArgs'.
	/// </summary>
	public static bool HasEventHandlerSignature(this IMethodSymbol method, [NotNullWhen(true)] INamedTypeSymbol? eventArgsType)
	{
		if (eventArgsType != null && method.ReturnsVoid && method.Parameters.Length == 2 && method.Parameters[0].Type.SpecialType == SpecialType.System_Object)
		{
			if (!method.Parameters[1].Type.DerivesFrom(eventArgsType, baseTypesOnly: true))
			{
				return method.Parameters[1].Type.Name.EndsWith("EventArgs", StringComparison.Ordinal);
			}
			return true;
		}
		return false;
	}

	public static bool IsLockMethod(this IMethodSymbol method, [NotNullWhen(true)] INamedTypeSymbol? systemThreadingMonitor)
	{
		if (method.Name == "Enter" && SymbolEqualityComparer.Default.Equals(method.ContainingType, systemThreadingMonitor) && method.ReturnsVoid && !method.Parameters.IsEmpty)
		{
			return method.Parameters[0].Type.SpecialType == SpecialType.System_Object;
		}
		return false;
	}

	public static bool IsInterlockedExchangeMethod(this IMethodSymbol method, INamedTypeSymbol? systemThreadingInterlocked)
	{
		if (method.Name == "Exchange" && method.Parameters.Length == 2 && method.Parameters[0].RefKind == RefKind.Ref && method.Parameters[1].RefKind != RefKind.Ref)
		{
			return SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, method.Parameters[1].Type);
		}
		return false;
	}

	public static bool IsInterlockedCompareExchangeMethod(this IMethodSymbol method, INamedTypeSymbol? systemThreadingInterlocked)
	{
		if (method.Name == "CompareExchange" && method.Parameters.Length == 3 && method.Parameters[0].RefKind == RefKind.Ref && method.Parameters[1].RefKind != RefKind.Ref && method.Parameters[2].RefKind != RefKind.Ref && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, method.Parameters[1].Type))
		{
			return SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, method.Parameters[2].Type);
		}
		return false;
	}

	public static bool HasParameterWithDelegateType(this IMethodSymbol methodSymbol)
	{
		return methodSymbol.Parameters.Any((IParameterSymbol p) => p.Type.TypeKind == TypeKind.Delegate);
	}

	/// <summary>
	/// Find out if the method overrides from target virtual method of a certain type
	/// or it is the virtual method itself.
	/// </summary>
	/// <param name="methodSymbol">The method</param>
	/// <param name="typeSymbol">The type has virtual method</param>
	public static bool IsOverrideOrVirtualMethodOf([NotNullWhen(true)] this IMethodSymbol? methodSymbol, [NotNullWhen(true)] INamedTypeSymbol? typeSymbol)
	{
		if (methodSymbol == null)
		{
			return false;
		}
		if (SymbolEqualityComparer.Default.Equals(methodSymbol.ContainingType, typeSymbol))
		{
			return true;
		}
		return methodSymbol.OverriddenMethod.IsOverrideOrVirtualMethodOf(typeSymbol);
	}

	/// <summary>
	/// Returns true if this is a bool returning static method whose name starts with "IsNull"
	/// with a single parameter whose type is not a value type.
	/// For example, "static bool string.IsNullOrEmpty()"
	/// </summary>
	public static bool IsArgumentNullCheckMethod(this IMethodSymbol method)
	{
		if (method.IsStatic && method.ReturnType.SpecialType == SpecialType.System_Boolean && method.Name.StartsWith("IsNull", StringComparison.Ordinal) && method.Parameters.Length == 1)
		{
			return !method.Parameters[0].Type.IsValueType;
		}
		return false;
	}

	public static bool IsBenchmarkOrXUnitTestMethod(this IMethodSymbol method, ConcurrentDictionary<INamedTypeSymbol, bool> knownTestAttributes, INamedTypeSymbol? benchmarkAttribute, INamedTypeSymbol? xunitFactAttribute)
	{
		ImmutableArray<AttributeData>.Enumerator enumerator = method.GetAttributes().GetEnumerator();
		while (enumerator.MoveNext())
		{
			if (enumerator.Current.AttributeClass.IsBenchmarkOrXUnitTestAttribute(knownTestAttributes, benchmarkAttribute, xunitFactAttribute))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Check if a method is an auto-property accessor.
	/// </summary>
	public static bool IsAutoPropertyAccessor(this IMethodSymbol methodSymbol)
	{
		if (methodSymbol.IsPropertyAccessor() && methodSymbol.AssociatedSymbol is IPropertySymbol propertySymbol)
		{
			return propertySymbol.IsAutoProperty();
		}
		return false;
	}

	/// <summary>
	/// Check if the given <paramref name="methodSymbol" /> is an implicitly generated method for top level statements.
	/// </summary>
	public static bool IsTopLevelStatementsEntryPointMethod([NotNullWhen(true)] this IMethodSymbol? methodSymbol)
	{
		bool flag = methodSymbol?.IsStatic ?? false;
		if (flag)
		{
			string name = methodSymbol.Name;
			bool flag2 = name == "$Main" || name == "<Main>$";
			flag = flag2;
		}
		return flag;
	}

	public static bool IsGetAwaiterFromAwaitablePattern([NotNullWhen(true)] this IMethodSymbol? method, [NotNullWhen(true)] INamedTypeSymbol? inotifyCompletionType, [NotNullWhen(true)] INamedTypeSymbol? icriticalNotifyCompletionType)
	{
		if (method == null || !method.Name.Equals("GetAwaiter", StringComparison.Ordinal) || method.Parameters.Length != 0)
		{
			return false;
		}
		ITypeSymbol typeSymbol = method.ReturnType?.OriginalDefinition;
		if (typeSymbol == null)
		{
			return false;
		}
		if (!typeSymbol.DerivesFrom(inotifyCompletionType))
		{
			return typeSymbol.DerivesFrom(icriticalNotifyCompletionType);
		}
		return true;
	}

	public static bool IsGetResultFromAwaiterPattern([NotNullWhen(true)] this IMethodSymbol? method, [NotNullWhen(true)] INamedTypeSymbol? inotifyCompletionType, [NotNullWhen(true)] INamedTypeSymbol? icriticalNotifyCompletionType)
	{
		if (method == null || !method.Name.Equals("GetResult", StringComparison.Ordinal) || method.Parameters.Length != 0)
		{
			return false;
		}
		INamedTypeSymbol namedTypeSymbol = method.ContainingType?.OriginalDefinition;
		if (namedTypeSymbol == null)
		{
			return false;
		}
		if (!namedTypeSymbol.DerivesFrom(inotifyCompletionType))
		{
			return namedTypeSymbol.DerivesFrom(icriticalNotifyCompletionType);
		}
		return true;
	}

	public static ImmutableArray<IMethodSymbol> GetOriginalDefinitions(this IMethodSymbol methodSymbol)
	{
		IMethodSymbol methodSymbol2 = methodSymbol;
		ImmutableArray<IMethodSymbol>.Builder builder = ImmutableArray.CreateBuilder<IMethodSymbol>();
		if (methodSymbol2.IsOverride && methodSymbol2.OverriddenMethod != null)
		{
			builder.Add(methodSymbol2.OverriddenMethod);
		}
		if (!methodSymbol2.ExplicitInterfaceImplementations.IsEmpty)
		{
			builder.AddRange(methodSymbol2.ExplicitInterfaceImplementations);
		}
		INamedTypeSymbol typeSymbol = methodSymbol2.ContainingType;
		string methodSymbolName = methodSymbol2.Name;
		builder.AddRange(from m in typeSymbol.AllInterfaces.SelectMany((INamedTypeSymbol m) => m.GetMembers(methodSymbolName)).OfType<IMethodSymbol>()
			where methodSymbol2.Parameters.Length == m.Parameters.Length && methodSymbol2.Arity == m.Arity && typeSymbol.FindImplementationForInterfaceMember(m) != null
			select m);
		return builder.ToImmutable();
	}
}
