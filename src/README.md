# src

Source for the two custom `DotnetTool` packages published to `feed/`:

- **CsharpDuplicateDetector** → package `csharp-duplicate-detector`, command `csharp-duplicate-detector`
- **SltCsharpMetrics** → package `slt-csharp-metrics`, command `slt-csharp-metrics`

Both are clean-room reimplementations, not the original vendored binaries. The
originals (`SltDuplicateCodeDetector` 1.1.0 and `slt-csharp-metrics` 4.1.2,
currently restored into `TrainingProgram/.devops/dotnet-tools`) come from a
private GitLab fork of two MIT-licensed Microsoft projects —
[near-duplicate-code-detector](https://github.com/microsoft/near-duplicate-code-detector)
and the `dotnet/roslyn` code-metrics tool (`src/RoslynAnalyzers/Tools/Metrics`)
— that we don't have source access to, and whose upstream forms are no longer
a drop-in match (the private fork rewrote the CLI substantially, and the
metrics tool has since been absorbed into the huge `dotnet/roslyn` monorepo
with a dependency graph that isn't realistically vendorable standalone). So
rather than guess at the private fork's exact internals, these projects
reproduce the **observed CLI contract and output schema** — verified by
running the real `.exe` files from `TrainingProgram/.devops/dotnet-tools`
against a sample project and diffing outputs — using straightforward Roslyn
APIs.

## What matches exactly (verified against the real tools)

- Both tools' full `--help` / `/help` usage text, byte-for-byte.
- `csharp-duplicate-detector`: console progress/summary format, `duplicates.csv`
  header, `duplicates.json` shape (`[]` when empty), exit codes.
- `slt-csharp-metrics`: `CodeMetricsReport` XML schema (`Targets/Target/Assembly
  /Namespaces/Namespace/Types/NamedType/Members/Method`, same `Metric`
  element/attribute names), and — on the sample project used to validate this
  — exact matches for `CyclomaticComplexity`, `ClassCoupling`,
  `DepthOfInheritance`, `SourceLines` and `ExecutableLines` per method.

## Known approximations

- `slt-csharp-metrics`: `MaintainabilityIndex` uses the public Halstead-based
  formula, not the private tool's exact internal implementation — expect the
  same 0-100 scale and rough ballpark, not identical numbers. Namespace/
  project-level `SourceLines` is a rollup of contained types rather than the
  raw namespace-block span, so it can be a few lines lower than the original.
  Nested types aren't walked (only top-level types per namespace).
  `LackOfCohesionOfMethods` and `ProjectClassCouplingList` are always emitted
  as `NaN` / `?` — the real tool doesn't populate the latter either.
- `csharp-duplicate-detector`: Jaccard similarity (set and multiset) is the
  standard textbook definition applied to Roslyn token text, which is what
  the tool's `-k`/`-j` options describe, but the exact tokenization rules
  (e.g. whether literals are normalized) may differ slightly from the
  original in edge cases.

## Building

```
dotnet pack -c Release src/CsharpDuplicateDetector
dotnet pack -c Release src/SltCsharpMetrics
```

Copy the resulting `.nupkg` from `bin/Release/` into `feed/` following the
same process as every other package in this repo (see the root
[README](../README.md)).
