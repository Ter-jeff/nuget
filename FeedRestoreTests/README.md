# FeedRestoreTests

Integration test that `csharp-duplicate-detector` and `slt-csharp-metrics`
(built from [`../src`](../src), packed into [`../feed`](../feed)) actually
resolve and run once restored as `dotnet tool`s — the same way a real
consuming project (e.g. `TrainingProgram`) would via
`.config/dotnet-tools.json`.

There's no application source to unit-test here — these are prebuilt CLI
tools — so this instead does the thing that actually matters: install them
exactly as a consumer would, and confirm each one runs.

This project is self-contained on purpose: its own [`NuGet.Config`](NuGet.Config)
and [`.config/dotnet-tools.json`](.config/dotnet-tools.json) live next to it
rather than at the repo root, so they don't affect how `src/*` restores.

## What it does

- [`NuGet.Config`](NuGet.Config) points at the GitHub Packages feed (plus a
  local-folder fallback at `../feed`, used before a given version has been
  pushed to `main` and published by `publish-packages.yml` — safe to drop
  once that's happened once).
- [`.config/dotnet-tools.json`](.config/dotnet-tools.json) pins both tools,
  same as any consuming repo would.
- [`ToolRestoreTests.cs`](ToolRestoreTests.cs) (MSTest) runs
  `dotnet tool restore` and then invokes each tool
  (`csharp-duplicate-detector --version`, `slt-csharp-metrics /help`),
  asserting a zero exit code and expected output.

## Running

```
dotnet test FeedRestoreTests
```
