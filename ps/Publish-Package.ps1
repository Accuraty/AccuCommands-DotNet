$ErrorActionPreference = 'Stop'

$packageShare = 'A:\dev\nupkg'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not (Test-Path -LiteralPath $packageShare -PathType Container)) {
    throw "Package share '$packageShare' is unavailable. Connect or map the A: drive, then try again."
}

dotnet pack (Join-Path $projectRoot 'AccuCommands.csproj') --configuration Release --output $packageShare
if ($LASTEXITCODE -ne 0) {
    throw "dotnet pack failed with exit code $LASTEXITCODE."
}

$assetsFile = Join-Path $projectRoot 'obj\project.assets.json'
$assets = Get-Content -LiteralPath $assetsFile -Raw | ConvertFrom-Json
$packageFolders = @($assets.packageFolders.PSObject.Properties.Name)
foreach ($libraryEntry in $assets.libraries.PSObject.Properties) {
    $library = $libraryEntry.Value
    if ($library.type -ne 'package') {
        continue
    }

    $libraryPath = $libraryEntry.Name.ToLowerInvariant()
    $packageFileName = "$($libraryEntry.Name.Split('/')[0]).$($libraryEntry.Name.Split('/')[1]).nupkg"
    $sourcePackage = $null
    foreach ($packageFolder in $packageFolders) {
        $candidate = Join-Path $packageFolder (Join-Path $libraryPath $packageFileName)
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            $sourcePackage = $candidate
            break
        }
    }

    if (-not $sourcePackage) {
        throw "Could not locate cached NuGet dependency '$packageFileName'."
    }
    Copy-Item -LiteralPath $sourcePackage -Destination (Join-Path $packageShare $packageFileName) -Force
}

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install-Accu.ps1') -Destination (Join-Path $packageShare 'Install-Accu.ps1') -Force
Write-Host "Published the Accuraty.Commands.Cli package and installer to $packageShare"
