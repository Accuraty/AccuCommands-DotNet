using System.Runtime.InteropServices;
using System.Reflection;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Playwright;

return await Accu.RunAsync(args);

internal static class Accu
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            PrintSummary();
            return 0;
        }

        if (args[0] is "--help" or "-h" or "help")
        {
            PrintHelp();
            return 0;
        }

        if (args[0] is "examples" or "samples")
        {
            PrintExamples();
            return 0;
        }

        if (args[0] is "version" or "--version" or "-v")
        {
            Console.WriteLine(GetVersion());
            return 0;
        }

        if (args[0] == "serp")
            return await RunSerpAsync(args);

        if (args[0] != "capture")
        {
            Console.Error.WriteLine($"Unknown command: {args[0]}");
            PrintSummary();
            return 2;
        }

        if (args.Length < 2 || args[1].StartsWith('-'))
        {
            Console.Error.WriteLine("Usage: accu capture <url> [--format jpg|pdf] [--output <path>]");
            return 2;
        }

        string? outputPath = null;
        var format = "jpg";
        for (var i = 2; i < args.Length; i++)
        {
            if ((args[i] is "--output" or "-o") && i + 1 < args.Length)
                outputPath = args[++i];
            else if ((args[i] is "--format" or "-f") && i + 1 < args.Length)
                format = args[++i].ToLowerInvariant();
            else
            {
                Console.Error.WriteLine($"Unknown or incomplete option: {args[i]}");
                return 2;
            }
        }

        if (format is not ("jpg" or "pdf"))
        {
            Console.Error.WriteLine("The format must be 'jpg' or 'pdf'.");
            return 2;
        }

        if (!Uri.TryCreate(args[1].Contains("://", StringComparison.Ordinal) ? args[1] : $"https://{args[1]}", UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            Console.Error.WriteLine("Please provide a valid HTTP or HTTPS website address.");
            return 2;
        }

        var extension = format == "pdf" ? "pdf" : "jpg";
        outputPath ??= Path.Combine(GetDownloadsFolder(), $"{SafeFileName(uri.Host)}-{DateTime.Now:yyyyMMdd-HHmmss}.{extension}");

        try
        {
            outputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var page = await browser.NewPageAsync(new BrowserNewPageOptions
            {
                ViewportSize = new ViewportSize { Width = 1440, Height = 1000 },
                DeviceScaleFactor = 1
            });

            Console.WriteLine($"Loading {GetDisplayUrl(uri)} …");
            await page.GotoAsync(uri.ToString(), new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 30_000
            });
            if (format == "pdf")
            {
                await page.PdfAsync(new PagePdfOptions
                {
                    Path = outputPath,
                    PrintBackground = true
                });
            }
            else
            {
                await page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = outputPath,
                    Type = ScreenshotType.Jpeg,
                    FullPage = true,
                    Quality = 90
                });
            }

            Console.WriteLine($"Saved page as {format.ToUpperInvariant()}: {outputPath}");
            return 0;
        }
        catch (PlaywrightException ex)
        {
            var safeMessage = ex.Message.Replace(uri.ToString(), GetDisplayUrl(uri), StringComparison.OrdinalIgnoreCase);
            Console.Error.WriteLine($"Could not capture the page: {safeMessage}");
            if (ex.Message.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase))
                Console.Error.WriteLine("Install Chromium once with the Playwright script generated beside the build output: .\\playwright.ps1 install chromium");
            return 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Console.Error.WriteLine($"Could not use output path '{outputPath}': {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> RunSerpAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: accu serp <domain> --query <phrase> [--location <city, state>] [--engine google|bing] [--max-pages 1-10]");
            return 2;
        }

        var domain = NormalizeDomain(args[1]);
        if (domain is null)
        {
            Console.Error.WriteLine("Please provide a valid website domain.");
            return 2;
        }

        if (!TryParseSerpOptions(args[2..], out var options, out var parseError))
        {
            Console.Error.WriteLine(parseError);
            return 2;
        }
        var query = options.Query;
        var location = options.Location;
        var engine = options.Engine;
        var maxPages = options.MaxPages;

        var login = Environment.GetEnvironmentVariable("DATAFORSEO_LOGIN");
        var password = Environment.GetEnvironmentVariable("DATAFORSEO_PASSWORD");
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
        {
            Console.Error.WriteLine("Set DATAFORSEO_LOGIN and DATAFORSEO_PASSWORD from your DataForSEO API Access page before using accu serp.");
            return 2;
        }

        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{login}:{password}"));
            var authHeader = new AuthenticationHeaderValue("Basic", credentials);
            var locationCode = string.IsNullOrWhiteSpace(location)
                ? (int?)null
                : await ResolveLocationCodeAsync(httpClient, authHeader, engine, location);
            if (!string.IsNullOrWhiteSpace(location) && locationCode is null)
                return 1;

            var task = new Dictionary<string, object>
            {
                ["keyword"] = query.Trim(),
                ["language_code"] = "en",
                ["depth"] = maxPages * 10,
                ["max_crawl_pages"] = maxPages,
                ["stop_crawl_on_match"] = new[]
                {
                    new Dictionary<string, string>
                    {
                        ["match_value"] = domain,
                        ["match_type"] = "with_subdomains"
                    }
                },
                ["find_targets_in"] = new[] { "organic" },
                ["device"] = "desktop"
            };
            if (locationCode is int code)
                task["location_code"] = code;
            else
                task["location_name"] = "United States";

            using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.dataforseo.com/v3/serp/{engine}/organic/live/regular");
            request.Headers.Authorization = authHeader;
            request.Content = new StringContent(JsonSerializer.Serialize(new[] { task }), Encoding.UTF8, "application/json");
            Console.WriteLine($"Searching {engine} for \"{query.Trim()}\" via DataForSEO …");
            using var response = await httpClient.SendAsync(request);
            var responseJson = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"DataForSEO request failed with HTTP {(int)response.StatusCode} ({response.ReasonPhrase}). Check your API credentials and account balance.");
                return 1;
            }

            var parsedResponse = DataForSeoResponseParser.ParseSerp(responseJson);
            if (parsedResponse.ErrorMessage is not null)
            {
                Console.Error.WriteLine($"DataForSEO error: {parsedResponse.ErrorMessage}");
                return 1;
            }

            if (parsedResponse.NoTasks)
            {
                Console.Error.WriteLine("DataForSEO returned no search task results.");
                return 1;
            }

            if (parsedResponse.TaskErrorMessage is not null)
            {
                Console.Error.WriteLine($"DataForSEO error: {parsedResponse.TaskErrorMessage ?? "search task failed"}");
                return 1;
            }

            var results = parsedResponse.Results ?? [];
            var matches = results.Where(result => IsDomainMatch(result.Url, domain)).ToArray();
            if (matches.Length == 0)
            {
                Console.WriteLine($"No results from {domain} found in up to {maxPages} page(s) of {engine} organic results ({results.Count} results returned).");
                return 0;
            }

            foreach (var result in matches)
            {
                Console.WriteLine($"{result.Position}. {result.Title}");
                Console.WriteLine($"   {result.Url}");
            }

            return 0;
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Could not reach DataForSEO: {ex.Message}");
            return 1;
        }
        catch (TaskCanceledException)
        {
            Console.Error.WriteLine("The DataForSEO request timed out.");
            return 1;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Could not parse the DataForSEO response: {ex.Message}");
            return 1;
        }
    }

    private static bool TryReadOptionValue(string[] args, ref int index, out string value)
    {
        value = "";
        if (index + 1 >= args.Length || args[index + 1].StartsWith("-", StringComparison.Ordinal))
            return false;
        value = args[++index];
        return !string.IsNullOrWhiteSpace(value);
    }

    internal static bool TryParseSerpOptions(string[] args, out SerpOptions options, out string error)
    {
        string? query = null;
        string? location = null;
        var engine = "google";
        var maxPages = 10;
        var positionalQueryParts = new List<string>();
        var optionsStarted = false;

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is "--query" or "-q")
            {
                optionsStarted = true;
                if (positionalQueryParts.Count > 0 || !TryReadOptionValue(args, ref i, out query))
                {
                    options = default;
                    error = "Provide the search phrase either positionally or with --query, not both.";
                    return false;
                }
            }
            else if (args[i] is "--location" or "-l")
            {
                optionsStarted = true;
                if (!TryReadOptionValue(args, ref i, out location))
                {
                    options = default;
                    error = "The --location option requires a location.";
                    return false;
                }
            }
            else if (args[i] is "--engine" or "-e")
            {
                optionsStarted = true;
                if (!TryReadOptionValue(args, ref i, out engine))
                {
                    options = default;
                    error = "The --engine option requires 'google' or 'bing'.";
                    return false;
                }
                engine = engine.ToLowerInvariant();
            }
            else if (args[i] is "--max-pages" or "-p")
            {
                optionsStarted = true;
                if (!TryReadOptionValue(args, ref i, out var pagesText)
                    || !int.TryParse(pagesText, out maxPages)
                    || maxPages is < 1 or > 10)
                {
                    options = default;
                    error = "The --max-pages option must be a number from 1 to 10.";
                    return false;
                }
            }
            else if (args[i].StartsWith("-", StringComparison.Ordinal) || optionsStarted)
            {
                options = default;
                error = $"Unknown option or unexpected argument: {args[i]}";
                return false;
            }
            else
            {
                positionalQueryParts.Add(args[i]);
            }
        }

        query ??= positionalQueryParts.Count > 0 ? string.Join(' ', positionalQueryParts) : null;
        if (string.IsNullOrWhiteSpace(query))
        {
            options = default;
            error = "Provide a search phrase, either after the domain or with --query.";
            return false;
        }
        if (engine is not ("google" or "bing"))
        {
            options = default;
            error = "The --engine option must be 'google' or 'bing'.";
            return false;
        }

        options = new SerpOptions(query, location, engine, maxPages);
        error = "";
        return true;
    }

    private static async Task<int?> ResolveLocationCodeAsync(
        HttpClient httpClient,
        AuthenticationHeaderValue authHeader,
        string engine,
        string location)
    {
        var parts = location.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            Console.Error.WriteLine("Use a U.S. city and state for --location, for example 'Savoy, IL'.");
            return null;
        }

        var state = NormalizeUsState(parts[1]);
        if (state is null)
        {
            Console.Error.WriteLine($"Unrecognized U.S. state in location '{location}'. Use a state name or abbreviation.");
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.dataforseo.com/v3/serp/{engine}/locations/US");
        request.Headers.Authorization = authHeader;
        using var response = await httpClient.SendAsync(request);
        var responseJson = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"DataForSEO location lookup failed with HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
            return null;
        }

        IReadOnlyList<int>? locationCodes;
        try
        {
            locationCodes = DataForSeoResponseParser.ParseLocationCodes(responseJson, parts[0], state);
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Could not parse the DataForSEO location response: {ex.Message}");
            return null;
        }
        if (locationCodes is null)
        {
            Console.Error.WriteLine("DataForSEO returned no valid locations.");
            return null;
        }
        if (locationCodes.Count != 1)
        {
            Console.Error.WriteLine($"Could not find a unique DataForSEO city location for '{location}'. Try a more specific city name.");
            return null;
        }

        return locationCodes[0];
    }

    private static string? NormalizeUsState(string value)
    {
        var states = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AL"] = "Alabama", ["AK"] = "Alaska", ["AZ"] = "Arizona", ["AR"] = "Arkansas", ["CA"] = "California",
            ["CO"] = "Colorado", ["CT"] = "Connecticut", ["DE"] = "Delaware", ["FL"] = "Florida", ["GA"] = "Georgia",
            ["HI"] = "Hawaii", ["ID"] = "Idaho", ["IL"] = "Illinois", ["IN"] = "Indiana", ["IA"] = "Iowa", ["KS"] = "Kansas",
            ["KY"] = "Kentucky", ["LA"] = "Louisiana", ["ME"] = "Maine", ["MD"] = "Maryland", ["MA"] = "Massachusetts",
            ["MI"] = "Michigan", ["MN"] = "Minnesota", ["MS"] = "Mississippi", ["MO"] = "Missouri", ["MT"] = "Montana",
            ["NE"] = "Nebraska", ["NV"] = "Nevada", ["NH"] = "New Hampshire", ["NJ"] = "New Jersey", ["NM"] = "New Mexico",
            ["NY"] = "New York", ["NC"] = "North Carolina", ["ND"] = "North Dakota", ["OH"] = "Ohio", ["OK"] = "Oklahoma",
            ["OR"] = "Oregon", ["PA"] = "Pennsylvania", ["RI"] = "Rhode Island", ["SC"] = "South Carolina", ["SD"] = "South Dakota",
            ["TN"] = "Tennessee", ["TX"] = "Texas", ["UT"] = "Utah", ["VT"] = "Vermont", ["VA"] = "Virginia", ["WA"] = "Washington",
            ["WV"] = "West Virginia", ["WI"] = "Wisconsin", ["WY"] = "Wyoming", ["DC"] = "District of Columbia"
        };

        return states.TryGetValue(value, out var stateName)
            ? stateName
            : states.Values.FirstOrDefault(stateName => stateName.Equals(value, StringComparison.OrdinalIgnoreCase));
    }

    internal static string GetDisplayUrl(Uri uri)
    {
        var host = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
        return $"{uri.Scheme}://{host}{uri.AbsolutePath}";
    }

    private static string? NormalizeDomain(string value)
    {
        var candidate = value.Contains("://", StringComparison.Ordinal) ? value : $"https://{value}";
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return null;

        var host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
            host = host[4..];
        return host.Length == 0 ? null : host;
    }

    private static bool IsDomainMatch(string resultUrl, string targetDomain)
    {
        if (!Uri.TryCreate(resultUrl, UriKind.Absolute, out var resultUri) || resultUri.Scheme is not ("http" or "https"))
            return false;

        var resultHost = resultUri.IdnHost.TrimEnd('.');
        return resultHost.Equals(targetDomain, StringComparison.OrdinalIgnoreCase)
            || resultHost.EndsWith($".{targetDomain}", StringComparison.OrdinalIgnoreCase);
    }

    private static void PrintHelp()
    {
        Console.WriteLine("accu — small command-line utilities");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  help                            Show this help");
        Console.WriteLine("  examples (samples)              Show command examples");
        Console.WriteLine("  version (-v, --version)        Show the installed accu version");
        Console.WriteLine("  capture <url> [--format jpg|pdf] [--output <path>]");
        Console.WriteLine("                                  Save the page as a JPG (default) or PDF in Downloads");
        Console.WriteLine("  serp <domain> <search phrase> [--location <city, state>]");
        Console.WriteLine("       (or use --query <phrase>)");
        Console.WriteLine("       [--engine google|bing] [--max-pages 1-10]");
        Console.WriteLine("                                  Find a domain's organic position on Google or Bing");
    }

    private static void PrintSummary()
    {
        Console.WriteLine(GetVersion());
        Console.WriteLine("Commands: help, version, capture, serp, examples");
        Console.WriteLine("More info: accu help or accu examples");
    }

    private static string GetVersion()
    {
        var informationalVersion = typeof(Accu).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return string.IsNullOrWhiteSpace(informationalVersion)
            ? typeof(Accu).Assembly.GetName().Version?.ToString(3) ?? "unknown"
            : informationalVersion.Split('+')[0];
    }

    private static void PrintExamples()
    {
        Console.WriteLine("Examples:");
        Console.WriteLine("  accu version");
        Console.WriteLine("  accu -v");
        Console.WriteLine("  accu capture accuraty.com");
        Console.WriteLine("  accu capture accuraty.com --format pdf");
        Console.WriteLine("  accu capture https://example.com --output page.jpg");
        Console.WriteLine("  accu serp classicplumb.com \"air conditioning\" --location \"Savoy, IL\"");
        Console.WriteLine("  accu serp classicplumb.com --query \"air conditioning\" --location \"Savoy, IL\" --engine bing");
        Console.WriteLine("  accu serp classicplumb.com --query \"air conditioning\" --location \"Savoy, IL\" --max-pages 3");
    }

    private static string SafeFileName(string value)
    {
        foreach (var character in Path.GetInvalidFileNameChars())
            value = value.Replace(character, '-');
        return value.Replace(':', '-');
    }

    private static string GetDownloadsFolder()
    {
        if (OperatingSystem.IsWindows() && SHGetKnownFolderPath(DownloadsFolderId, 0, IntPtr.Zero, out var path) == 0)
        {
            try { return Marshal.PtrToStringUni(path) ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"); }
            finally { Marshal.FreeCoTaskMem(path); }
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    private static readonly Guid DownloadsFolderId = new("374DE290-123F-4565-9164-39C4925E467B");

    internal readonly record struct SerpOptions(string Query, string? Location, string Engine, int MaxPages);

    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint flags, IntPtr token, out IntPtr path);
}
