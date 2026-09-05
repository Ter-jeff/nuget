using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Analyzer.Utilities.Extensions;

internal static class DiagnosticExtensions
{
	/// <summary>
	/// TODO: Revert this reflection based workaround once we move to Microsoft.CodeAnalysis version 3.0
	/// </summary>
	private static readonly PropertyInfo? s_syntaxTreeDiagnosticOptionsProperty = typeof(SyntaxTree).GetTypeInfo().GetDeclaredProperty("DiagnosticOptions");

	private static readonly PropertyInfo? s_compilationOptionsSyntaxTreeOptionsProviderProperty = typeof(CompilationOptions).GetTypeInfo().GetDeclaredProperty("SyntaxTreeOptionsProvider");

	public static Diagnostic CreateDiagnostic(this SyntaxNode node, DiagnosticDescriptor rule, params object[] args)
	{
		return node.CreateDiagnostic(rule, null, args);
	}

	public static Diagnostic CreateDiagnostic(this SyntaxNode node, DiagnosticDescriptor rule, ImmutableDictionary<string, string?>? properties, params object[] args)
	{
		return node.CreateDiagnostic(rule, ImmutableArray<Location>.Empty, properties, args);
	}

	public static Diagnostic CreateDiagnostic(this SyntaxNode node, DiagnosticDescriptor rule, ImmutableArray<Location> additionalLocations, ImmutableDictionary<string, string?>? properties, params object[] args)
	{
		return node.GetLocation().CreateDiagnostic(rule, additionalLocations, properties, args);
	}

	public static Diagnostic CreateDiagnostic(this IOperation operation, DiagnosticDescriptor rule, params object[] args)
	{
		return operation.CreateDiagnostic(rule, null, args);
	}

	public static Diagnostic CreateDiagnostic(this IOperation operation, DiagnosticDescriptor rule, ImmutableDictionary<string, string?>? properties, params object[] args)
	{
		return operation.Syntax.CreateDiagnostic(rule, properties, args);
	}

	public static Diagnostic CreateDiagnostic(this IOperation operation, DiagnosticDescriptor rule, ImmutableArray<Location> additionalLocations, ImmutableDictionary<string, string?>? properties, params object[] args)
	{
		return operation.Syntax.CreateDiagnostic(rule, additionalLocations, properties, args);
	}

	public static Diagnostic CreateDiagnostic(this SyntaxToken token, DiagnosticDescriptor rule, params object[] args)
	{
		return token.GetLocation().CreateDiagnostic(rule, args);
	}

	public static Diagnostic CreateDiagnostic(this ISymbol symbol, DiagnosticDescriptor rule, params object[] args)
	{
		return symbol.Locations.CreateDiagnostic(rule, args);
	}

	public static Diagnostic CreateDiagnostic(this ISymbol symbol, DiagnosticDescriptor rule, ImmutableDictionary<string, string?>? properties, params object[] args)
	{
		return symbol.Locations.CreateDiagnostic(rule, properties, args);
	}

	public static Diagnostic CreateDiagnostic(this Location location, DiagnosticDescriptor rule, params object[] args)
	{
		return location.CreateDiagnostic(rule, ImmutableDictionary<string, string>.Empty, args);
	}

	public static Diagnostic CreateDiagnostic(this Location location, DiagnosticDescriptor rule, ImmutableDictionary<string, string?>? properties, params object[] args)
	{
		return location.CreateDiagnostic(rule, ImmutableArray<Location>.Empty, properties, args);
	}

	public static Diagnostic CreateDiagnostic(this Location location, DiagnosticDescriptor rule, ImmutableArray<Location> additionalLocations, ImmutableDictionary<string, string?>? properties, params object[] args)
	{
		if (!location.IsInSource)
		{
			location = Location.None;
		}
		return Diagnostic.Create(rule, location, additionalLocations, properties, args);
	}

	public static Diagnostic CreateDiagnostic(this IEnumerable<Location> locations, DiagnosticDescriptor rule, params object[] args)
	{
		return locations.CreateDiagnostic(rule, null, args);
	}

