namespace OracleOfBatman.Ingest.ComicVine;

/// <summary>
/// Real, HTTP-backed IComicVineCharacterSource. A single `/character/{id}/` request
/// returns issue_credits, character_friends, and character_enemies together (confirmed
/// from real sample data) — so this is exactly one request per character, matching
/// ADR-0010's budget model. Caller owns httpClient's lifetime and configures BaseAddress
/// (https://comicvine.gamespot.com/api/).
/// </summary>
public sealed class ComicVineApiClient(HttpClient httpClient, string apiKey) : IComicVineCharacterSource
{
    public async Task<ComicVineCharacter> GetCharacterAsync(int comicVineId)
    {
        var requestUri =
            $"character/4005-{comicVineId}/?api_key={apiKey}&format=xml" +
            "&field_list=id,name,character_friends,character_enemies,issue_credits";

        using var response = await httpClient.GetAsync(requestUri);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        return ComicVineXmlReader.ReadCharacter(stream);
    }
}
