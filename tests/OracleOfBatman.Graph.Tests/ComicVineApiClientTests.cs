using System.Net;
using OracleOfBatman.Graph.ComicVine;
using OracleOfBatman.Graph.Tests.Fakes;
using Xunit;

namespace OracleOfBatman.Graph.Tests;

public class ComicVineApiClientTests
{
    private const string CharacterXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <response>
            <results>
                <id>157242</id>
                <name><![CDATA[Jeff the Land Shark]]></name>
                <character_friends>
                    <character>
                        <id>1475</id>
                        <name><![CDATA[Hawkeye]]></name>
                    </character>
                </character_friends>
                <character_enemies/>
                <issue_credits/>
            </results>
        </response>
        """;

    [Fact]
    public async Task GetCharacterAsync_ParsesTheResponse()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, CharacterXml);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://comicvine.gamespot.com/api/") };
        var client = new ComicVineApiClient(httpClient, apiKey: "test-key");

        var character = await client.GetCharacterAsync(157242);

        Assert.Equal(157242, character.Id);
        Assert.Equal("Jeff the Land Shark", character.Name);
        Assert.Equal(1475, Assert.Single(character.CharacterFriends).Id);
    }

    [Fact]
    public async Task GetCharacterAsync_RequestsXmlFormatWithApiKeyAndCharacterId()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, CharacterXml, request => capturedRequest = request);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://comicvine.gamespot.com/api/") };
        var client = new ComicVineApiClient(httpClient, apiKey: "test-key");

        await client.GetCharacterAsync(157242);

        Assert.NotNull(capturedRequest);
        var requestUri = capturedRequest!.RequestUri!.ToString();
        Assert.Contains("character/4005-157242", requestUri);
        Assert.Contains("api_key=test-key", requestUri);
        Assert.Contains("format=xml", requestUri);
    }

    [Fact]
    public async Task GetCharacterAsync_ThrowsOnNonSuccessStatusCode()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.TooManyRequests, "");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://comicvine.gamespot.com/api/") };
        var client = new ComicVineApiClient(httpClient, apiKey: "test-key");

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetCharacterAsync(157242));
    }

    [Fact]
    public async Task GetIssueAsync_RequestsXmlFormatWithApiKeyAndIssueId()
    {
        const string issueXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <response>
                <results>
                    <id>739613</id>
                    <name><![CDATA[Some Issue]]></name>
                </results>
            </response>
            """;
        HttpRequestMessage? capturedRequest = null;
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, issueXml, request => capturedRequest = request);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://comicvine.gamespot.com/api/") };
        var client = new ComicVineApiClient(httpClient, apiKey: "test-key");

        var issue = await client.GetIssueAsync(739613);

        Assert.Equal(739613, issue.Id);
        Assert.NotNull(capturedRequest);
        var requestUri = capturedRequest!.RequestUri!.ToString();
        Assert.Contains("issue/4000-739613", requestUri);
        Assert.Contains("api_key=test-key", requestUri);
    }

    [Fact]
    public async Task SearchCharactersAsync_RequestsResourceFilteredSearchAndParsesResults()
    {
        const string searchXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <response>
                <results>
                    <character>
                        <id>46793</id>
                        <name><![CDATA[BloodRayne]]></name>
                        <site_detail_url><![CDATA[https://comicvine.gamespot.com/bloodrayne/4005-46793/]]></site_detail_url>
                    </character>
                </results>
            </response>
            """;
        HttpRequestMessage? capturedRequest = null;
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, searchXml, request => capturedRequest = request);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://comicvine.gamespot.com/api/") };
        var client = new ComicVineApiClient(httpClient, apiKey: "test-key");

        var results = await client.SearchCharactersAsync("blood rayne");

        var match = Assert.Single(results);
        Assert.Equal(46793, match.Id);
        Assert.Equal("BloodRayne", match.Name);
        Assert.NotNull(capturedRequest);
        var requestUri = capturedRequest!.RequestUri!.ToString();
        Assert.Contains("search/", requestUri);
        Assert.Contains("resources=character", requestUri);
        Assert.Contains("api_key=test-key", requestUri);
        Assert.Contains("blood rayne", Uri.UnescapeDataString(requestUri));
    }
}
