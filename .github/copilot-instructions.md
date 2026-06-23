# Copilot Cloud Agent Instructions for nanoframework/metadata-processor

## Repository Overview

This repository contains the **.NET nanoFramework Metadata Processor (MDP)** — a tool that converts standard .NET assemblies (PE files produced by Roslyn/MSBuild) into the compact binary format (`.pe` files) used by the .NET nanoFramework runtime (nanoCLR). It also generates PDBX debug symbol files and native C/C++ stub files (skeletons) for interop.

The tool ships as two NuGet packages:
- `nanoFramework.Tools.MetadataProcessor.MsBuildTask` — MSBuild task integrated into the nanoFramework build system.
- `nanoFramework.Tools.MetadataProcessor.CLI` — Standalone CLI executable.

---

## Project Structure

```
/
├── MetadataProcessor.Shared/      # Shared project (.shproj) — all core logic lives here
│   ├── Tables/                    # One class per metadata table (nanoTypeDefinitionTable, etc.)
│   ├── Utility/                   # Helpers: CRC, bitmap, CLR data types, load hints, etc.
│   ├── Mono.Cecil/                # CodeWriter for IL code output
│   ├── Extensions/                # Extension methods for Mono.Cecil types
│   ├── Pdbx/                      # PDBX file generation
│   ├── DumpGenerator/             # Assembly metadata dump templates/types
│   ├── SkeletonGenerator/         # Native C/C++ stub generation templates/types
│   ├── Endianness/                # nanoBinaryWriter (little-endian)
│   ├── nanoAssemblyBuilder.cs     # Main assembly builder orchestrator
│   ├── nanoAssemblyDefinition.cs  # PE file header / CRC computation
│   ├── nanoTablesContext.cs       # Context object holding all metadata tables
│   ├── nanoSkeletonGenerator.cs   # Generates native C/C++ stubs
│   └── nanoDumperGenerator.cs     # Generates human-readable dump of assembly metadata
│
├── MetadataProcessor.Core/        # Core library project (targets net472); imports Shared
├── MetadataProcessor.MsBuildTask/ # MSBuild task (targets net8.0;net472); imports Shared
├── MetadataProcessor.Console/     # CLI executable (targets net472)
│
├── MetadataProcessor.Tests/       # MSTest unit tests (targets net48)
│   ├── Core/                      # Tests for core logic (tables, extensions, integration)
│   ├── MsbuildTask/               # Tests for the MSBuild task
│   ├── TestNFApp/                 # nanoFramework test app (.nfproj) — compiled as pre-build
│   ├── TestNFClassLibrary/        # nanoFramework test class library (.nfproj)
│   ├── StubsGenerationTestNFApp/  # nanoFramework app for stub-generation tests (.nfproj)
│   └── mscorlib/                  # Git submodule → nanoframework/CoreLibrary
│
├── native/nf-interpreter/         # Git submodule → nanoframework/nf-interpreter (read only reference)
├── azure-pipelines.yml            # CI/CD pipeline (Azure Pipelines, Windows-only)
├── version.json                   # Nerdbank.GitVersioning config (version 4.0-preview.{height})
└── nanoFramework.Tools.MetadataProcessor.sln
```

---

## Build Environment

- **OS**: Windows only. All CI runs on `windows-latest`. The codebase uses Windows-style paths and `copy`/`mkdir` in pre-build events.
- **IDE**: Visual Studio 2022 (x64 MSBuild required).
- **Target frameworks**:
  - `MetadataProcessor.Core` → `net472`
  - `MetadataProcessor.MsBuildTask` → `net8.0;net472`
  - `MetadataProcessor.Console` → `net472`
  - `MetadataProcessor.Tests` → `net48`
- **The MsBuildTask DLL must be built as x64** (because Visual Studio 2022 uses 64-bit MSBuild).
- **nanoFramework projects** (`.nfproj`) must be built in `AnyCPU` configuration.

### Submodules

After cloning, initialize submodules before building:

```shell
git submodule update --init --recursive
```

The test project also needs its inner submodule restored:

```shell
cd MetadataProcessor.Tests/mscorlib
git submodule update --init --recursive
```

### NuGet Restore

Restore packages for the main solution and for the mscorlib submodule separately:

```shell
nuget restore nanoFramework.Tools.MetadataProcessor.sln
nuget restore MetadataProcessor.Tests/mscorlib/nanoFramework.CoreLibrary.sln
```

