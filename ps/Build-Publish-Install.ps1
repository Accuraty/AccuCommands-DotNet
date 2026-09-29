$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path $PSScriptRoot -Parent
$projectFile = Join-Path $projectRoot 'AccuCommands.csproj'
$project = [xml](Get-Content -LiteralPath $projectFile -Raw)
$packageId = [string]($project.Project.PropertyGroup.PackageId | Select-Object -First 1)
$stableVersion = [string]($project.Project.PropertyGroup.Version | Select-Object -First 1)
if ($stableVersion -notmatch '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)$') {
    throw "The project Version must be a stable Major.Minor.Patch value; found '$stableVersion'."
}

$major = [int]$Matches['major']
$minor = [int]$Matches['minor']
$patch = [int]$Matches['patch'] + 1
$packageShare = 'A:\dev\nupkg'
if (-not (Test-Path -LiteralPath $packageShare -PathType Container)) {
    throw "Package share '$packageShare' is unavailable. Connect or map the A: drive, then try again."
}

$buildDate = [DateTime]::UtcNow.ToString('yyyyMMdd', [Globalization.CultureInfo]::InvariantCulture)
$escapedPackageId = [regex]::Escape($packageId)
$escapedStableVersion = [regex]::Escape("$major.$minor.$patch")
$packageVersionPrefixPattern = '^' + $escapedPackageId + '\.' + $escapedStableVersion + '-'
$datedVersionPattern = $packageVersionPrefixPattern + [regex]::Escape($buildDate) + '-dev(?<iteration>\d+)\.nupkg$'
$legacyVersionPattern = $packageVersionPrefixPattern + 'dev\.' + [regex]::Escape($buildDate) + 'T\d+Z\.nupkg$'
$existingPackages = @(Get-ChildItem -LiteralPath $packageShare -File -Filter "$packageId.$major.$minor.$patch-*.nupkg")
$maxIteration = 0
$legacyBuildCount = 0
foreach ($package in $existingPackages) {
    if ($package.Name -match $datedVersionPattern) {
        $maxIteration = [Math]::Max($maxIteration, [int]$Matches['iteration'])
    }
    elseif ($package.Name -match $legacyVersionPattern) {
        $legacyBuildCount++
    }
}

# Count same-day timestamp builds during the transition so the first dated build continues their sequence.
$iteration = [Math]::Max($maxIteration, $legacyBuildCount) + 1
$iterationText = $iteration.ToString('D2', [Globalization.CultureInfo]::InvariantCulture)
$packageVersion = '{0}.{1}.{2}-{3}-dev{4}' -f $major, $minor, $patch, $buildDate, $iterationText

$publishScript = Join-Path $PSScriptRoot 'Publish-Package.ps1'
$installScript = Join-Path $PSScriptRoot 'Install-Accu.ps1'

Write-Host "Building and publishing development package $packageVersion to A:\dev\nupkg..."
& $publishScript -PackageVersion $packageVersion

Write-Host 'Installing or updating the global accu tool...'
& $installScript -PackageVersion $packageVersion

Write-Host 'Build, publish, and install complete. Try: accu version'
