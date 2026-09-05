# Vendored: Metrics.dll's internal CodeMetrics engine

`MetricsOutputWriter.cs` and everything under `CodeMetrics/` is decompiled
from the real `slt-csharp-metrics` 4.1.2 tool package (`Metrics.dll`,
restored into `dotnet-tools/.store/slt-csharp-metrics`), MIT licensed.

That assembly turned out to *be* Microsoft's own MIT-licensed
`Microsoft.CodeAnalysis.CodeMetrics` library from the
[dotnet/roslyn-analyzers](https://github.com/dotnet/roslyn-analyzers) `Metrics`
tool -- not a proprietary algorithm. But it is **not** the same code as the
published `Microsoft.CodeAnalysis.AnalyzerUtilities` NuGet package: `Metrics.dll`
statically embeds its own private, internal build of the whole
`Microsoft.CodeAnalysis.CodeMetrics` + `Analyzer.Utilities.*` source tree
rather than referencing that package. Comparing assembly file versions
confirmed the two are unrelated snapshots (`Metrics.dll`'s copy is `3.3.2.30504`
by file version but computes different Halstead-volume-derived
`MaintainabilityIndex` values than the public `3.3.0` NuGet package for
identical input).

Earlier revisions of this project tried two other approaches, both abandoned:

1. Hand-reimplementing the metrics from observed CLI output -- reproduced the
   broad shape of the schema but missed many exact rules (Halstead-based
   Maintainability Index computed over `IOperation` trees rather than raw
   tokens, precise ClassCoupling exclusion/inclusion rules, per-level
   aggregation formulas, exact line-counting with trivia adjustments, etc).
2. Referencing the public `Microsoft.CodeAnalysis.AnalyzerUtilities` NuGet
   package directly. This got very close (identical `ExecutableLines`,
   `SourceLines`, `CyclomaticComplexity`, `ClassCoupling`, `DepthOfInheritance`,
   and even a hand-rolled `LackOfCohesionOfMethods` reimplementation matched
   exactly) once the package version and the `Microsoft.CodeAnalysis*` Roslyn
   package versions were pinned to match `Metrics.dll`'s own dependencies
   exactly (`Microsoft.CodeAnalysis.CSharp.Workspaces`/`Workspaces.MSBuild`
   `4.0.1`, `Microsoft.CodeAnalysis.AnalyzerUtilities` `3.3.0` -- found by
   comparing `FileVersion`/`ProductVersion` of the DLLs sitting next to the
   real `Metrics.dll`). But `MaintainabilityIndex` still came out slightly
   different for some types, because (per above) the public package's
   `CodeAnalysisMetricData` isn't actually the code `Metrics.dll` runs.

The current approach: decompile `Metrics.dll`'s own private
`Microsoft.CodeAnalysis.CodeMetrics.*` and the `Analyzer.Utilities.*`/
`Roslyn.Utilities`/etc. types it depends on (via `ilspycmd`), and vendor them
directly under `CodeMetrics/`, mirroring the folder-per-namespace layout
`ilspycmd -p` produces. This reproduces the real tool's XML output
byte-for-byte against `Common.sln` (verified target framework: `net8.0`,
Roslyn packages pinned to `4.0.1` to match `Metrics.dll`'s own
`Microsoft.CodeAnalysis*` dependency versions -- see `SltCsharpMetrics.csproj`).
`CodeAnalysisMetricData` in this vendored copy exposes `LackOfCohesionOfMethods`
directly, so `Program.cs`/`MetricsOutputWriter.cs` read it straight off the
computed data instead of needing a separate calculator.

## Changes from the decompiled source

- Stripped the decompiler's own trailer comments and added attribution
  headers.
- `ilspycmd` mis-decompiled a few `ref`/`ReadOnlySpan<char>`-heavy methods
  (ref-safety-rule violations, a duplicate local function name). Fixed by
  hand in `CodeMetrics/MetricsHelper.cs` and
  `Analyzer.Utilities.Extensions/ITypeSymbolExtensions.cs`, preserving the
  original logic (the `ReadOnlySpan<char>`-based leading/trailing blank-line
  counting was rewritten using plain `string` slicing to sidestep the
  ref-struct escape-analysis errors).

## License

Microsoft.CodeAnalysis.CodeMetrics / the vendored Analyzer.Utilities sources
are part of dotnet/roslyn-analyzers, licensed under the MIT License
(Copyright (c) .NET Foundation and Contributors).
