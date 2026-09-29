param(
    [string] $PackageFeed = 'A:\dev\nupkg',
    [string] $PackageVersion
)

$ErrorActionPreference = 'Stop'
$packageId = 'Accuraty.Commands.Cli'

function Get-InstalledAccu {
    $listOutput = dotnet tool list --global --format json
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not list installed .NET tools. Make sure the .NET 10 SDK is installed.'
    }

    $toolList = ($listOutput -join [Environment]::NewLine) | ConvertFrom-Json
    $tools = if ($toolList.PSObject.Properties.Name -contains 'data') { @($toolList.data) } else { @($toolList) }
    $tools | Where-Object { $_.packageId -ieq $packageId } | Select-Object -First 1
}

if (-not (Test-Path -LiteralPath $PackageFeed -PathType Container)) {
    throw "Package feed '$PackageFeed' is unavailable. Connect or map the A: drive, then try again."
}

$installed = Get-InstalledAccu
$toolArguments = @('tool')
if ($installed) {
    $toolArguments += 'update'
    if ($PackageVersion) {
        # New date-first prerelease versions sort below the previous dev.timestamp versions.
        $toolArguments += '--allow-downgrade'
    }
} else {
    $toolArguments += 'install'
}
$toolArguments += @('--global', '--source', $PackageFeed, $packageId)
if ($PackageVersion) {
    $toolArguments += @('--version', $PackageVersion)
    Write-Host "Installing requested package version $PackageVersion..."
}
dotnet @toolArguments
if ($LASTEXITCODE -ne 0) {
    throw "Could not install or update $packageId from '$PackageFeed'."
}

$installed = Get-InstalledAccu
if (-not $installed) {
    throw "The .NET tool command did not report $packageId after installation."
}
if ($PackageVersion -and $installed.version -ne $PackageVersion) {
    throw "Expected $packageId version '$PackageVersion' after installation, but found '$($installed.version)'."
}

$toolStore = Join-Path $HOME ".dotnet\tools\.store\$($packageId.ToLowerInvariant())\$($installed.version)"
$playwrightScript = Get-ChildItem -LiteralPath $toolStore -Filter 'playwright.ps1' -File -Recurse -ErrorAction SilentlyContinue |
    Select-Object -First 1 -ExpandProperty FullName
if (-not $playwrightScript) {
    throw "The Playwright browser installer was not found under '$toolStore'."
}

Write-Host 'Installing the Chromium browser required by accu...'
& $playwrightScript install chromium
if ($LASTEXITCODE -ne 0) {
    throw "Chromium installation failed with exit code $LASTEXITCODE."
}

$dotnetTools = Join-Path $HOME '.dotnet\tools'
$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
$pathEntries = @($userPath -split ';' | Where-Object { $_ })
if ($pathEntries -notcontains $dotnetTools) {
    $newUserPath = (@($pathEntries) + $dotnetTools) -join ';'
    [Environment]::SetEnvironmentVariable('Path', $newUserPath, 'User')
}
$env:Path = "$dotnetTools;$env:Path"

Write-Host 'Accu is installed. Try: accu capture accuraty.com'
Write-Host 'Open a new terminal window for the updated PATH to apply there.'
