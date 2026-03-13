# Repository Guidelines

## Project Structure & Module Organization
`WorkflowCore.sln` is the solution entry point. The library lives in `src/WorkflowCore`, with core runtime code grouped by responsibility in `Services/`, `Models/`, `Primitives/`, `Exceptions/`, and `Interface/` (`Interface/Persistence/` for storage contracts). Integration tests live in `tests/WorkflowCore.IntegrationTests`, with scenario-style coverage under `Scenarios/`. Packaging assets are under `doc/nuget/`, and the root `README.md` is shipped with the NuGet package.

## Build, Test, and Development Commands
Use the .NET CLI from the repository root:

- `dotnet restore WorkflowCore.sln` restores dependencies.
- `dotnet build WorkflowCore.sln -c Debug` builds the library and test project.
- `dotnet test tests/WorkflowCore.IntegrationTests/WorkflowCore.IntegrationTests.csproj` runs the xUnit integration suite.
- `dotnet pack src/WorkflowCore/WorkflowCore.csproj -c Release` creates the package in `doc/nuget/`.

`src/WorkflowCore/WorkflowCore.csproj` has `GeneratePackageOnBuild=true`, so a normal build may also refresh the `.nupkg`.

## Coding Style & Naming Conventions
Follow the existing C# style: 4-space indentation, block-scoped namespaces, nullable reference types enabled, and `ImplicitUsings` turned on. Use `PascalCase` for public types and members, `_camelCase` for private fields, and prefix interfaces with `I`. Keep new runtime types in the matching folder (`Services`, `Primitives`, `Models`, etc.). Test files follow the existing scenario naming pattern such as `AttachScenario.cs`; workflow implementations commonly end in `Workflow`.

## Testing Guidelines
Tests use `xUnit` with `FluentAssertions`. Add new behavior coverage in `tests/WorkflowCore.IntegrationTests/Scenarios`, and prefer focused `[Fact]` tests that assert final workflow status and `UnhandledStepErrors.Count`. For faster iteration, run a subset with a filter such as `dotnet test --filter FullyQualifiedName~AttachScenario`. No coverage threshold is configured, so rely on scenario completeness.

## Commit & Pull Request Guidelines
Recent history mixes short descriptive subjects and Conventional Commit prefixes like `chore:`. Prefer concise, imperative commit subjects and use a prefix when it adds clarity (`fix:`, `docs:`, `chore:`). Pull requests should summarize the behavior change, list affected areas, include test evidence or explain why tests were not run, and link the related issue. Only include screenshots when changing documentation images or packaged assets.

## Configuration Notes
If restore unexpectedly points to a non-repository NuGet source, check user or machine NuGet configuration before changing project files. Treat files in `doc/nuget/` as packaging outputs or assets, not hand-edited source.
