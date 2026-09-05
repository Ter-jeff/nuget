using System.Runtime.InteropServices;

namespace Analyzer.Utilities;

/// <summary>
/// Represents a type with a single value. This type is often used to denote the successful completion of a void-returning method (C#) or a Sub procedure (Visual Basic).
/// </summary>
/// <remarks>
/// This class is a duplicate from "https://github.com/dotnet/reactive/blob/main/Rx.NET/Source/src/System.Reactive/Unit.cs
/// </remarks>
[StructLayout(LayoutKind.Sequential, Size = 1)]
public struct Unit : IEquatable<Unit>
{
	/// <summary>
	/// Gets the single <see cref="T:Analyzer.Utilities.Unit" /> value.
	/// </summary>
	public static Unit Default => default(Unit);

	/// <summary>
	/// Determines whether the specified <see cref="T:Analyzer.Utilities.Unit" /> value is equal to the current <see cref="T:Analyzer.Utilities.Unit" />. Because <see cref="T:Analyzer.Utilities.Unit" /> has a single value, this always returns <c>true</c>.
	/// </summary>
	/// <param name="other">An object to compare to the current <see cref="T:Analyzer.Utilities.Unit" /> value.</param>
	/// <returns>Because <see cref="T:Analyzer.Utilities.Unit" /> has a single value, this always returns <c>true</c>.</returns>
	public readonly bool Equals(Unit other)
	{
		return true;
	}

	/// <summary>
	/// Determines whether the specified System.Object is equal to the current <see cref="T:Analyzer.Utilities.Unit" />.
	/// </summary>
	/// <param name="obj">The System.Object to compare with the current <see cref="T:Analyzer.Utilities.Unit" />.</param>
	/// <returns><c>true</c> if the specified System.Object is a <see cref="T:Analyzer.Utilities.Unit" /> value; otherwise, <c>false</c>.</returns>
	public override readonly bool Equals(object? obj)
	{
		return obj is Unit;
	}

	/// <summary>
	/// Returns the hash code for the current <see cref="T:Analyzer.Utilities.Unit" /> value.
	/// </summary>
	/// <returns>A hash code for the current <see cref="T:Analyzer.Utilities.Unit" /> value.</returns>
	public override readonly int GetHashCode()
	{
		return 0;
	}

	/// <summary>
	/// Returns a string representation of the current <see cref="T:Analyzer.Utilities.Unit" /> value.
	/// </summary>
	/// <returns>String representation of the current <see cref="T:Analyzer.Utilities.Unit" /> value.</returns>
	public override readonly string ToString()
	{
		return "()";
	}

	/// <summary>
	/// Determines whether the two specified <see cref="T:Analyzer.Utilities.Unit" /> values are equal. Because <see cref="T:Analyzer.Utilities.Unit" /> has a single value, this always returns <c>true</c>.
	/// </summary>
	/// <param name="first">The first <see cref="T:Analyzer.Utilities.Unit" /> value to compare.</param>
	/// <param name="second">The second <see cref="T:Analyzer.Utilities.Unit" /> value to compare.</param>
	/// <returns>Because <see cref="T:Analyzer.Utilities.Unit" /> has a single value, this always returns <c>true</c>.</returns>
	public static bool operator ==(Unit first, Unit second)
	{
		return true;
	}

	/// <summary>
	/// Determines whether the two specified <see cref="T:Analyzer.Utilities.Unit" /> values are not equal. Because <see cref="T:Analyzer.Utilities.Unit" /> has a single value, this always returns <c>false</c>.
	/// </summary>
	/// <param name="first">The first <see cref="T:Analyzer.Utilities.Unit" /> value to compare.</param>
	/// <param name="second">The second <see cref="T:Analyzer.Utilities.Unit" /> value to compare.</param>
	/// <returns>Because <see cref="T:Analyzer.Utilities.Unit" /> has a single value, this always returns <c>false</c>.</returns>
	public static bool operator !=(Unit first, Unit second)
	{
		return false;
	}
}
