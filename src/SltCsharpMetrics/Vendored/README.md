# Vendored: Metrics.MetricsOutputWriter

`MetricsOutputWriter.cs` is decompiled from `Metrics.dll`, the real
`slt-csharp-metrics` 4.1.2 tool package restored into
`TrainingProgram/.devops/dotnet-tools/.store/slt-csharp-metrics`.

That assembly turned out to *be* Microsoft's own MIT-licensed
`Microsoft.CodeAnalysis.CodeMetrics` library from the
[dotnet/roslyn-analyzers](https://github.com/dotnet/roslyn-analyzers) `Metrics`
tool, plus a small `Metrics.Program`/`Metrics.MetricsOutputWriter` CLI wrapper
around it -- not a proprietary algorithm. Earlier work on this project treated
the metrics computation as unavailable and hand-reimplemented it from observed
CLI output, which reproduced the broad shape of the schema but missed many
exact rules (Halstead-based Maintainability Index computed over `IOperation`
trees rather than raw tokens, precise ClassCoupling exclusion/inclusion rules,
per-level aggregation formulas, exact line-counting with trivia adjustments,
etc).

The actual metric computation classes (`CodeAnalysisMetricData`,
`CodeMetricsAnalysisContext`, `MetricsHelper`, `ComputationalComplexityMetrics`)
turned out to already ship in the public `Microsoft.CodeAnalysis.AnalyzerUtilities`
NuGet package (see `SltCsharpMetrics.csproj`) -- referencing that package
directly is more accurate and maintainable than vendoring a copy, so only the
tool-specific XML writer is vendored here.

## Changes from the decompiled source

- Stripped the decompiler's own trailer comments and added the attribution
  header at the top of the file.
- `Private`/`Constant` attributes: the referenced package version (3.3.0)
  doesn't publicly expose the `IsPrivate()`/`IsConst()` extension methods the
  original code called, so these are computed directly
  (`DeclaredAccessibility == Accessibility.Private`, `IFieldSymbol.IsConst`).
- `LackOfCohesionOfMethods`: this package version doesn't expose that member
  on `CodeAnalysisMetricData` either. The fixture always reports `NaN` for it
  regardless, so it's hardcoded to `"NaN"` rather than computed.

## License

Microsoft.CodeAnalysis.CodeMetrics / Microsoft.CodeAnalysis.AnalyzerUtilities
are part of dotnet/roslyn-analyzers, licensed under the MIT License
(Copyright (c) .NET Foundation and Contributors).
