# Test Fixtures

## CsharpDuplicateDetector Test Fixtures

- `src\CsharpDuplicateDetector.Tests\CommandLineTests.cs` runs the detector against a real-world solution (`CommonSlnPath`, currently `C:\GitHub\test\Automation.sln`) and compares output against `src\CsharpDuplicateDetector.Tests\Expected\duplicates.csv`/`duplicates.json`.
- These Expected fixtures are tied to the exact line numbers/method bodies in that external solution, so they go stale whenever `Automation.sln`'s source changes. Regenerate them with:
  ```
  cd src\CsharpDuplicateDetector.Tests
  dotnet test -c Release --filter "Main_OnCommonSln_ExitsSuccessfullyAndWritesCsvAndJson"
  copy bin\Release\net8.0\Output\duplicates.csv Expected\duplicates.csv
  copy bin\Release\net8.0\Output\duplicates.json Expected\duplicates.json
  dotnet test -c Release --filter "Main_OnCommonSln_ExitsSuccessfullyAndWritesCsvAndJson"
  ```
- Always regenerate via `dotnet test` (in-process `Program.Main`), not the standalone `dotnet-tools\csharp-duplicate-detector.exe` — that prebuilt exe can lag behind the current source of `CsharpDuplicateDetector` and produce mismatched output.
- To regenerate straight into `Expected` using the standalone exe instead (only after rebuilding/republishing the exe so it matches current source):
  ```
  C:\GitHub\nuget\dotnet-tools\csharp-duplicate-detector.exe --min-tokens 100 C:\GitHub\test C:\GitHub\nuget\src\CsharpDuplicateDetector.Tests\Expected
  ```

## SltCsharpMetrics Test Fixtures

- `src\SltCsharpMetrics.Tests\CommandLineTests.cs` runs the tool against the same real-world solution (`CommonSlnPath`, currently `C:\GitHub\test\Automation.sln`) and compares output against `src\SltCsharpMetrics.Tests\Expected\metrics.xml`.
- `C:\GitHub\test\Automation.sln` restores packages from a private GitHub Packages feed (`C:\GitHub\test\NuGet.Config`), which needs a valid `GITHUB_PACKAGES_TOKEN` env var (`read:packages` scope on `Ter-jeff`). Without it, restore fails with 401s on several projects (`CommonLib`, `MockLib`, `LcdLib`, etc.), silently lowering the computed metrics (e.g. `ClassCoupling`) instead of failing outright — always confirm `dotnet restore C:\GitHub\test\Automation.sln` is clean (no `NU1301`/401 warnings) before trusting a regenerated fixture.
- Always regenerate via `dotnet test` (in-process `Program.Main`), not the standalone `dotnet-tools\slt-csharp-metrics.exe` — same in-process-vs-prebuilt-exe caveat as `CsharpDuplicateDetector` above. Regenerate with:
  ```
  set GITHUB_PACKAGES_TOKEN=<token>
  cd src\SltCsharpMetrics.Tests
  dotnet test -c Release --filter "Main_OnCommonSln_ExitsSuccessfullyAndWritesMetricsReport"
  copy bin\Release\net8.0\Output\metrics.xml Expected\metrics.xml
  dotnet test -c Release --filter "Main_OnCommonSln_ExitsSuccessfullyAndWritesMetricsReport"
  ```
- To run the standalone exe instead (only after rebuilding/republishing so it matches current source): its args start with `/`, which Git Bash mangles into a path (`/solution:...` gets rewritten and the exe just prints usage) — run this from PowerShell, not Git Bash:
  ```
  $env:GITHUB_PACKAGES_TOKEN = "<token>"
  C:\GitHub\nuget\dotnet-tools\slt-csharp-metrics.exe /solution:C:\GitHub\test\Automation.sln /out:C:\GitHub\nuget\src\SltCsharpMetrics.Tests\Expected\metrics.xml /quiet
  ```