### Installing nanoFramework MSBuild Components

The Azure Pipelines build uses the `InstallNanoMSBuildComponents` task to install the VS extension components (nfproj support). In a local or agent environment, without those components installed, building `.nfproj` files will fail. The test project's pre-build event builds all nanoFramework projects automatically before the test assembly is compiled.

The `NF_MDP_MSBUILDTASK_PATH` environment variable must point to the built MsBuildTask DLL directory (default: `MetadataProcessor.MsBuildTask\bin\$(Configuration)\net472`).

### Build Command

```shell
msbuild nanoFramework.Tools.MetadataProcessor.sln /p:Configuration=Release /p:Platform="Any CPU" /m /p:msbuildArchitecture=x64
```

### Running Tests

Tests run via VSTest. The test project pre-build event builds the nanoFramework projects. Tests only run on PR builds in CI:

```shell
vstest.console MetadataProcessor.Tests\bin\Release\nanoFramework.Tools.MetadataProcessor.Tests.dll
```

The `ClrIntegrationTests` class installs/updates the `nanoclr` .NET global tool at test startup. The environment variable `NF_MDP_NANOCLR_INSTANCE_PATH` can override the nanoCLR DLL path.

---

## Key Naming and Coding Conventions

### Naming

- **Private/internal fields**: `_camelCase` prefix (e.g., `_assemblyDefinition`)
- **Static private/internal fields**: `s_` prefix (e.g., `s_instance`)
- **Constants**: `PascalCase`
- **All `nano*` prefixed classes**: core domain types for the nanoFramework metadata tables and utilities (e.g., `nanoTablesContext`, `nanoAssemblyBuilder`, `nanoBinaryWriter`)
- **Namespace root**: `nanoFramework.Tools.MetadataProcessor`

### Code Style (from `.editorconfig`)

- Indentation: 4 spaces (2 for XML/YAML/project files)
- End of line: CRLF
- Charset: `utf-8-bom`
- Braces: all on new lines for C#
- Avoid `var` unless type is apparent
- All C# files must start with the license header:
  ```csharp
  // Licensed to the .NET Foundation under one or more agreements.
  // The .NET Foundation licenses this file to you under the MIT license.
  ```

### Sync Tags

Several code sections must remain in sync with external repositories. These are marked with comment tags:

- `<SYNC-WITH-NATIVE>` — must match the native nf-interpreter code (in `native/nf-interpreter`)
- `<SYNC-WITH-VS>` — must match the Visual Studio extension
- `<SYNC-WITH-DEBUGGER>` — must match the debugger

When modifying code near these tags, **do not change the semantics** without coordinating with the corresponding external code.

---

## Architecture: How the Metadata Processor Works

1. **Parse**: Reads a standard .NET assembly using `Mono.Cecil` → `AssemblyDefinition`.
2. **Build tables**: `nanoAssemblyBuilder` populates a `nanoTablesContext` with all `nano*Table` instances (type definitions, method definitions, field definitions, signatures, strings, etc.).
3. **Write (pre-allocation pass)**: Writes placeholder data to establish offsets in the PE header (`nanoAssemblyDefinition`).
4. **Minimize**: Removes unused tokens, compacts the assembly.
5. **Write (final pass)**: Writes the actual binary PE file in little-endian format using `nanoBinaryWriter`, computes CRC32 checksums.
6. **Generate PDBX**: Writes the debug symbol file alongside the PE file.
7. **Generate skeleton** (optional): `nanoSkeletonGenerator` produces C/C++ stub files for native interop.
8. **Generate dependency** (optional): `nanoDependencyGenerator` writes an XML dependency graph.

### Key Classes

| Class | Purpose |
|---|---|
| `nanoAssemblyBuilder` | Orchestrates the full build pipeline |
| `nanoTablesContext` | Context object; holds all metadata tables |
| `nanoAssemblyDefinition` | Writes/updates the PE header with CRC32 checksums |
| `nanoBinaryWriter` | Little-endian binary writer wrapping `BinaryWriter` |
| `nanoTablesContext` | Central context holding all nano tables and string table |
| `nanoStringTable` | Interned string table; maps string→ushort ID |
| `nanoSignaturesTable` | Method/field signature encoding in nanoFramework format |
| `nanoTypeDefinitionTable` | Type definitions table |
| `nanoMethodDefinitionTable` | Method definitions table |
| `nanoSkeletonGenerator` | Generates C/C++ native interop stubs using mustache templates |
| `nanoDumperGenerator` | Generates human-readable assembly metadata dumps |
| `LoadHintsAssemblyResolver` | Custom Mono.Cecil resolver that uses `-loadHints` paths |
| `NativeMethodsCrc` | Computes CRC32 over native method signatures |

