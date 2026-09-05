// Vendored from Microsoft's dotnet/roslyn-analyzers "Metrics" tool
// (Microsoft.CodeAnalysis.CodeMetrics), MIT licensed. Decompiled from the
// slt-csharp-metrics 4.1.2 tool package (Metrics.dll) restored into
// TrainingProgram/.devops/dotnet-tools, and adapted for use here.
// See Vendored/README.md.

using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeMetrics;

namespace Metrics;

internal static class MetricsOutputWriter
{
	private const string Version = "1.0";

	public static void WriteMetricFile(
		ImmutableArray<(string, CodeAnalysisMetricData)> data,
		XmlTextWriter writer)
	{
		XmlTextWriter writer2 = writer;
		writer2.Formatting = Formatting.Indented;
		writer2.WriteStartDocument();
		writer2.WriteStartElement("CodeMetricsReport");
		writer2.WriteAttributeString("Version", "1.0");
		writer2.WriteStartElement("Targets");
		writeMetrics();
		writer2.WriteEndElement();
		writer2.WriteEndElement();
		writer2.WriteEndDocument();
		void writeMetrics()
		{
			ImmutableArray<(string, CodeAnalysisMetricData)>.Enumerator enumerator = data.GetEnumerator();
			while (enumerator.MoveNext())
			{
				var (path, data2) = enumerator.Current;
				writer2.WriteStartElement("Target");
				writer2.WriteAttributeString("Name", Path.GetFileName(path));
				WriteMetricData(data2, writer2);
				writer2.WriteEndElement();
			}
		}
	}

	private static void WriteMetricData(CodeAnalysisMetricData data, XmlTextWriter writer)
	{
		XmlTextWriter writer2 = writer;
		CodeAnalysisMetricData data2 = data;
		writeHeader();
		writeMetrics();
		writeChildren();
		writer2.WriteEndElement();
		void writeChildren()
		{
			if (!data2.Children.IsEmpty)
			{
				bool flag;
				switch (data2.Symbol.Kind)
				{
				case SymbolKind.Assembly:
					writer2.WriteStartElement("Namespaces");
					flag = true;
					break;
				case SymbolKind.Namespace:
					writer2.WriteStartElement("Types");
					flag = true;
					break;
				case SymbolKind.NamedType:
					writer2.WriteStartElement("Members");
					flag = true;
					break;
				case SymbolKind.Event:
				case SymbolKind.Property:
					writer2.WriteStartElement("Accessors");
					flag = true;
					break;
				default:
					flag = false;
					break;
				}
				ImmutableArray<CodeAnalysisMetricData>.Enumerator enumerator = data2.Children.GetEnumerator();
				while (enumerator.MoveNext())
				{
					WriteMetricData(enumerator.Current, writer2);
				}
				if (flag)
				{
					writer2.WriteEndElement();
				}
			}
		}
		void writeHeader()
		{
			writer2.WriteStartElement(data2.Symbol.Kind.ToString());
			switch (data2.Symbol.Kind)
			{
			case SymbolKind.NamedType:
			{
				StringBuilder stringBuilder = new StringBuilder(data2.Symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
				for (INamedTypeSymbol containingType = data2.Symbol.ContainingType; containingType != null; containingType = containingType.ContainingType)
				{
					stringBuilder.Insert(0, ".");
					stringBuilder.Insert(0, containingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
				}
				writer2.WriteAttributeString("Name", stringBuilder.ToString());
				Location location2 = data2.Symbol.Locations.First();
				writer2.WriteAttributeString("File", location2.SourceTree?.FilePath ?? "UNKNOWN");
				writer2.WriteAttributeString("Line", (location2.GetLineSpan().StartLinePosition.Line + 1).ToString(CultureInfo.InvariantCulture));
				break;
			}
			case SymbolKind.Event:
			case SymbolKind.Field:
			case SymbolKind.Method:
			case SymbolKind.Property:
			{
				Location location = data2.Symbol.Locations.First();
				writer2.WriteAttributeString("Name", data2.Symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
				writer2.WriteAttributeString("File", location.SourceTree?.FilePath ?? "UNKNOWN");
				writer2.WriteAttributeString("Line", (location.GetLineSpan().StartLinePosition.Line + 1).ToString(CultureInfo.InvariantCulture));
				writer2.WriteAttributeString("Private", data2.Symbol.DeclaredAccessibility == Accessibility.Private ? "true" : "false");
				break;
			}
			default:
				writer2.WriteAttributeString("Name", data2.Symbol.ToDisplayString());
				break;
			}
			if (data2.Symbol.Kind == SymbolKind.Field)
			{
				writer2.WriteAttributeString("Constant", data2.Symbol is IFieldSymbol { IsConst: true } ? "true" : "false");
			}
		}
		void writeMetrics()
		{
			writer2.WriteStartElement("Metrics");
			WriteMetric("MaintainabilityIndex", data2.MaintainabilityIndex.ToString(CultureInfo.InvariantCulture), writer2);
			WriteMetric("CyclomaticComplexity", data2.CyclomaticComplexity.ToString(CultureInfo.InvariantCulture), writer2);
			WriteMetric("ClassCoupling", data2.CoupledNamedTypes.Count.ToString(CultureInfo.InvariantCulture), writer2);
			if (data2.DepthOfInheritance.HasValue)
			{
				WriteMetric("DepthOfInheritance", data2.DepthOfInheritance.Value.ToString(CultureInfo.InvariantCulture), writer2);
			}
			if (data2.Symbol.Kind == SymbolKind.NamedType)
			{
				if (data2.LackOfCohesionOfMethods.HasValue)
				{
					WriteMetric("LackOfCohesionOfMethods", data2.LackOfCohesionOfMethods.Value.ToString("0.##"), writer2);
				}
				string[] array = (from namedType in data2.CoupledNamedTypes
					where !namedType.IsStatic && namedType.TypeKind != TypeKind.Enum
					select namedType.ToDisplayString() into typeName
					where !typeName.StartsWith("System.", StringComparison.InvariantCulture)
					orderby typeName
					select typeName).ToArray();
				WriteMetric("ProjectClassCoupling", array.Length.ToString(CultureInfo.InvariantCulture), writer2);
				WriteMetric("ProjectClassCouplingList", string.Join("%", array), writer2);
			}
			WriteMetric("SourceLines", data2.SourceLines.ToString(CultureInfo.InvariantCulture), writer2);
			WriteMetric("ExecutableLines", data2.ExecutableLines.ToString(CultureInfo.InvariantCulture), writer2);
			writer2.WriteEndElement();
		}
	}

	private static void WriteMetric(string name, string value, XmlTextWriter writer)
	{
		writer.WriteStartElement("Metric");
		writer.WriteAttributeString("Name", name);
		writer.WriteAttributeString("Value", value);
		writer.WriteEndElement();
	}
}
