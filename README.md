# nuget

Local NuGet repackaging of legacy Office primary-interop-assemblies (PIAs) that
aren't available on nuget.org, so they can be consumed via `PackageReference`
with `EmbedInteropTypes` instead of file-based `<Reference HintPath=.../>`.

## Layout

- `vendor/interop/` — the original vendor-supplied interop DLLs.
- `src/<PackageId>/` — one packaging project per assembly. Each contains:
  - a `.csproj` that packs the DLL under a non-`lib` path (so NuGet doesn't
    auto-generate a plain, non-embedded reference for it), and
  - a `build/<PackageId>.targets` file, auto-imported by NuGet on restore,
    that adds the actual `<Reference>` with `EmbedInteropTypes=true`.

This two-part structure is necessary because `EmbedInteropTypes` set directly
on a `<PackageReference>` item is not honored by the .NET SDK — it only takes
effect on classic `<Reference>` items, so the package has to inject one itself.

- `feed/` — the built `.nupkg` files, **committed to git**. This *is* the feed:
  GitHub Packages isn't enabled on our GHE instance, so instead of a hosted
  registry, consumers point their `NuGet.config` at a local clone of this
  folder and `git pull` to pick up new/updated packages.

## Building the packages

```
dotnet pack src\Microsoft.Office.Interop.Excel -c Release -o feed
dotnet pack src\Microsoft.Office.Core -c Release -o feed
dotnet pack src\Microsoft.Vbe.Interop -c Release -o feed
dotnet pack src\Microsoft.Office.Interop.PowerPoint -c Release -o feed
```

Then commit and push the updated `feed/*.nupkg` files. Bump the `<Version>`
in the relevant `src/<PackageId>/*.csproj` first if the DLL itself changed,
since NuGet treats a given package id+version as immutable.

## Consuming this feed

Clone this repo as a sibling of the consuming project (e.g. both under
`C:\GitHub\`), then add a source pointing at `feed/` in the consumer's
`NuGet.config`:

```xml
<packageSources>
  <add key="nuget-feed" value="../nuget/feed" />
</packageSources>
```

`git pull` here whenever a package is updated, then `dotnet restore` in the
consuming project.

## If GitHub Packages is ever enabled

```
dotnet nuget add source --username <you> --password <PAT> --store-password-in-clear-text --name github "https://github.teradyne.com/_registry/nuget/jeff-li/index.json"
dotnet nuget push "feed\*.nupkg" --api-key <PAT> --source github
```

The PAT needs `read:packages` and `write:packages` scopes.