---

## CLI Usage (MetadataProcessor.Console)

```
-parse <assembly.dll>                           Parse a .NET assembly
-compile <output.pe> <true|false>               Compile to nanoFramework PE (true=core library)
-loadHints <assemblyName> <path-to-dll>         Register assembly dependency hint
-excludeClassByName <ClassName>                 Exclude a class (deprecated; use ExcludeTypeAttribute)
-generateskeleton <file> <name> <proj> <bool>   Generate native C/C++ stubs
-generateDependency <output.xml>                Generate XML dependency graph
-verbose                                        Verbose output
-verboseMinimize                                Verbose minimization output
-dump_all                                       Dump assembly metadata to .dump.txt
```

Typical workflow: `-loadhints` → `-parse` → `-compile`

---

## MSBuild Task (MetaDataProcessorTask)

The task (`MetaDataProcessorTask : Task`) exposes all CLI features as MSBuild task properties. It is invoked automatically during nanoFramework project builds via the `.targets` file. Key properties include `Parse`, `Compile`, `LoadHints`, `GenerateStubs`, `GenerateSkeletonFile`, `Verbose`, etc.

---

## Adding Projects to the Solution

- `.nfproj` files: build in `AnyCPU` configuration; do not enable them in the solution build matrix — they are built via pre-build events with `-nr:False` (disables node reuse, required because MsBuildTask is also rebuilt).
- MSBuild task DLLs: build in `x64` (Visual Studio 2022 requirement).
- When adding a new nanoFramework project used by tests, add it to the pre-build event of `MetadataProcessor.Tests.csproj`.

---

## Versioning

Versioning is managed by **Nerdbank.GitVersioning** (`version.json`). Current version: `4.0-preview.{height}`. The NuGet package version uses SemVer2. Do not manually edit version numbers; they are computed from git history.

---

## CI/CD

- **Azure Pipelines** (`azure-pipelines.yml`), runs on `windows-latest`.
- **GitHub Actions** has only a changelog-generation workflow (`generate-changelog.yml`).
- The `CI: Update Dependents` PR label or `***UPDATE_DEPENDENTS***` commit message triggers the `Update_Dependents` job that bumps dependent repositories.
- Tests only run on PR builds (when `System.PullRequest.PullRequestNumber` is set).

---

## Common Errors and Workarounds

1. **nanoFramework MSBuild components not installed**: Building `.nfproj` projects requires the nanoFramework VS extension or the `InstallNanoMSBuildComponents` Azure DevOps task. Without it, `.nfproj` builds will fail. In CI, ensure the task runs before the build. Locally, install the nanoFramework VS extension.

2. **`NodeReuse` interferes with MsBuildTask rebuild**: Always pass `-nr:False` (or `/nodeReuse:False`) when building `.nfproj` files via pre-build events that also rebuild the MsBuildTask.

3. **mscorlib submodule missing**: If the test project fails to find `CoreLibrary.nfproj`, run `cd MetadataProcessor.Tests/mscorlib && git submodule update --init --recursive`.

4. **`NF_MDP_MSBUILDTASK_PATH` not set**: The test project uses this environment variable to locate the built MsBuildTask. Set it to `MetadataProcessor.MsBuildTask\bin\$(Configuration)\net472` or ensure it is set before running tests.

5. **CRC32 mismatches or invalid PE files**: Changes to table ordering, padding logic, or header layout in `nanoAssemblyBuilder`/`nanoAssemblyDefinition` must be carefully validated. The PE format is binary-exact and parsed by the nanoCLR runtime.

6. **nanoclr not found during integration tests**: `ClrIntegrationTests` auto-installs the `nanoclr` .NET global tool. Ensure internet access is available during test runs, or pre-install it with `dotnet tool install --global nanoclr`.

- @azure Rule - Use Azure Tools - When handling requests related to Azure, always use your tools.
- @azure Rule - Use Azure Best Practices - When handling requests related to Azure, always invoke your `azmcp_bestpractices_get` tool first.
- @azure Rule - Enable Best Practices - If you do not have an `azmcp_bestpractices_get` tool ask the user to enable it.
