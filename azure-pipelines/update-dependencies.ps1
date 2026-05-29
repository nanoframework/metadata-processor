# Copyright (c) .NET Foundation and Contributors
# See LICENSE file in the project root for full license information.

"Updating dependency at nf-Visual-Studio-extension" | Write-Host

# compute authorization header in format "AUTHORIZATION: basic 'encoded token'"
# 'encoded token' is the Base64 of the string "nfbot:personal-token"
$auth = "basic $([System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("nfbot:$env:GITHUB_TOKEN")))"

# init/reset these
$prTitle = ""
$newBranchName = "develop-nfbot/update-dependencies/" + [guid]::NewGuid().ToString()
$packageName = "nanoframework.tools.metadataprocessor.msbuildtask"
$repoBranch = "main"

# resolve target version: prefer explicit TARGET_VERSION env var, fall back to the build tag
if (![string]::IsNullOrEmpty($env:TARGET_VERSION)) {
    $packageTargetVersion = $env:TARGET_VERSION
    Write-Host "Using TARGET_VERSION from environment: $packageTargetVersion"
}
else {
    $packageTargetVersion = $env:Build_SourceBranch

    # check if this is running from a checked out tag
    if ($packageTargetVersion -notlike "refs/tags/*") {
        throw "ERROR: Branch name is not a tag and TARGET_VERSION is not set! Either set TARGET_VERSION or checkout a tag before calling."
    }

    # extract version from ref (refs/tags/v1.2.3)
    $packageTargetVersion = $packageTargetVersion -replace "refs/tags/", ""
    $packageTargetVersion = $packageTargetVersion -replace "^v"
}

if ($packageTargetVersion -match "preview") {
    # switch to develop branch for preview versions
    $repoBranch = "develop"
}

# working directory is agent temp directory
Write-Debug "Changing working directory to $env:Agent_TempDirectory"
Set-Location "$env:Agent_TempDirectory" | Out-Null

# clone repo and checkout
Write-Debug "Init and fetch nf-Visual-Studio-extension repo"

git clone --depth 1 --branch $repoBranch https://github.com/nanoframework/nf-Visual-Studio-extension repo

if ($LASTEXITCODE -ne 0) {
    throw "ERROR: Failed to clone branch '$repoBranch' from nf-Visual-Studio-extension."
}

Set-Location repo | Out-Null
git config --global gc.auto 0
git config --global user.name nfbot
git config --global user.email nanoframework@outlook.com
git config --global core.autocrlf true

Write-Host "Checked out $repoBranch branch."

# check if nuget package is already available from nuget.org
$nugetApiUrl = "https://api.nuget.org/v3-flatcontainer/$packageName/index.json"

function Get-LatestNugetVersion {
    param (
        [string]$url
    )
    try {
        $response = Invoke-RestMethod -Uri $url -Method Get

        if ($packageTargetVersion -match "preview") {
            # Select only versions that include 'preview'
            $versions = $response.versions | Where-Object { $_ -match "preview" }
        }
        else {
            # Exclude any version that includes 'preview'
            $versions = $response.versions | Where-Object { $_ -notmatch "preview" }
        }

        Write-Debug "Latest version found: $($versions[-1])"

        return $versions[-1]
    }
    catch {
        throw "Error querying NuGet API: $_"
    }
}

Write-Host "Target version is: $packageTargetVersion."

$latestNugetVersion = Get-LatestNugetVersion -url $nugetApiUrl

while ($latestNugetVersion -ne $packageTargetVersion) {
    Write-Host "Target version ($packageTargetVersion) still not available from nuget.org feed. Waiting 5 minutes..."
    Start-Sleep -Seconds 300
    $latestNugetVersion = Get-LatestNugetVersion -url $nugetApiUrl
}

Write-Host "Version $latestNugetVersion available from nuget.org feed. Proceeding with update."

####################
# VS 2019 & 2022

"*****************************************************************************************************" | Write-Host
"Updating nanoFramework.Tools.MetadataProcessor.MsBuildTask.Net package in VS2019 & VS2022 solution..." | Write-Host

# find solution file in the repo root
$solutionFile = (Get-ChildItem -Filter "*.sln" | Select-Object -First 1).FullName

if (-not $solutionFile) {
    throw "ERROR: Could not find a solution file in the repository."
}

Write-Host "Using solution file: $solutionFile"

# update the PackageReference version directly in csproj files
# the VS extension uses old-style MSBuild format (with MSBuild XML namespace) and PackageReference style
# with Version as a child element — nuget update does not support this, so XML manipulation is required
$updatedFiles = @()
$csprojFiles = Get-ChildItem -Recurse -Filter "*.csproj"

