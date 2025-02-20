[![License](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT) [![#yourfirstpr](https://img.shields.io/badge/first--timers--only-friendly-blue.svg)](https://github.com/nanoframework/Home/blob/main/CONTRIBUTING.md) [![Build Status](https://dev.azure.com/nanoframework/metadata-processor/_apis/build/status/nanoframework.metadata-processor?branchName=develop)](https://dev.azure.com/nanoframework/metadata-processor/_build/latest?definitionId=43&branchName=develop) [![Discord](https://img.shields.io/discord/478725473862549535.svg)](https://discord.gg/gCyBu8T)

![nanoFramework logo](https://raw.githubusercontent.com/nanoframework/Home/main/resources/logo/nanoFramework-repo-logo.png)

-----

### Welcome to the .NET **nanoFramework** metadata processor repository

This repo contains .NET **nanoFramework** metadata processor tool.
It's a console app that performs various tasks when running a build of a C# project, like parsing the PE file generated by Roslyn, generating the PDBX files, generate the stubs for native code.
Is part of .NET **nanoFramework** toolbox, along with other various tools that are required in .NET **nanoFramework** development, usage or repository management.

Version 2.0 is a C# application adapted from the original work of [Oleg Rakhmatulin](https://github.com/OlegRa).
Version 1.0 was a Visual C++ application adapted from .NETMF toolbox.

## Using the metadata processor

The metadata processor is available in two packages.

The main use of the metadata processor is packaged as a MSBuild task: [nanoFramework.Tools.MetadataProcessor.MsBuildTask](https://www.nuget.org/packages/nanoFramework.Tools.MetadataProcessor.MsBuildTask). The task is well integrated into the .NET **nanoFramework** build system and is distributed as part of the VS extensions. Almost all use cases can be addressed by the build system as it is, or by setting build variables for, e.g., additional logging.

There are few use cases where the build task cannot be used. E.g., in software that generates source code at runtime, compiles the code via Roslyn, executes it on the virtual device, and uses the output for further processing. The metadata processor is required as a companion to Roslyn to convert the generated .NET assemblies to .NET **nanoFramework** .pe assemblies. The second package, [nanoFramework.Tools.MetadataProcessor.CLI](https://www.nuget.org/packages/nanoFramework.Tools.MetadataProcessor.CLI), contains a CLI version of the tool for this purpose. It is packaged as content: if added to a project, the tool will be distributed with the results of that project. If the package is added to a non-SDK-style project (e.g., .NET Framework project), make sure to set the *Copy to Output Directory* property for the files, as the package manager does not automatically do that. The code in the project should run the CLI with arguments *-loadhints*, *-parse* and *-compile* (in that order!) to create the .pe assembly.

## Developers guide

### Cloning the repository

After cloning the repository, instantiate the submodules with:

```shell
git submodule update --init --recursive
```

After which the NuGet packages in both the main solution and the mscorelib solution included within a submodule have to be restored. These have to be restored individually.

### Adding projects to the solution

When adding a project to the solution the following points have to be kept in mind for configuring the project and solution:

- nanoFramework projects (.nfproj) have to be build in `AnyCPU` configuration.
- DLLs for msbuild tasks have to be build in `x64` because Visual Studio uses this architecture since VS2022.
- Any nanoFramework projects (.nfproj) that are required for Unit Tests have to have their build configuration changed so they don't build. Building those has to be added to the pre-build event of the Unit Test project that will be using it. See the [prebuild event](https://github.com/nanoframework/metadata-processor/blob/bcb82f6c9153cdc3863abf6fcc5b589437408b28/MetadataProcessor.Tests/MetadataProcessor.Tests.csproj#L118-L128) for the MetadataProcessor.Tests project. When adding nanoFramework projects to the pre-build event it is important to add the `-nr=False` flag<sup>[1]</sup>.

1. This flag disables "nodeReuse", this is needed as a custom MsBuildTask is used which also gets rebuilt. "NodeReuse" keeps instances of MsBuild running which interferes with the rebuilding of the custom MsBuildTask.

## Feedback and documentation

For documentation, providing feedback, issues and finding out how to contribute please refer to the [Home repo](https://github.com/nanoframework/Home).

Join our Discord community [here](https://discord.gg/gCyBu8T).

## Credits

The list of contributors to this project can be found at [CONTRIBUTORS](https://github.com/nanoframework/Home/blob/main/CONTRIBUTORS.md).

## License

The **nanoFramework** metadata processor tool is licensed under the [MIT license](LICENSE.md).

## Code of Conduct

This project has adopted the code of conduct defined by the Contributor Covenant to clarify expected behaviour in our community.
For more information see the [.NET Foundation Code of Conduct](https://dotnetfoundation.org/code-of-conduct).

### .NET Foundation

This project is supported by the [.NET Foundation](https://dotnetfoundation.org).
