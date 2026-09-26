using System.Text.Json;
using Xunit;

public sealed class DataForSeoResponseParserTests
{
    [Fact]
    public void TryParseSerpOptions_AcceptsQuotedPositionalPhrase()
    {
        var success = Accu.TryParseSerpOptions(["air conditioner repair", "--location", "Savoy, IL"],
            out var options, out var error);

        Assert.True(success, error);
        Assert.Equal("air conditioner repair", options.Query);
        Assert.Equal("Savoy, IL", options.Location);
    }

    [Fact]
    public void TryParseSerpOptions_AcceptsUnquotedPositionalPhrase()
    {
        var success = Accu.TryParseSerpOptions(["air", "conditioner", "repair", "--location", "Savoy, IL"],
            out var options, out var error);

        Assert.True(success, error);
        Assert.Equal("air conditioner repair", options.Query);
    }

    [Fact]
    public void TryParseSerpOptions_PreservesQueryOptionForm()
    {
        var success = Accu.TryParseSerpOptions(["--query", "air conditioner repair", "--location", "Savoy, IL"],
            out var options, out var error);

        Assert.True(success, error);
        Assert.Equal("air conditioner repair", options.Query);
    }

    [Fact]
    public void GetDisplayUrl_KeepsHostAndPathButOmitsQueryAndFragment()
    {
        var displayUrl = Accu.GetDisplayUrl(new Uri("https://example.com/private/path?token=secret#section"));

        Assert.Equal("https://example.com/private/path", displayUrl);
        Assert.DoesNotContain("secret", displayUrl);
        Assert.DoesNotContain("section", displayUrl);
    }

    [Fact]
    public void ParseSerp_ReturnsOnlyOrganicResults()
    {
        const string json = """
            {"status_code":20000,"tasks":[{"status_code":20000,"result":[{"items":[
              {"type":"organic","rank_group":3,"title":"Example","url":"https://example.com/page"},
              {"type":"paid","rank_group":1,"title":"Ad","url":"https://ads.example/"}
            ]}]}]}
            """;

        var parsed = DataForSeoResponseParser.ParseSerp(json);

        Assert.Equal(new DataForSeoResponseParser.SearchItem(3, "Example", "https://example.com/page"),
            Assert.Single(parsed.Results!));
    }

    [Theory]
    [InlineData("{\"tasks\":{}}", "tasks")]
    [InlineData("{\"status_code\":\"20000\",\"tasks\":[]}", "status_code")]
    [InlineData("{\"tasks\":[{\"result\":[{\"items\":{}}]}]}", "items")]
    public void ParseSerp_RejectsUnexpectedJsonTypes(string json, string expectedPath)
    {
        var error = Assert.Throws<JsonException>(() => DataForSeoResponseParser.ParseSerp(json));
        Assert.Contains(expectedPath, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseSerp_ReturnsProviderErrorMessage()
    {
        var parsed = DataForSeoResponseParser.ParseSerp("""{"status_code":40100,"status_message":"Not authorized"}""");
        Assert.Equal("Not authorized", parsed.ErrorMessage);
    }

    [Fact]
    public void ParseLocationCodes_ReturnsMatchingCityCode()
    {
        const string json = """
            {"tasks":[{"result":[
              {"location_name":"Savoy,Illinois,United States","location_type":"City","country_iso_code":"US","location_code":101},
              {"location_name":"Savoy,Illinois,United States","location_type":"State","country_iso_code":"US","location_code":102}
            ]}]}
            """;

        Assert.Equal(101, Assert.Single(DataForSeoResponseParser.ParseLocationCodes(json, "Savoy", "Illinois")!));
    }

    [Fact]
    public void ParseLocationCodes_RejectsUnexpectedResultType()
    {
        var error = Assert.Throws<JsonException>(() =>
            DataForSeoResponseParser.ParseLocationCodes("""{"tasks":[{"result":{}}]}""", "Savoy", "Illinois"));
        Assert.Contains("result", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
