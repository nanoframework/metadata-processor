# Copyright (c) .NET Foundation and Contributors
# See LICENSE file in the project root for full license information.

"Updating dependency at nf-Visual-Studio-extension" | Write-Host

# compute authorization header in format "AUTHORIZATION: basic 'encoded token'"
# 'encoded token' is the Base64 of the string "nfbot:personal-token"
$auth = "basic $([System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("nfbot:$env:MY_GITHUB_TOKEN")))"

# init/reset these
$commitMessage = ""
$prTitle = ""
$newBranchName = "develop-nfbot/update-dependencies/" + [guid]::NewGuid().ToString()
$packageTargetVersion = gh release view --json tagName --jq .tagName
$packageTargetVersion = $packageTargetVersion -replace "v"
$packageName = "nanoframework.tools.metadataprocessor.msbuildtask"
$repoMainBranch = "main"

# working directory is agent temp directory
Write-Debug "Changing working directory to $env:Agent_TempDirectory"
Set-Location "$env:Agent_TempDirectory" | Out-Null

# clone repo and checkout
Write-Debug "Init and featch nf-Visual-Studio-extension repo"

git clone --depth 1 https://github.com/nanoframework/nf-Visual-Studio-extension repo
Set-Location repo | Out-Null
git config --global gc.auto 0
git config --global user.name nfbot
git config --global user.email nanoframework@outlook.com
git config --global core.autocrlf true

Write-Host "Checkout $repoMainBranch branch..."
git checkout --quiet $repoMainBranch | Out-Null

# check if nuget package is already available from nuget.org
$nugetApiUrl = "https://api.nuget.org/v3-flatcontainer/$packageName/index.json"

function Get-LatestNugetVersion {
    param (
        [string]$url
    )
    try {
        $response = Invoke-RestMethod -Uri $url -Method Get
        return $response.versions[-1]
    }
    catch {
        throw "Error querying NuGet API: $_"
    }
}

$latestNugetVersion = Get-LatestNugetVersion -url $nugetApiUrl

while ($latestNugetVersion -ne $packageTargetVersion) {
    Write-Host "Latest version still not available from nuget.org feed. Waiting 5 minutes..."
    Start-Sleep -Seconds 300
    $latestNugetVersion = Get-LatestNugetVersion -url $nugetApiUrl
}

Write-Host "Version $latestNugetVersion available from nuget.org feed. Proceeding with update."

####################
# VS 2019 & 2022

"*****************************************************************************************************" | Write-Host
"Updating nanoFramework.Tools.MetadataProcessor.MsBuildTask.Net package in VS2019 & VS2022 solution..." | Write-Host

dotnet remove VisualStudio.Extension-2019/VisualStudio.Extension-vs2019.csproj package nanoFramework.Tools.MetadataProcessor.MsBuildTask
dotnet add VisualStudio.Extension-2019/VisualStudio.Extension-vs2019.csproj package nanoFramework.Tools.MetadataProcessor.MsBuildTask --version $packageTargetVersion --no-restore 
dotnet remove VisualStudio.Extension-2022/VisualStudio.Extension-vs2022.csproj package nanoFramework.Tools.MetadataProcessor.MsBuildTask
dotnet add VisualStudio.Extension-2022/VisualStudio.Extension-vs2022.csproj package nanoFramework.Tools.MetadataProcessor.MsBuildTask --version $packageTargetVersion --no-restore 
nuget restore -uselockfile

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
    $prRequestBody = @{title = "$prTitle"; body = "$commitMessage"; head = "$newBranchName"; base = "$repoMainBranch" } | ConvertTo-Json
    $githubApiEndpoint = "https://api.github.com/repos/nanoframework/nf-Visual-Studio-extension/pulls"
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

    $headers = @{}
    $headers.Add("Authorization", "$auth")
    $headers.Add("Accept", "application/vnd.github.symmetra-preview+json")

    try {
        $result = Invoke-RestMethod -Method Post -UserAgent [Microsoft.PowerShell.Commands.PSUserAgent]::InternetExplorer -Uri  $githubApiEndpoint -Header $headers -ContentType "application/json" -Body $prRequestBody
        'Started PR with dependencies update...' | Write-Host -NoNewline
        'OK' | Write-Host -ForegroundColor Green
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
