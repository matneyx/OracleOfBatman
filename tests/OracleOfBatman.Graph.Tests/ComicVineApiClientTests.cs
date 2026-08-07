using System.Net;
using OracleOfBatman.Graph.ComicVine;
using OracleOfBatman.Graph.Tests.Fakes;

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
    var characterRateLimiter = new ComicVineRateLimiter(2, TimeSpan.FromMinutes(1));
    var issueRateLimiter = new ComicVineRateLimiter(2, TimeSpan.FromMinutes(1));
    var searchRateLimiter = new ComicVineRateLimiter(2, TimeSpan.FromMinutes(1));
    var client = new ComicVineApiClient(httpClient, "test-key", characterRateLimiter, issueRateLimiter,
      searchRateLimiter);

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

    var characterRateLimiter = new ComicVineRateLimiter(2, TimeSpan.FromMinutes(1));
    var issueRateLimiter = new ComicVineRateLimiter(2, TimeSpan.FromMinutes(1));
    var searchRateLimiter = new ComicVineRateLimiter(2, TimeSpan.FromMinutes(1));
    var client = new ComicVineApiClient(httpClient, "test-key", characterRateLimiter, issueRateLimiter,
      searchRateLimiter);

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
    var characterRateLimiter = new ComicVineRateLimiter(2, TimeSpan.FromMinutes(1));
    var issueRateLimiter = new ComicVineRateLimiter(2, TimeSpan.FromMinutes(1));
    var searchRateLimiter = new ComicVineRateLimiter(2, TimeSpan.FromMinutes(1));
    var client = new ComicVineApiClient(httpClient, "test-key", characterRateLimiter, issueRateLimiter,
      searchRateLimiter);

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
    var characterRateLimiter = new ComicVineRateLimiter(2, TimeSpan.FromMinutes(1));
    var issueRateLimiter = new ComicVineRateLimiter(2, TimeSpan.FromMinutes(1));
    var searchRateLimiter = new ComicVineRateLimiter(2, TimeSpan.FromMinutes(1));
    var client = new ComicVineApiClient(httpClient, "test-key", characterRateLimiter, issueRateLimiter,
      searchRateLimiter);

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
    var characterRateLimiter = new ComicVineRateLimiter(2, TimeSpan.FromMinutes(1));
    var issueRateLimiter = new ComicVineRateLimiter(2, TimeSpan.FromMinutes(1));
    var searchRateLimiter = new ComicVineRateLimiter(2, TimeSpan.FromMinutes(1));
    var client = new ComicVineApiClient(httpClient, "test-key", characterRateLimiter, issueRateLimiter,
      searchRateLimiter);

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

  [Fact]
  public async Task GetCharacterAsync_IsRateLimitedIndependentlyPerResource()
  {
    // Comic Vine's limit is 200 requests/resource/hour (ADR-0004), not a single shared
    // pool across the whole API key — exhausting the character resource's budget must not
    // block the issue resource, and vice versa.
    var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, CharacterXml);
    using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://comicvine.gamespot.com/api/") };
    var characterRateLimiter = new ComicVineRateLimiter(1, TimeSpan.FromMinutes(1));
    var issueRateLimiter = new ComicVineRateLimiter(1, TimeSpan.FromMinutes(1));
    var searchRateLimiter = new ComicVineRateLimiter(1, TimeSpan.FromMinutes(1));
    var client = new ComicVineApiClient(httpClient, "test-key", characterRateLimiter, issueRateLimiter,
      searchRateLimiter);

    await client.GetCharacterAsync(157242); // consumes the character resource's only slot

    var secondCharacterCall = client.GetCharacterAsync(12605);
    var secondCharacterCompleted = await Task.WhenAny(secondCharacterCall, Task.Delay(TimeSpan.FromMilliseconds(200)));
    Assert.NotSame(secondCharacterCall, secondCharacterCompleted);

    // A different resource (issue) must not be throttled by the character resource's
    // exhausted limit.
    var issueCall = client.GetIssueAsync(739613);
    var issueCallCompleted = await Task.WhenAny(issueCall, Task.Delay(TimeSpan.FromMilliseconds(200)));
    Assert.Same(issueCall, issueCallCompleted);
  }
}
