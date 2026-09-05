using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities;

internal static class DiagnosticHelpers
{
	public static bool TryConvertToUInt64(object? value, SpecialType specialType, out ulong convertedValue)
	{
		bool result = false;
		convertedValue = 0uL;
		if (value != null)
		{
			switch (specialType)
			{
			case SpecialType.System_Int16:
				convertedValue = (ulong)(short)value;
				result = true;
				break;
			case SpecialType.System_Int32:
				convertedValue = (ulong)(int)value;
				result = true;
				break;
			case SpecialType.System_Int64:
				convertedValue = (ulong)(long)value;
				result = true;
				break;
			case SpecialType.System_UInt16:
				convertedValue = (ushort)value;
				result = true;
				break;
			case SpecialType.System_UInt32:
				convertedValue = (uint)value;
				result = true;
				break;
			case SpecialType.System_UInt64:
				convertedValue = (ulong)value;
				result = true;
				break;
			case SpecialType.System_Byte:
				convertedValue = (byte)value;
				result = true;
				break;
			case SpecialType.System_SByte:
				convertedValue = (ulong)(sbyte)value;
				result = true;
				break;
			case SpecialType.System_Char:
				convertedValue = (char)value;
				result = true;
				break;
			case SpecialType.System_Boolean:
				convertedValue = (ulong)(((bool)value) ? 1 : 0);
				result = true;
				break;
			}
		}
		return result;
	}

	public static string GetMemberName(ISymbol symbol)
	{
		if (symbol is INamedTypeSymbol { IsGenericType: not false })
		{
			return symbol.MetadataName;
		}
		return symbol.Name;
	}
}