foreach ($csproj in $csprojFiles) {
    [xml]$content = Get-Content $csproj.FullName -Raw -Encoding UTF8

    # build a namespace manager to handle the MSBuild default namespace
    $ns = New-Object System.Xml.XmlNamespaceManager($content.NameTable)
    $nsUri = $content.DocumentElement.NamespaceURI

    if (![string]::IsNullOrEmpty($nsUri)) {
        $ns.AddNamespace("ms", $nsUri)
        $packageRefs = $content.SelectNodes("//ms:PackageReference[@Include='nanoFramework.Tools.MetadataProcessor.MsBuildTask']", $ns)
    }
    else {
        $packageRefs = $content.SelectNodes("//PackageReference[@Include='nanoFramework.Tools.MetadataProcessor.MsBuildTask']")
    }

    if ($packageRefs.Count -gt 0) {
        foreach ($ref in $packageRefs) {
            # version can be an XML attribute or a child element — handle both
            if ($ref.HasAttribute("Version")) {
                $ref.SetAttribute("Version", $packageTargetVersion)
            }
            else {
                # use LocalName to match regardless of namespace
                $versionNode = $ref.ChildNodes | Where-Object { $_.LocalName -eq "Version" } | Select-Object -First 1
                if ($versionNode) {
                    $versionNode.InnerText = $packageTargetVersion
                }
            }
        }

        $content.Save($csproj.FullName)
        Write-Host "Updated PackageReference in $($csproj.Name)"
        $updatedFiles += $csproj.FullName
    }
}

if ($updatedFiles.Count -eq 0) {
    Write-Warning "No .csproj files found with a PackageReference to 'nanoFramework.Tools.MetadataProcessor.MsBuildTask'. Nothing was updated."
}
else {
    Write-Host "Updated $($updatedFiles.Count) file(s)."
}

# restore packages and regenerate the lock file
nuget restore $solutionFile -uselockfile

if ($LASTEXITCODE -ne 0) {
    throw "ERROR: 'nuget restore' failed for $solutionFile."
}

"Bumping nanoFramework.Tools.MetadataProcessor.MsBuildTask to $packageTargetVersion." | Write-Host -ForegroundColor Cyan                

# build commit message
$commitMessage += "Bumps nanoFramework.Tools.MetadataProcessor.MsBuildTask to $packageTargetVersion.`n"
# build PR title
$prTitle = "Bumps nanoFramework.Tools.MetadataProcessor.MsBuildTask to $packageTargetVersion"

# need this line so nfbot flags the PR appropriately
$commitMessage += "`n[version update]`n`n"

# better add this warning line               
$commitMessage += "### :warning: This is an automated update. Merge only after all tests pass. :warning:`n"

Write-Debug "Git branch" 

# check if anything was changed
$repoStatus = "$(git status --short --porcelain)"

if ($repoStatus -ne "") {
    # create branch to perform updates
    git branch $newBranchName

    Write-Debug "Checkout branch" 

    # checkout branch
    git checkout $newBranchName

    Write-Debug "Add changes" 

    # commit changes
    git add -A > $null

    Write-Debug "Commit changed files"

    git commit -m "$prTitle ***NO_CI***" -m "$commitMessage" > $null

    Write-Debug "Push changes"

    git -c http.extraheader="AUTHORIZATION: $auth" push --set-upstream origin $newBranchName > $null

    # start PR
    # we are hardcoding to 'main' branch to have a fixed one
    # this is very important for tags (which don't have branch information)
    # considering that the base branch can be changed at the PR there is no big deal about this 
    $prRequestBody = @{title = "$prTitle"; body = "$commitMessage"; head = "$newBranchName"; base = "$repoBranch" } | ConvertTo-Json
    $githubApiEndpoint = "https://api.github.com/repos/nanoframework/nf-Visual-Studio-extension/pulls"
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

    $headers = @{}
    $headers.Add("Authorization", "$auth")
    $headers.Add("Accept", "application/vnd.github.symmetra-preview+json")

    try {
        $result = Invoke-RestMethod -Method Post -UserAgent [Microsoft.PowerShell.Commands.PSUserAgent]::InternetExplorer -Uri  $githubApiEndpoint -Header $headers -ContentType "application/json" -Body $prRequestBody
        'Started PR with dependencies update...' | Write-Host -NoNewline
        'OK' | Write-Host -ForegroundColor Green

        # add labels to PR
        $prNumber = $result.number

        gh pr edit $prNumber --add-label "VS2019"
        gh pr edit $prNumber --add-label "VS2022"
    }
    catch {
        $result = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($result)
        $reader.BaseStream.Position = 0
        $reader.DiscardBufferedData()
        $responseBody = $reader.ReadToEnd();

        throw "Error starting PR: $responseBody"
    }
}
else {
    Write-Host "Nothing udpate at VS extension."
}