	public static Diagnostic CreateDiagnostic(this IEnumerable<Location> locations, DiagnosticDescriptor rule, ImmutableDictionary<string, string?>? properties, params object[] args)
	{
		IEnumerable<Location> source = locations.Where((Location l) => l.IsInSource);
		if (!source.Any())
		{
			return Diagnostic.Create(rule, null, args);
		}
		return Diagnostic.Create(rule, source.First(), source.Skip(1), properties, args);
	}

	public static void ReportNoLocationDiagnostic(this CompilationAnalysisContext context, DiagnosticDescriptor rule, params object[] args)
	{
		context.Compilation.ReportNoLocationDiagnostic(rule, ((CompilationAnalysisContext)context).ReportDiagnostic, null, args);
	}

	public static void ReportNoLocationDiagnostic(this SyntaxNodeAnalysisContext context, DiagnosticDescriptor rule, params object[] args)
	{
		context.Compilation.ReportNoLocationDiagnostic(rule, ((SyntaxNodeAnalysisContext)context).ReportDiagnostic, null, args);
	}

	public static void ReportNoLocationDiagnostic(this Compilation compilation, DiagnosticDescriptor rule, Action<Diagnostic> addDiagnostic, ImmutableDictionary<string, string?>? properties, params object[] args)
	{
		Compilation compilation2 = compilation;
		DiagnosticDescriptor rule2 = rule;
		DiagnosticSeverity? diagnosticSeverity = GetEffectiveSeverity();
		if (diagnosticSeverity.HasValue)
		{
			if (diagnosticSeverity.Value != rule2.DefaultSeverity)
			{
				rule2 = new DiagnosticDescriptor(rule2.Id, rule2.Title, rule2.MessageFormat, rule2.Category, diagnosticSeverity.Value, rule2.IsEnabledByDefault, rule2.Description, rule2.HelpLinkUri, rule2.CustomTags.ToArray());
			}
			Diagnostic obj = Diagnostic.Create(rule2, Location.None, properties, args);
			addDiagnostic(obj);
		}
		DiagnosticSeverity? GetEffectiveSeverity()
		{
			object obj2 = s_compilationOptionsSyntaxTreeOptionsProviderProperty?.GetValue(compilation2.Options);
			MethodInfo methodInfo = obj2?.GetType().GetRuntimeMethods().FirstOrDefault((MethodInfo m) => m.Name == "TryGetDiagnosticValue");
			if (methodInfo == null && s_syntaxTreeDiagnosticOptionsProperty == null)
			{
				return rule2.DefaultSeverity;
			}
			ReportDiagnostic? reportDiagnostic = null;
			foreach (SyntaxTree syntaxTree in compilation2.SyntaxTrees)
			{
				ReportDiagnostic? reportDiagnostic2 = null;
				ReportDiagnostic value2;
				if (s_compilationOptionsSyntaxTreeOptionsProviderProperty != null)
				{
					if (methodInfo != null)
					{
						object[] array = ((methodInfo.GetParameters().Length != 3) ? new object[4]
						{
							syntaxTree,
							rule2.Id,
							CancellationToken.None,
							null
						} : new object[3] { syntaxTree, rule2.Id, null });
						object obj3 = methodInfo.Invoke(obj2, array);
						if (obj3 is bool && (bool)obj3 && array.Last() is ReportDiagnostic value)
						{
							reportDiagnostic2 = value;
						}
					}
				}
				else if (((ImmutableDictionary<string, ReportDiagnostic>)s_syntaxTreeDiagnosticOptionsProperty.GetValue(syntaxTree)).TryGetValue(rule2.Id, out value2))
				{
					reportDiagnostic2 = value2;
				}
				if (reportDiagnostic2.HasValue)
				{
					if (reportDiagnostic2.GetValueOrDefault() == ReportDiagnostic.Suppress)
					{
						return null;
					}
					if (!reportDiagnostic.HasValue)
					{
						reportDiagnostic = reportDiagnostic2;
					}
					else if (reportDiagnostic.Value.IsLessSevereThan(reportDiagnostic2.Value))
					{
						reportDiagnostic = reportDiagnostic2;
					}
				}
			}
			if (!reportDiagnostic.HasValue)
			{
				return rule2.DefaultSeverity;
			}
			return reportDiagnostic.Value.ToDiagnosticSeverity();
		}
	}
}
