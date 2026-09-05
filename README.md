# nuget

Drop-and-publish mirror of public NuGet packages onto our own GitHub
Packages feed (`https://nuget.pkg.github.com/Ter-jeff/index.json`). We
switched the `test` repo's `NuGet.Config` to that feed exclusively (no
`nuget.org`), so every package it references — including `dotnet tool`
packages installed in CI — has to be republished here first.

## Adding or updating a package

1. Download the exact `.nupkg` your consuming project pins, e.g.:

   ```bash
   curl -sSfL -o feed/epplus.4.5.3.2.nupkg \
     https://api.nuget.org/v3-flatcontainer/epplus/4.5.3.2/epplus.4.5.3.2.nupkg
   ```

2. Commit it under [`feed/`](feed) and push to `main`.
3. That's it — [`.github/workflows/publish-packages.yml`](.github/workflows/publish-packages.yml)
   picks up any changed `feed/**.nupkg` file and pushes it to our GitHub
   Packages feed automatically. Already-published id+version pairs are
   skipped safely (`--skip-duplicate`).

`.nupkg` files under `feed/` are committed on purpose — this repo *is* the
audit trail of what's been mirrored and when.

### One-time setup: `GH_PACKAGES_PAT` secret

The workflow authenticates with a **real PAT**, not the built-in
`GITHUB_TOKEN` — GitHub rejects `GITHUB_TOKEN` pushes for packages whose
nuspec `<repository>` metadata points somewhere other than this repo (which
is every package we mirror, since we're republishing other projects'
`.nupkg` files as-is). Create a repository secret:

**Settings → Secrets and variables → Actions → New repository secret**
- Name: `GH_PACKAGES_PAT`
- Value: a PAT with `write:packages` scope

Only needs to be done once; rotate the PAT there whenever it expires.

## Custom tool packages

`csharp-duplicate-detector` and `slt-csharp-metrics` aren't mirrored from
nuget.org — they're built from source in [`src/`](src), packed locally, and
committed to `feed/` like everything else. See [`src/README.md`](src/README.md)
for what they are and how they were validated against the originals.
[`FeedRestoreTests/`](FeedRestoreTests) integration-tests that both actually
restore and run as `dotnet tool`s.

## Note on build tools

Not everything a project needs has to go through this feed. Build-time tools
(e.g. `dotnet-reportgenerator-globaltool` in the `test` repo's Jenkinsfile)
can instead pull straight from nuget.org via `dotnet tool update --add-source
https://api.nuget.org/v3/index.json`, bypassing the restricted default
source without needing to mirror the tool here. Prefer that for anything
that's a build dependency rather than something the shipped product
references.

## History

This repo previously repackaged legacy Office primary-interop-assemblies
(PIAs) so they could be consumed via `PackageReference`. That Office Interop
dependency was dropped project-wide, so that packaging content is gone.
