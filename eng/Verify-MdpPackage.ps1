#requires -Version 7.3

param(
    [Parameter(Mandatory = $true)]
    [string] $TaskPackage,
    [Parameter(Mandatory = $true)]
    [string] $CliPackage,
    [Parameter(Mandatory = $true)]
    [string[]] $MSBuild,
    [string] $Fixture = "$PSScriptRoot\..\MetadataProcessor.Tests\TestNFApp\bin\Release\TestNFApp.exe",
    [string] $StubsFixture = "$PSScriptRoot\..\MetadataProcessor.Tests\StubsGenerationTestNFApp\bin\Release\StubsGenerationTestNFApp.exe"
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

function Assert-SameFile([string] $Expected, [string] $Actual) {
    Assert-File $Expected
    Assert-File $Actual
    if ((Get-FileHash -LiteralPath $Expected).Hash -ne (Get-FileHash -LiteralPath $Actual).Hash) {
        throw "Packaged hosts produced different output: $Expected and $Actual"
    }
}

function Assert-SamePdbx([string] $Expected, [string] $Actual) {
    $expectedJson = Get-Content -LiteralPath $Expected -Raw | ConvertFrom-Json | ConvertTo-Json -Depth 100
    $actualJson = Get-Content -LiteralPath $Actual -Raw | ConvertFrom-Json | ConvertTo-Json -Depth 100
    # Enum.ToString can choose either name for the same NanoCLRDataType byte value.
    $expectedJson = $expectedJson -replace '"PrimitiveType": "DATATYPE_LAST_PRIMITIVE"', '"PrimitiveType": "DATATYPE_STRING"'
    $actualJson = $actualJson -replace '"PrimitiveType": "DATATYPE_LAST_PRIMITIVE"', '"PrimitiveType": "DATATYPE_STRING"'
    $expectedJson = $expectedJson -replace '"PrimitiveType": "DATATYPE_LAST_PRIMITIVE_TO_PRESERVE"', '"PrimitiveType": "DATATYPE_R8"'
    $actualJson = $actualJson -replace '"PrimitiveType": "DATATYPE_LAST_PRIMITIVE_TO_PRESERVE"', '"PrimitiveType": "DATATYPE_R8"'
    if ($expectedJson -cne $actualJson) {
        throw "Packaged hosts produced different PDBX: $Expected and $Actual"
    }
}

try {
    Assert-File $Fixture
    Assert-File $StubsFixture
    foreach ($fixtureVariable in @('Fixture', 'StubsFixture')) {
        $source = (Resolve-Path (Get-Variable $fixtureVariable -ValueOnly)).Path
        $snapshot = Join-Path $work $fixtureVariable
        [IO.Directory]::CreateDirectory($snapshot) | Out-Null
        Copy-Item -Path (Join-Path (Split-Path $source) '*') -Destination $snapshot -Recurse
        Set-Variable $fixtureVariable (Join-Path $snapshot ([IO.Path]::GetFileName($source)))
    }
    $taskRoot = Join-Path $work 'task'
    $cliRoot = Join-Path $work 'cli'
    [IO.Compression.ZipFile]::ExtractToDirectory((Resolve-Path $TaskPackage).Path, $taskRoot)
    [IO.Compression.ZipFile]::ExtractToDirectory((Resolve-Path $CliPackage).Path, $cliRoot)

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

    $baseline = $null
    for ($index = 0; $index -lt $MSBuild.Count; $index++) {
        $hostPath = (Resolve-Path $MSBuild[$index]).Path
        $arguments = @()
        $executable = $hostPath
        if ([IO.Path]::GetExtension($hostPath) -eq '.dll') {
            $executable = 'dotnet'
            $arguments += $hostPath
        }
        $hostOutput = Join-Path $work "host-$index"
        foreach ($inputAssembly in @($Fixture, $StubsFixture)) {
            $inputAssembly = (Resolve-Path $inputAssembly).Path
            $name = [IO.Path]::GetFileNameWithoutExtension($inputAssembly)
            $generateStubs = $name -eq [IO.Path]::GetFileNameWithoutExtension($StubsFixture)
            $output = Join-Path $hostOutput $name
            & $executable @arguments "$PSScriptRoot\MdpPackageSmoke.proj" /t:VerifyParallel /m:2 /nologo /v:minimal /nr:false `
                "/p:PackageRoot=$taskRoot" "/p:Fixture=$inputAssembly" `
                "/p:FixtureDirectory=$([IO.Path]::GetDirectoryName($inputAssembly))" `
                "/p:OutputDirectory=$output" "/p:GenerateStubs=$generateStubs"
            if ($LASTEXITCODE -ne 0) {
                throw "Packaged task failed under $hostPath (exit $LASTEXITCODE)."
            }
            if ($generateStubs -and !(Get-ChildItem "$output\stubs" -Recurse -Filter '*.cpp')) {
                throw "The packaged task did not generate native stubs under $hostPath."
            }
            foreach ($parallelRun in @('parallel-one', 'parallel-two')) {
                Assert-SameFile "$output\Smoke.pe" "$output\$parallelRun\Smoke.pe"
                Assert-SamePdbx "$output\Smoke.pdbx" "$output\$parallelRun\Smoke.pdbx"
                Assert-SameFile "$output\native-contract.txt" "$output\$parallelRun\native-contract.txt"
            }
            if ($null -ne $baseline) {
                Assert-SameFile "$baseline\$name\Smoke.pe" "$output\Smoke.pe"
                Assert-SamePdbx "$baseline\$name\Smoke.pdbx" "$output\Smoke.pdbx"
                Assert-SameFile "$baseline\$name\native-contract.txt" "$output\native-contract.txt"
                if ($generateStubs) {
                    $expectedStubs = @(Get-ChildItem "$baseline\$name\stubs" -Recurse -File)
                    $actualStubs = @(Get-ChildItem "$output\stubs" -Recurse -File)
                    if ($expectedStubs.Count -ne $actualStubs.Count) {
                        throw "Native stub file counts differ under $hostPath."
                    }
                    foreach ($stub in $expectedStubs) {
                        $relative = $stub.FullName.Substring("$baseline\$name\stubs".Length)
                        Assert-SameFile $stub.FullName ("$output\stubs" + $relative)
                    }
                }
            }
        }
        if ($null -eq $baseline) {
            $baseline = $hostOutput
        }
        Write-Host "Packaged conversions passed: $hostPath"
    }

    $feed = Join-Path $work 'feed'
    [IO.Directory]::CreateDirectory($feed) | Out-Null
    Copy-Item -LiteralPath $CliPackage -Destination $feed
    [xml] $cliNuspec = Get-Content (Get-ChildItem $cliRoot -Filter '*.nuspec').FullName -Raw
    $consumerOutput = Join-Path $work 'consumer'
    dotnet build "$PSScriptRoot\CliPackageConsumer.csproj" --nologo -v:quiet -o $consumerOutput `
        "/p:CliPackageVersion=$($cliNuspec.package.metadata.version)" `
        "/p:RestoreSources=$feed" "/p:RestorePackagesPath=$work\packages" `
        "/p:BaseIntermediateOutputPath=$work\obj\"
    if ($LASTEXITCODE -ne 0) {
        throw "CLI PackageReference content-copy build failed (exit $LASTEXITCODE)."
    }

    $cliLayouts = @(
        (Join-Path $cliRoot 'content\MetadataProcessor'),
        (Join-Path $cliRoot 'contentFiles\any\any\MetadataProcessor'),
        (Join-Path $consumerOutput 'MetadataProcessor')
    )
    foreach ($cli in $cliLayouts) {
        $exe = Join-Path $cli 'nanoFramework.Tools.MetadataProcessor.exe'
        Assert-File $exe
        Assert-File "$exe.config"
        Assert-File "$cli\System.Text.Json.dll"
        [xml] $config = Get-Content "$exe.config" -Raw
        if (!$config.configuration.runtime.assemblyBinding.dependentAssembly) {
            throw 'The CLI must ship the generated executable binding redirects.'
        }
        foreach ($inputAssembly in @($Fixture, $StubsFixture)) {
            $name = [IO.Path]::GetFileNameWithoutExtension($inputAssembly)
            $outputDirectory = Join-Path $cli $name
            [IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
            $output = Join-Path $outputDirectory 'Smoke.pe'
            $cliArguments = @()
            foreach ($hint in Get-ChildItem (Split-Path $inputAssembly) -Filter '*.dll') {
                $cliArguments += @('-loadHints', $hint.BaseName, $hint.FullName)
            }
            $cliArguments += @('-parse', $inputAssembly, '-compile', $output, 'false')
            if ($inputAssembly -eq $StubsFixture) {
                $cliArguments += @('-generateskeleton', "$outputDirectory\stubs", 'PackageSmoke', 'PackageSmoke', 'false')
            }
            $cliArguments += @('-generateDependency', "$outputDirectory\Smoke.dependencies.xml")
            & $exe @cliArguments
            if ($LASTEXITCODE -ne 0) {
                throw "Packaged console failed (exit $LASTEXITCODE)."
            }
            Assert-SameFile "$baseline\$name\Smoke.pe" $output
            Assert-SamePdbx "$baseline\$name\Smoke.pdbx" ([IO.Path]::ChangeExtension($output, 'pdbx'))
            Assert-File "$outputDirectory\Smoke.dependencies.xml"
            [xml] (Get-Content "$outputDirectory\Smoke.dependencies.xml" -Raw) | Out-Null
            if ($inputAssembly -eq $StubsFixture) {
                foreach ($stub in Get-ChildItem "$baseline\$name\stubs" -Recurse -File) {
                    $relative = $stub.FullName.Substring("$baseline\$name\stubs".Length)
                    Assert-SameFile $stub.FullName ("$outputDirectory\stubs" + $relative)
                }
            }
        }
        $failure = Start-Process -FilePath $exe -ArgumentList "-parse `"$work\missing.dll`"" `
            -Wait -PassThru -RedirectStandardOutput "$work\failure.stdout" -RedirectStandardError "$work\failure.stderr"
        if ($failure.ExitCode -ne 1 -or (Get-Content "$work\failure.stderr" -Raw) -notmatch 'Unable to parse input assembly') {
            throw 'The packaged console did not preserve its missing-input diagnostic and failure exit code.'
        }
    }
    Write-Host 'Task and console package qualification passed.'
}
finally {
    Remove-Item -LiteralPath $work -Recurse -Force
}
