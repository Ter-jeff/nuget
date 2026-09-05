using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities.Extensions;

internal sealed class ParameterInfo
{
	public int ArrayRank { get; private set; }

	public bool IsArray { get; private set; }

	public bool IsParams { get; private set; }

	public INamedTypeSymbol ParameterType { get; private set; }

	private ParameterInfo(INamedTypeSymbol type, bool isArray, int arrayRank, bool isParams)
	{
		ParameterType = type;
		IsArray = isArray;
		ArrayRank = arrayRank;
		IsParams = isParams;
	}

	public static ParameterInfo GetParameterInfo(INamedTypeSymbol type, bool isArray = false, int arrayRank = 0, bool isParams = false)
	{
		return new ParameterInfo(type, isArray, arrayRank, isParams);
	}
}
