using System.Net;
using OracleOfBatman.Ingest.ComicVine;
using OracleOfBatman.Ingest.Tests.Fakes;
using Xunit;

namespace OracleOfBatman.Ingest.Tests;

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
}
