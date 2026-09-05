using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities.Lightup;

internal readonly struct IUtf8StringOperationWrapper : IOperationWrapper
{
	internal const string WrappedTypeName = "Microsoft.CodeAnalysis.Operations.IUtf8StringOperation";

	private static readonly Type? WrappedType = OperationWrapperHelper.GetWrappedType(typeof(IUtf8StringOperationWrapper));

	private static readonly Func<IOperation, string> ValueAccessor = LightupHelpers.CreateOperationPropertyAccessor<IOperation, string>(WrappedType, "Value", null);

	public IOperation WrappedOperation { get; }

	public ITypeSymbol? Type => WrappedOperation.Type;

	public string Value => ValueAccessor(WrappedOperation);

	private IUtf8StringOperationWrapper(IOperation operation)
	{
		WrappedOperation = operation;
	}

	public static IUtf8StringOperationWrapper FromOperation(IOperation operation)
	{
		if (operation == null)
		{
			return default(IUtf8StringOperationWrapper);
		}
		if (!IsInstance(operation))
		{
			throw new InvalidCastException($"Cannot cast '{operation.GetType().FullName}' to '{"Microsoft.CodeAnalysis.Operations.IUtf8StringOperation"}'");
		}
		return new IUtf8StringOperationWrapper(operation);
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
