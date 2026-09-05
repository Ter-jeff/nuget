using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Analyzer.Utilities.Lightup;

internal readonly struct IFunctionPointerInvocationOperationWrapper : IOperationWrapper
{
	internal const string WrappedTypeName = "Microsoft.CodeAnalysis.Operations.IFunctionPointerInvocationOperation";

	private static readonly Type? WrappedType = OperationWrapperHelper.GetWrappedType(typeof(IFunctionPointerInvocationOperationWrapper));

	private static readonly Func<IOperation, ImmutableArray<IArgumentOperation>> ArgumentsAccessor = LightupHelpers.CreateOperationPropertyAccessor<IOperation, ImmutableArray<IArgumentOperation>>(WrappedType, "Arguments", ImmutableArray<IArgumentOperation>.Empty);

	private static readonly Func<IOperation, IOperation> TargetAccessor = LightupHelpers.CreateOperationPropertyAccessor<IOperation, IOperation>(WrappedType, "Target", null);

	private static readonly Func<IOperation, IMethodSymbol> GetFunctionPointerSignatureAccessor = CreateFunctionPointerSignatureAccessor(WrappedType);

	public IOperation WrappedOperation { get; }

	public ITypeSymbol? Type => WrappedOperation.Type;

	public ImmutableArray<IArgumentOperation> Arguments => ArgumentsAccessor(WrappedOperation);

	public IOperation Target => TargetAccessor(WrappedOperation);

	private static Func<IOperation, IMethodSymbol> CreateFunctionPointerSignatureAccessor(Type? wrappedType)
	{
		if (wrappedType == null)
		{
			return (IOperation op) => (IMethodSymbol)null;
		}
		MethodInfo declaredMethod = typeof(OperationExtensions).GetTypeInfo().GetDeclaredMethod("GetFunctionPointerSignature");
		if ((object)declaredMethod == null)
		{
			return (IOperation op) => (IMethodSymbol)null;
		}
		ParameterExpression parameterExpression = Expression.Variable(typeof(IOperation));
		return Expression.Lambda<Func<IOperation, IMethodSymbol>>(Expression.Call(declaredMethod, Expression.Convert(parameterExpression, wrappedType)), new ParameterExpression[1] { parameterExpression }).Compile();
	}

	private IFunctionPointerInvocationOperationWrapper(IOperation operation)
	{
		WrappedOperation = operation;
	}

	public IMethodSymbol GetFunctionPointerSignature()
	{
		return GetFunctionPointerSignatureAccessor(WrappedOperation);
	}

	public static IFunctionPointerInvocationOperationWrapper FromOperation(IOperation operation)
	{
		if (operation == null)
		{
			return default(IFunctionPointerInvocationOperationWrapper);
		}
		if (!IsInstance(operation))
		{
			throw new InvalidCastException($"Cannot cast '{operation.GetType().FullName}' to '{"Microsoft.CodeAnalysis.Operations.IFunctionPointerInvocationOperation"}'");
		}
		return new IFunctionPointerInvocationOperationWrapper(operation);
	}

	public static bool IsInstance(IOperation operation)
	{
		if (operation != null)
		{
			return LightupHelpers.CanWrapOperation(operation, WrappedType);
		}
		return false;
	}
}
