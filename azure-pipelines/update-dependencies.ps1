"Updating dependency at nf-Visual-Studio-extension" | Write-Host

# compute authorization header in format "AUTHORIZATION: basic 'encoded token'"
# 'encoded token' is the Base64 of the string "nfbot:personal-token"
$auth = "basic $([System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("nfbot:$env:MY_GITHUB_TOKEN"))))"

# because it can take sometime for the package to become available on the NuGet providers
# need to hang here for 1 minute (1 * 60)
"Waiting 1 minute to let package process flow in Azure Artifacts feed..." | Write-Host
Start-Sleep -Seconds 60 

# init/reset these
$commitMessage = ""
$prTitle = ""
$newBranchName = "develop-nfbot/update-dependencies/" + [guid]::NewGuid().ToString()
$packageTargetVersion = $env:NBGV_NuGetPackageVersion

# working directory is agent temp directory
Write-Debug "Changing working directory to $env:Agent_TempDirectory"
Set-Location "$env:Agent_TempDirectory" | Out-Null

# clone repo and checkout develop branch
Write-Debug "Init and featch nf-Visual-Studio-extension repo"

git clone --depth 1 https://github.com/nanoframework/nf-Visual-Studio-extension repo
Set-Location repo | Out-Null
git config --global gc.auto 0
git config --global user.name nfbot
git config --global user.email nanoframework@outlook.com
git config --global core.autocrlf true

Write-Host "Checkout develop branch..."
git checkout --quiet develop | Out-Null

####################
# VS 2017

Write-Host "Updating nanoFramework.Tools.MetadataProcessor.MsBuildTask package in VS2017 solution..."

dotnet remove VisualStudio.Extension\VisualStudio.Extension.csproj package nanoFramework.Tools.MetadataProcessor.MsBuildTask

dotnet add VisualStudio.Extension\VisualStudio.Extension.csproj package nanoFramework.Tools.MetadataProcessor.MsBuildTask --prerelease -s https://pkgs.dev.azure.com/nanoframework/feed/_packaging/sandbox/nuget/v3/index.json

####################
# VS 2019

Write-Host "Updating nanoFramework.Tools.MetadataProcessor.MsBuildTask package in VS2019 solution..."

dotnet remove VisualStudio.Extension-2019\VisualStudio.Extension.csproj package nanoFramework.Tools.MetadataProcessor.MsBuildTask

dotnet add VisualStudio.Extension-2019\VisualStudio.Extension.csproj package nanoFramework.Tools.MetadataProcessor.MsBuildTask --prerelease -s https://pkgs.dev.azure.com/nanoframework/feed/_packaging/sandbox/nuget/v3/index.json

#####################

"Bumping MetadataProcessor.MsBuildTask to $packageTargetVersion." | Write-Host -ForegroundColor Cyan                

# build commit message
$commitMessage += "Bumps MetadataProcessor.MsBuildTask to $packageTargetVersion.`n"
# build PR title
$prTitle = "Bumps MetadataProcessor.MsBuildTask to $packageTargetVersion"

# need this line so nfbot flags the PR appropriately
$commitMessage += "`n[version update]`n`n"

# better add this warning line               
$commitMessage += "### :warning: This is an automated update. Merge only after all tests pass. :warning:`n"

Write-Debug "Git branch" 

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
# we are hardcoding to 'develop' branch to have a fixed one
# this is very important for tags (which don't have branch information)
# considering that the base branch can be changed at the PR ther is no big deal about this 
$prRequestBody = @{title="$prTitle";body="$commitMessage";head="$newBranchName";base="develop"} | ConvertTo-Json
$githubApiEndpoint = "https://api.github.com/repos/nanoframework/nf-Visual-Studio-extension/pulls"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$headers = @{}
$headers.Add("Authorization","$auth")
$headers.Add("Accept","application/vnd.github.symmetra-preview+json")

try 
{
    $result = Invoke-RestMethod -Method Post -UserAgent [Microsoft.PowerShell.Commands.PSUserAgent]::InternetExplorer -Uri  $githubApiEndpoint -Header $headers -ContentType "application/json" -Body $prRequestBody
    'Started PR with dependencies update...' | Write-Host -NoNewline
    'OK' | Write-Host -ForegroundColor Green
}
catch 
{
    $result = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($result)
    $reader.BaseStream.Position = 0
    $reader.DiscardBufferedData()
    $responseBody = $reader.ReadToEnd();

    throw "Error starting PR: $responseBody"
}
