using System.Text.Json;

internal static class DataForSeoResponseParser
{
    public static SerpResponse ParseSerp(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = RequireObject(document.RootElement, "response");
        var responseError = GetStatusError(root, "response");
        if (responseError is not null)
            return new SerpResponse(responseError, false, null, null);

        if (!root.TryGetProperty("tasks", out var tasks))
            return new SerpResponse(null, true, null, null);
        RequireArray(tasks, "tasks");
        if (tasks.GetArrayLength() == 0)
            return new SerpResponse(null, true, null, null);

        var task = RequireObject(tasks[0], "tasks[0]");
        var taskError = GetStatusError(task, "task");
        if (taskError is not null)
            return new SerpResponse(null, false, taskError, null);

        if (!task.TryGetProperty("result", out var result))
            return new SerpResponse(null, false, null, []);
        RequireArray(result, "tasks[0].result");
        if (result.GetArrayLength() == 0)
            return new SerpResponse(null, false, null, []);

        var firstResult = RequireObject(result[0], "tasks[0].result[0]");
        if (!firstResult.TryGetProperty("items", out var items))
            return new SerpResponse(null, false, null, []);
        RequireArray(items, "tasks[0].result[0].items");

        var parsedItems = new List<SearchItem>();
        var itemIndex = 0;
        foreach (var itemElement in items.EnumerateArray())
        {
            var item = RequireObject(itemElement, $"items[{itemIndex}]");
            if (GetString(item, "type") == "organic")
                parsedItems.Add(new SearchItem(GetInt(item, "rank_group") ?? 0,
                    GetString(item, "title") ?? "", GetString(item, "url") ?? ""));
            itemIndex++;
        }

        return new SerpResponse(null, false, null, parsedItems);
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

    private static string? GetStatusError(JsonElement element, string context)
    {
        if (!element.TryGetProperty("status_code", out var status))
            return null;
        if (status.ValueKind != JsonValueKind.Number || !status.TryGetInt32(out var code))
            throw new JsonException($"Expected {context}.status_code to be an integer.");
        return code == 20000 ? null : GetString(element, "status_message") ?? $"{context} failed";
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

    internal sealed record SerpResponse(string? ErrorMessage, bool NoTasks, string? TaskErrorMessage, IReadOnlyList<SearchItem>? Results);
    internal sealed record SearchItem(int Position, string Title, string Url);
}
