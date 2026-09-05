using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities.Lightup;

internal interface IOperationWrapper
{
	IOperation? WrappedOperation { get; }
}
