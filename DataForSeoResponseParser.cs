using System.Text.Json;

internal static class DataForSeoResponseParser
{
    public static SerpResponse ParseSerp(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = RequireObject(document.RootElement, "response");
        var responseError = GetStatusError(root, "response");
        if (responseError is not null)
            return new SerpResponse(responseError, false, null, null) { Diagnostics = GetDiagnostics(root) };

        if (!root.TryGetProperty("tasks", out var tasks))
            return new SerpResponse(null, true, null, null) { Diagnostics = GetDiagnostics(root) };
        RequireArray(tasks, "tasks");
        if (tasks.GetArrayLength() == 0)
            return new SerpResponse(null, true, null, null) { Diagnostics = GetDiagnostics(root) };

        var task = RequireObject(tasks[0], "tasks[0]");
        var taskError = GetStatusError(task, "task");
        if (taskError is not null)
            return new SerpResponse(null, false, taskError, null) { Diagnostics = GetDiagnostics(root, task) };

        if (!task.TryGetProperty("result", out var result))
            return new SerpResponse(null, false, null, []) { Diagnostics = GetDiagnostics(root, task) };
        RequireArray(result, "tasks[0].result");
        if (result.GetArrayLength() == 0)
            return new SerpResponse(null, false, null, []) { Diagnostics = GetDiagnostics(root, task) };

        var firstResult = RequireObject(result[0], "tasks[0].result[0]");
        var diagnostics = GetDiagnostics(root, task, firstResult);
        if (!firstResult.TryGetProperty("items", out var items))
            return new SerpResponse(null, false, null, []) { Diagnostics = diagnostics };
        RequireArray(items, "tasks[0].result[0].items");

        var parsedItems = new List<SearchItem>();
        var itemIndex = 0;
        foreach (var itemElement in items.EnumerateArray())
        {
            var item = RequireObject(itemElement, $"items[{itemIndex}]");
            if (GetString(item, "type") == "organic")
                parsedItems.Add(new SearchItem(GetInt(item, "rank_group") ?? 0,
                    GetString(item, "title") ?? "", GetString(item, "url") ?? "", GetInt(item, "page")));
            itemIndex++;
        }

        return new SerpResponse(null, false, null, parsedItems) { Diagnostics = diagnostics };
    }

    private static SerpDiagnostics GetDiagnostics(JsonElement root, JsonElement? task = null, JsonElement? result = null)
    {
        var spell = result is JsonElement resultElement
            && resultElement.TryGetProperty("spell", out var spellElement)
            && spellElement.ValueKind == JsonValueKind.Object
                ? spellElement
                : (JsonElement?)null;

        return new SerpDiagnostics(
            task is JsonElement taskElement ? GetString(taskElement, "id") : null,
            GetInt(root, "status_code"),
            task is JsonElement taskStatusElement ? GetInt(taskStatusElement, "status_code") : null,
            GetString(root, "time"),
            task is JsonElement taskTimeElement ? GetString(taskTimeElement, "time") : null,
            GetDecimal(root, "cost"),
            task is JsonElement taskCostElement ? GetDecimal(taskCostElement, "cost") : null,
            result is JsonElement resultMeta ? GetInt(resultMeta, "pages_count") : null,
            result is JsonElement resultItems ? GetInt(resultItems, "items_count") : null,
            result is JsonElement resultCount ? GetInt(resultCount, "se_results_count") : null,
            result is JsonElement resultUrl ? GetString(resultUrl, "check_url") : null,
            result is JsonElement resultDate ? GetString(resultDate, "datetime") : null,
            result is JsonElement resultTypes ? GetStringArray(resultTypes, "item_types") : [],
            spell is JsonElement spellInfo ? GetString(spellInfo, "keyword") : null,
            spell is JsonElement spellType ? GetString(spellType, "type") : null);
    }

    public static IReadOnlyList<int>? ParseLocationCodes(string json, string city, string state)
    {
        using var document = JsonDocument.Parse(json);
        var root = RequireObject(document.RootElement, "response");
        if (GetStatusError(root, "response") is not null)
            return null;
        if (!root.TryGetProperty("tasks", out var tasks))
            return null;
        RequireArray(tasks, "tasks");
        if (tasks.GetArrayLength() == 0)
            return null;

        var task = RequireObject(tasks[0], "tasks[0]");
        if (GetStatusError(task, "task") is not null || !task.TryGetProperty("result", out var locations))
            return null;
        RequireArray(locations, "tasks[0].result");

        var expectedName = $"{city},{state},United States";
        var matches = new List<int>();
        var index = 0;
        foreach (var element in locations.EnumerateArray())
        {
            var location = RequireObject(element, $"locations[{index}]");
            if (string.Equals(GetString(location, "location_name"), expectedName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(GetString(location, "location_type"), "City", StringComparison.OrdinalIgnoreCase)
                && string.Equals(GetString(location, "country_iso_code"), "US", StringComparison.OrdinalIgnoreCase)
                && GetInt(location, "location_code") is int code)
                matches.Add(code);
            index++;
        }

        return matches;
    }

    private static SerpError? GetStatusError(JsonElement element, string context)
    {
        if (!element.TryGetProperty("status_code", out var status))
            return null;
        if (status.ValueKind != JsonValueKind.Number || !status.TryGetInt32(out var code))
            throw new JsonException($"Expected {context}.status_code to be an integer.");
        return code == 20000
            ? null
            : new SerpError(code, GetString(element, "status_message") ?? $"{context} failed",
                GetString(element, "id"), context);
    }

    private static JsonElement RequireObject(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new JsonException($"Expected {path} to be an object.");
        return element;
    }

    private static void RequireArray(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Array)
            throw new JsonException($"Expected {path} to be an array.");
    }

    private static string? GetString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
            throw new JsonException($"Expected '{name}' to be a string.");
        return value.GetString();
    }

    private static int? GetInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
            throw new JsonException($"Expected '{name}' to be an integer.");
        return number;
    }

    private static decimal? GetDecimal(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var number))
            throw new JsonException($"Expected '{name}' to be a number.");
        return number;
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
            return [];
        if (value.ValueKind != JsonValueKind.Array)
            throw new JsonException($"Expected '{name}' to be an array.");
        var values = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                throw new JsonException($"Expected '{name}' values to be strings.");
            values.Add(item.GetString()!);
        }
        return values;
    }

    internal sealed record SerpError(int StatusCode, string Message, string? TaskId, string Context);
    internal sealed record SerpDiagnostics(
        string? TaskId, int? ResponseStatusCode, int? TaskStatusCode,
        string? ApiTime, string? TaskTime, decimal? ApiCost, decimal? TaskCost,
        int? PagesCount, int? ItemsCount, int? SearchResultsCount,
        string? CheckUrl, string? ResultDateTime, IReadOnlyList<string> ItemTypes,
        string? CorrectedKeyword, string? CorrectionType);
    internal sealed record SerpResponse(SerpError? Error, bool NoTasks, SerpError? TaskError, IReadOnlyList<SearchItem>? Results)
    {
        public string? ErrorMessage => Error?.Message;
        public string? TaskErrorMessage => TaskError?.Message;
        public SerpDiagnostics? Diagnostics { get; init; }
    }
    internal sealed record SearchItem(int Position, string Title, string Url, int? Page);
}
