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
   Packages feed automatically, using the workflow's own `GITHUB_TOKEN`
   (no PAT needed, nothing to run locally). Already-published id+version
   pairs are skipped safely (`--skip-duplicate`).

`.nupkg` files under `feed/` are committed on purpose — this repo *is* the
audit trail of what's been mirrored and when.

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
