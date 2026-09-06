# CsharpDuplicateDetector Test Fixtures

- `src\CsharpDuplicateDetector.Tests\CommandLineTests.cs` runs the detector against a real-world solution (`CommonSlnPath`, currently `C:\jeff\GitHub\test\Automation.sln`) and compares output against `src\CsharpDuplicateDetector.Tests\Expected\duplicates.csv`/`duplicates.json`.
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
  nuget\dotnet-tools\csharp-duplicate-detector.exe --min-tokens 100 C:\GitHub\test src\CsharpDuplicateDetector.Tests\Expected
  ```
