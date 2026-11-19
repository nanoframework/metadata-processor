#requires -Version 7.3

# Lightweight qualification of the packed MDP task and console packages.
param(
    [Parameter(Mandatory = $true)]
    [string] $TaskPackage,
    [Parameter(Mandatory = $true)]
    [string] $CliPackage,
    [Parameter(Mandatory = $true)]
    [string[]] $MSBuild,
    [string] $Fixture = "$PSScriptRoot\..\MetadataProcessor.Tests\TestNFApp\bin\Release\TestNFApp.exe"
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$work = Join-Path ([IO.Path]::GetTempPath()) ("mdp package " + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($work) | Out-Null

function Assert-File([string] $Path) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required package file or generated output is missing: $Path"
    }
}

try {
    $Fixture = (Resolve-Path $Fixture).Path
    $taskRoot = Join-Path $work 'task'
    $cliRoot = Join-Path $work 'cli'
    [IO.Compression.ZipFile]::ExtractToDirectory((Resolve-Path $TaskPackage).Path, $taskRoot)
    [IO.Compression.ZipFile]::ExtractToDirectory((Resolve-Path $CliPackage).Path, $cliRoot)

    # Package layout: complete private payloads, no MSBuild host assemblies, no lib assets.
    foreach ($tfm in @('net472', 'net10.0')) {
        $payload = Join-Path $taskRoot "tasks\$tfm"
        Assert-File "$payload\nanoFramework.Tools.MetadataProcessor.MsBuildTask.dll"
        Assert-File "$payload\Mono.Cecil.dll"
        Assert-File "$payload\mustache-sharp.dll"
        if (Get-ChildItem -LiteralPath $payload -Recurse -Filter 'Microsoft.Build*.dll') {
            throw "MSBuild host assemblies must not be shipped in $payload"
        }
    }
    if (Test-Path "$taskRoot\lib") {
        throw 'Task assemblies must not be exposed as application library assets.'
    }
    Assert-File "$taskRoot\tasks\net472\System.Text.Json.dll"
    Assert-File "$taskRoot\tasks\net472\System.Text.Encodings.Web.dll"
    Assert-File "$taskRoot\tasks\net10.0\nanoFramework.Tools.MetadataProcessor.MsBuildTask.deps.json"
    Assert-File "$taskRoot\build\nanoFramework.Tools.MetadataProcessor.MsBuildTask.targets"
    [xml] $nuspec = Get-Content (Get-ChildItem $taskRoot -Filter '*.nuspec').FullName -Raw
    if ($nuspec.package.metadata.dependencies.ChildNodes.Count -gt 0) {
        throw 'Tool payload dependencies must not flow into consumer projects.'
    }
    if ($nuspec.package.metadata.developmentDependency -ne 'true') {
        throw 'The task package must be a development dependency.'
    }

    # One real conversion per host.
    foreach ($hostArg in $MSBuild) {
        $hostPath = (Resolve-Path $hostArg).Path
        $arguments = @()
        $executable = $hostPath
        if ([IO.Path]::GetExtension($hostPath) -eq '.dll') {
            $executable = 'dotnet'
            $arguments += $hostPath
        }
        $output = Join-Path $work ("host-" + [Guid]::NewGuid().ToString('N'))
        & $executable @arguments "$PSScriptRoot\MdpPackageSmoke.proj" /nologo /v:minimal /nr:false `
            "/p:PackageRoot=$taskRoot" "/p:Fixture=$Fixture" `
            "/p:FixtureDirectory=$([IO.Path]::GetDirectoryName($Fixture))" "/p:OutputDirectory=$output"
        if ($LASTEXITCODE -ne 0) {
            throw "Packaged task failed under $hostPath (exit $LASTEXITCODE)."
        }
        Write-Host "Packaged conversion passed: $hostPath"
    }

    # Console: run a conversion from the content layout.
    $cli = Join-Path $cliRoot 'content\MetadataProcessor'
    $exe = Join-Path $cli 'nanoFramework.Tools.MetadataProcessor.exe'
    Assert-File $exe
    Assert-File "$exe.config"
    Assert-File "$cli\System.Text.Json.dll"
    Assert-File (Join-Path $cliRoot 'contentFiles\any\any\MetadataProcessor\nanoFramework.Tools.MetadataProcessor.exe')
    $cliOutput = Join-Path $work 'cli-output'
    [IO.Directory]::CreateDirectory($cliOutput) | Out-Null
    $cliArguments = @()
    foreach ($hint in Get-ChildItem (Split-Path $Fixture) -Filter '*.dll') {
        $cliArguments += @('-loadHints', $hint.BaseName, $hint.FullName)
    }
    $cliArguments += @('-parse', $Fixture, '-compile', "$cliOutput\Smoke.pe", 'false')
    & $exe @cliArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Packaged console failed (exit $LASTEXITCODE)."
    }
    Assert-File "$cliOutput\Smoke.pe"
    Assert-File "$cliOutput\Smoke.pdbx"

    Write-Host 'Task and console package qualification passed.'
}
finally {
    Remove-Item -LiteralPath $work -Recurse -Force
}
