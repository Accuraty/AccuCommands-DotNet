$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path $PSScriptRoot -Parent
$projectFile = Join-Path $projectRoot 'AccuCommands.csproj'
$project = [xml](Get-Content -LiteralPath $projectFile -Raw)
$stableVersion = [string]($project.Project.PropertyGroup.Version | Select-Object -First 1)
if ($stableVersion -notmatch '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)$') {
    throw "The project Version must be a stable Major.Minor.Patch value; found '$stableVersion'."
}

$major = [int]$Matches['major']
$minor = [int]$Matches['minor']
$patch = [int]$Matches['patch'] + 1
$buildStamp = [DateTime]::UtcNow.ToString("yyyyMMdd'T'HHmmssfffffff'Z'", [Globalization.CultureInfo]::InvariantCulture)
$packageVersion = '{0}.{1}.{2}-dev.{3}' -f $major, $minor, $patch, $buildStamp

$publishScript = Join-Path $PSScriptRoot 'Publish-Package.ps1'
$installScript = Join-Path $PSScriptRoot 'Install-Accu.ps1'

Write-Host "Building and publishing development package $packageVersion to A:\dev\nupkg..."
& $publishScript -PackageVersion $packageVersion

Write-Host 'Installing or updating the global accu tool...'
& $installScript -PackageVersion $packageVersion

Write-Host 'Build, publish, and install complete. Try: accu version'
