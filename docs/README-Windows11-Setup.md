# Set Up AccuCommands on Windows 11

This guide covers installing and running the `accu` command-line tool on a newly installed, up-to-date Windows 11 PC.

For dependencies and installation details, skip to the bottom of this document.

## Step 1: Install PowerShell 7 with winget

Open **Windows PowerShell** or **Command Prompt** and install PowerShell 7 from the command line:

```powershell
winget install --id Microsoft.PowerShell --source winget
```

If winget asks you to accept package source or agreement terms, accept them to continue. When the installation finishes, open a new terminal window and start PowerShell 7 by running:

```powershell
pwsh
```

Continue with the remaining steps in that PowerShell 7 window. You can confirm the version with:

```powershell
$PSVersionTable.PSVersion
```

## Step 2: Connect to the package feed

Connect to the organization’s package share and make sure it is available as `A:\dev\nupkg`. The installer script and NuGet package files are expected in that folder.

## Step 3: Install the .NET 10 SDK

Install the .NET 10 SDK, then check that PowerShell can find it:

```powershell
dotnet --list-sdks
```

The output should include a `10.0.xxx` SDK. The .NET SDK is required because the installer uses `dotnet tool` commands and this project targets .NET 10.

## Step 4: Install AccuCommands and Chromium

Run the installer from the package share:

```powershell
& 'A:\dev\nupkg\Install-Accu.ps1'
```

The script installs or updates the global `accu` command from the local NuGet feed, downloads Playwright's Chromium browser, and adds the .NET global tools folder (`%USERPROFILE%\.dotnet\tools`) to your user `PATH`.

The PC needs network access to Playwright's browser CDN for the Chromium download. Once installation succeeds, open a new PowerShell 7 window so the updated `PATH` is available.

## Step 5: Run a capture

Capture a site as a full-page JPG. By default, the image is saved in your Downloads folder:

```powershell
accu capture accuraty.com
```

Choose an output path or save a PDF instead:

```powershell
accu capture https://example.com --output .\example.jpg
accu capture accuraty.com --format pdf
accu capture accuraty.com --format pdf --output .\accuraty.pdf
```

## Optional: Search Google or Bing results

The `serp` command uses DataForSEO and requires an account with API Access credentials. In PowerShell 7, set the API login and generated API password for the current session:

```powershell
$env:DATAFORSEO_LOGIN = '<your-api-login>'
$env:DATAFORSEO_PASSWORD = '<your-api-password>'
```

Then search for a domain and phrase:

```powershell
accu serp classicplumb.com --query "air conditioning" --location "Savoy, IL"
```

The location is optional; searches default to the United States. U.S. city and state names or abbreviations are resolved to DataForSEO location codes. Select Bing with `--engine bing`; `--max-pages` can be set from 1 to 10 and defaults to 10. The API request timeout defaults to 120 seconds and can be changed with `--timeout <seconds>`. The retry attempt count defaults to 3 and can be changed with `--retries <count>` (0 disables retries); only DataForSEO error codes explicitly added to the retry list are retried. Add `--verbose` to print request settings, response timing and cost, page and result counts, and extra error information.

DataForSEO receives the search query and charges for each results page crawled. The command can stop early if it finds the requested domain or a subdomain in organic results. See [DataForSEO pricing](https://dataforseo.com/pricing/serp/google-organic-serp-api).

## Dependencies and access needed

- Windows 11 with `winget` available (provided by the App Installer package).
- PowerShell 7, installed in Step 1.
- .NET 10 SDK.
- Access to the organization's package feed at `A:\dev\nupkg`.
- Network access to Playwright's browser CDN during installation, to download Chromium.
- For the optional `serp` command: DataForSEO API credentials and internet access.

The package feed contains the tool package and NuGet dependencies, so client machines do not need to restore those packages from nuget.org.

## Where AccuCommands is installed

The installer uses `dotnet tool install --global` (or `dotnet tool update --global`) to install the NuGet package for the current Windows user. It also runs Playwright's installer to download Chromium and adds the .NET tools folder to that user's `PATH`.

- **Command launcher:** `%USERPROFILE%\.dotnet\tools\accu.exe` (with a command shim alongside it). This is what lets you run `accu` from a terminal.
- **Installed tool files:** `%USERPROFILE%\.dotnet\tools\.store\accuraty.commands.cli\<version>\...`. The installer locates Playwright's `playwright.ps1` script within this package store.
- **Playwright Chromium:** `%USERPROFILE%\AppData\Local\ms-playwright`.

A global .NET tool is installed for the current user, not all users on the machine. The installer reads the NuGet package and dependencies from `A:\dev\nupkg`; it does not copy the installed tool into the project folder.
