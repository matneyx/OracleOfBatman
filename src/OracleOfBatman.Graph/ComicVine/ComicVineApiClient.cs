namespace OracleOfBatman.Graph.ComicVine;

/// <summary>
///   Real, HTTP-backed IComicVineCharacterSource/IComicVineIssueSource. A single
///   `/character/{id}/` request returns issue_credits, character_friends, character_enemies,
///   site_detail_url, and image together (confirmed from real sample data) — so a character
///   fetch is exactly one request, matching ADR-0010's budget model. `/issue/{id}/` is a
///   separate request, only ever called to lazily enrich a displayed Path (ADR-0010/ADR-0011),
///   never during the crawl itself. Caller owns httpClient's lifetime and configures
///   BaseAddress (https://comicvine.gamespot.com/api/).
/// </summary>
public sealed class ComicVineApiClient(
  HttpClient httpClient,
  string apiKey,
  ComicVineRateLimiter characterRateLimiter,
  ComicVineRateLimiter issueRateLimiter,
  ComicVineRateLimiter searchRateLimiter) : IComicVineCharacterSource,
  IComicVineIssueSource, IComicVineCharacterSearchSource
{

  public async Task<IReadOnlyList<ComicVineSearchCharacterResult>> SearchCharactersAsync(string query)
  {
    await searchRateLimiter.WaitForSlotAsync();

    var requestUri =
      $"search/?api_key={apiKey}&format=xml&resources=character" +
      $"&query={Uri.EscapeDataString(query)}&field_list=id,name,site_detail_url,image";

    using var response = await httpClient.GetAsync(requestUri);
    response.EnsureSuccessStatusCode();

    await using var stream = await response.Content.ReadAsStreamAsync();
    return ComicVineXmlReader.ReadSearchResults(stream);
  }

  public async Task<ComicVineCharacter> GetCharacterAsync(int comicVineId)
  {
    await characterRateLimiter.WaitForSlotAsync();

    var requestUri =
      $"character/4005-{comicVineId}/?api_key={apiKey}&format=xml" +
      "&field_list=id,name,site_detail_url,image,character_friends,character_enemies,issue_credits";

    using var response = await httpClient.GetAsync(requestUri);
    response.EnsureSuccessStatusCode();

    await using var stream = await response.Content.ReadAsStreamAsync();
    return ComicVineXmlReader.ReadCharacter(stream);
  }

  public async Task<ComicVineIssue> GetIssueAsync(int comicVineId)
  {
    await issueRateLimiter.WaitForSlotAsync();

    var requestUri =
      $"issue/4000-{comicVineId}/?api_key={apiKey}&format=xml" +
      "&field_list=id,name,cover_date,image,volume";

    using var response = await httpClient.GetAsync(requestUri);
    response.EnsureSuccessStatusCode();

    await using var stream = await response.Content.ReadAsStreamAsync();
    return ComicVineXmlReader.ReadIssue(stream);
  }
}
