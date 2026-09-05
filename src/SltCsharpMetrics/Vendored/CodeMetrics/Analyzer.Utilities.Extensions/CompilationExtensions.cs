using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Analyzer.Utilities.Extensions;

/// <summary>
/// Provides extensions to <see cref="T:Microsoft.CodeAnalysis.Compilation" />.
/// </summary>
internal static class CompilationExtensions
{
	private static readonly byte[] mscorlibPublicKeyToken = new byte[8] { 183, 122, 92, 86, 25, 52, 224, 137 };

	private const string WebAppProjectGuidString = "{349C5851-65DF-11DA-9384-00065B846F21}";

	private const string WebSiteProjectGuidString = "{E24C65DC-7377-472B-9ABA-BC803B73C61A}";

	/// <summary>
	/// Gets a value indicating whether the project of the compilation is a Web SDK project based on project properties.
	/// </summary>
	internal static bool IsWebProject(this Compilation compilation, AnalyzerOptions options)
	{
		if (string.Equals(options.GetMSBuildPropertyValue("UsingMicrosoftNETSdkWeb", compilation)?.Trim(), "true", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		string mSBuildPropertyValue = options.GetMSBuildPropertyValue("ProjectTypeGuids", compilation);
		if (!RoslynString.IsNullOrEmpty(mSBuildPropertyValue) && (mSBuildPropertyValue.Contains("{349C5851-65DF-11DA-9384-00065B846F21}", StringComparison.OrdinalIgnoreCase) || mSBuildPropertyValue.Contains("{E24C65DC-7377-472B-9ABA-BC803B73C61A}", StringComparison.OrdinalIgnoreCase)))
		{
			ImmutableArray<string> immutableArray = (from g in mSBuildPropertyValue.Split(';')
				select g.Trim()).ToImmutableArray();
			if (!immutableArray.Contains("{349C5851-65DF-11DA-9384-00065B846F21}", StringComparer.OrdinalIgnoreCase))
			{
				return immutableArray.Contains("{E24C65DC-7377-472B-9ABA-BC803B73C61A}", StringComparer.OrdinalIgnoreCase);
			}
			return true;
		}
		return false;
	}

	/// <summary>
	/// Gets a type by its full type name and cache it at the compilation level.
	/// </summary>
	/// <param name="compilation">The compilation.</param>
	/// <param name="fullTypeName">Namespace + type name, e.g. "System.Exception".</param>
	/// <returns>The <see cref="T:Microsoft.CodeAnalysis.INamedTypeSymbol" /> if found, null otherwise.</returns>
	internal static INamedTypeSymbol? GetOrCreateTypeByMetadataName(this Compilation compilation, string fullTypeName)
	{
		return WellKnownTypeProvider.GetOrCreate(compilation).GetOrCreateTypeByMetadataName(fullTypeName);
	}

	/// <summary>
	/// Gets a type by its full type name and cache it at the compilation level.
	/// </summary>
	/// <param name="compilation">The compilation.</param>
	/// <param name="fullTypeName">Namespace + type name, e.g. "System.Exception".</param>
	/// <returns>The <see cref="T:Microsoft.CodeAnalysis.INamedTypeSymbol" /> if found, null otherwise.</returns>
	internal static bool TryGetOrCreateTypeByMetadataName(this Compilation compilation, string fullTypeName, [NotNullWhen(true)] out INamedTypeSymbol? namedTypeSymbol)
	{
		return WellKnownTypeProvider.GetOrCreate(compilation).TryGetOrCreateTypeByMetadataName(fullTypeName, out namedTypeSymbol);
	}

	/// <summary>
	/// Gets a value indicating, whether the compilation of assembly targets .NET Framework.
	/// This method differentiates between .NET Framework and other frameworks (.NET Core, .NET Standard, .NET 5 in future).
	/// </summary>
	/// <param name="compilation">The compilation</param>
	/// <returns><c>True</c> if the compilation targets .NET Framework; otherwise <c>false</c>.</returns>
	internal static bool TargetsDotNetFramework(this Compilation compilation)
	{
		AssemblyIdentity identity = compilation.GetSpecialType(SpecialType.System_Object).ContainingAssembly.Identity;
		if (identity.Name == "mscorlib" && identity.IsStrongName && (identity.Version == new Version(4, 0, 0, 0) || identity.Version == new Version(2, 0, 0, 0)) && identity.PublicKeyToken.Length == mscorlibPublicKeyToken.Length)
		{
			for (int i = 0; i < mscorlibPublicKeyToken.Length; i++)
			{
				if (identity.PublicKeyToken[i] != mscorlibPublicKeyToken[i])
				{
					return false;
				}
			}
			return true;
		}
		return false;
	}
}
