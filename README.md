# Accuraty.Commands.Cli

A .NET 10 global command-line tool. The installed command is `accu`.

Complete how-to setup on Windows 11 is in /docs/README-Windows11-Setup.md

Microsoft.Playwright is pinned to version 1.63.0, with the resolved dependency graph recorded in `packages.lock.json`. To update dependencies, change the package version intentionally and run `dotnet restore` to refresh the lock file.

## Build and test

Run the same basic checks used by CI from the repository root:

```powershell
dotnet restore AccuCommands.sln --locked-mode
dotnet build AccuCommands.sln --configuration Release --no-restore
dotnet test AccuCommands.sln --configuration Release --no-build --no-restore
dotnet pack AccuCommands.csproj --configuration Release --no-build --no-restore --output artifacts/packages
```

## Help

Run `accu` for a short overview, `accu help` for the full command reference, or `accu examples` for usage examples (`accu samples` is an alias).

## Check the installed version

```powershell
accu version
accu -v
accu --version
```

## Capture a page

```powershell
accu capture accuraty.com
```

This opens the page in headless Chromium and saves a full-page JPG in the current user's Downloads folder. Use `--output` (or `-o`) to choose a path:

```powershell
accu capture https://example.com --output .\example.jpg
```

Use `--format pdf` (or `-f pdf`) to save the page as a PDF instead. The default filename extension follows the selected format; an explicit output path is used as provided.

```powershell
accu capture accuraty.com --format pdf
accu capture accuraty.com --format pdf --output .\accuraty.pdf
```

## Search Google results for a domain

`serp` uses DataForSEO to retrieve organic results from Google (default) or Bing and lists matching positions, titles, and URLs. It checks up to 10 pages and asks DataForSEO to stop crawling when the requested domain or one of its subdomains appears in organic results. The endpoint requires DataForSEO API Access credentials and bills for each results page crawled; a found domain can stop the crawl early, while an unmatched domain can use all requested pages. See [DataForSEO pricing](https://dataforseo.com/pricing/serp/google-organic-serp-api).

Create a DataForSEO account, find the API login and generated API password under API Access, then set them in the current PowerShell session:

```powershell
$env:DATAFORSEO_LOGIN = '<your-api-login>'
$env:DATAFORSEO_PASSWORD = '<your-api-password>'
```

Pass the search phrase separately from the location. U.S. city and state names or abbreviations are resolved to the DataForSEO location code. If omitted, searches use the United States. `--engine` accepts `google` or `bing` and defaults to Google; `--max-pages` accepts 1 through 10 and defaults to 10; `--timeout` sets the API timeout in seconds and defaults to 120; `--retries` sets the number of retry attempts and defaults to 3 (0 disables retries). Retries only occur for DataForSEO error codes explicitly added to the retry list in the program. Add `--verbose` to show request settings, response timing and cost, page and result counts, and extra error information:

```powershell
accu serp classicplumb.com "air conditioning" --location "Savoy, IL"
accu serp classicplumb.com --query "air conditioning" --location "Savoy, IL"
accu serp classicplumb.com --query "air conditioning" --location "Savoy, IL" --engine bing
accu serp classicplumb.com --query "air conditioning" --location "Savoy, IL" --max-pages 3
accu serp classicplumb.com --query "air conditioning" --timeout 240
accu serp classicplumb.com --query "air conditioning" --verbose
accu serp classicplumb.com --query "air conditioning" --retries 5 --verbose
```

DataForSEO receives the search query and returns Google results; `accu` matches the exact domain and its subdomains. Credentials are read from the environment and are not stored in the project or package. DataForSEO requires Basic authentication using the API credentials from its API Access page; the generated API password is different from the account password.

## Publish to the organization feed

With the `A:` network drive connected, run this from the project folder:

```powershell
.\ps\Publish-Package.ps1
```

This builds `Accuraty.Commands.Cli` and copies the NuGet package, its NuGet dependencies, and the installer script to `A:\dev\nupkg\`. The share acts as a local NuGet feed, so clients do not need to download the tool or its NuGet dependencies from nuget.org.

## Build, publish, and install locally

With the `A:` network drive connected, run this from the project folder:

```powershell
.\ps\Build-Publish-Install.ps1
```

This packs the project in Release configuration with the next dated development version (for example, `1.1.1-20260929-dev03` when the stable project version is `1.1.0`). The UTC date is followed by a two-digit iteration number that increments for each build published that day. It publishes the package and dependencies to `A:\dev\nupkg\`, then installs that exact version and its Chromium browser for the current user. The project file keeps its stable `Major.Minor.Patch` version unchanged. Try `accu version` when it finishes; it reports the development version for the installed build.

## Install or update on another machine

`dotnet build -c Release` only builds the project; it does not install or register the `accu` command. After a package has been published to the feed, run the installer below once on each machine (and again after publishing updates).

The machine needs the .NET 10 SDK and access to `A:\dev\nupkg\`. In PowerShell, run:

```powershell
& 'A:\dev\nupkg\Install-Accu.ps1'
```

The installer installs or updates the global `accu` command, installs Playwright's Chromium browser, and adds the .NET tools directory to the current user's `PATH`. The browser install downloads Chromium from Playwright's browser CDN, so the machine needs access to that CDN during installation. Open a new terminal after the script completes, then run:

```powershell
accu capture accuraty.com
```
