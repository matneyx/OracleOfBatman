using Neo4j.Driver;
using OracleOfBatman.Domain;
using Testcontainers.Neo4j;

namespace OracleOfBatman.Graph.Tests;

[Trait("Category", "Integration")]
public sealed class CharacterSearchTests : IAsyncLifetime
{
  private readonly Neo4jContainer _container = new Neo4jBuilder("neo4j:5").Build();
  private IDriver _driver = null!;
  private Neo4jGraphWriter _writer = null!;

  public async Task InitializeAsync()
  {
    await _container.StartAsync();
    _driver = GraphDatabase.Driver(_container.GetConnectionString(), AuthTokens.None);
    _writer = new Neo4jGraphWriter(_driver);
  }

  public async Task DisposeAsync()
  {
    await _driver.DisposeAsync();
    await _container.DisposeAsync();
  }

  [Fact]
  public async Task SearchCharacters_MatchesCaseInsensitiveSubstring()
  {
    await _writer.UpsertCharacterAsync(new Character(176719, "Soft Serve"));
    await _writer.UpsertCharacterAsync(new Character(15734, "Bloodscream"));

    var results = await _writer.SearchCharactersAsync("soft");

    var match = Assert.Single(results);
    Assert.Equal(new Character(176719, "Soft Serve"), match);
  }

  [Fact]
  public async Task SearchCharacters_ReturnsEmpty_WhenNoMatch()
  {
    await _writer.UpsertCharacterAsync(new Character(176719, "Soft Serve"));

    var results = await _writer.SearchCharactersAsync("wolverine");

    Assert.Empty(results);
  }

  [Fact]
  public async Task SearchCharacters_MatchesMidNameSubstring()
  {
    await _writer.UpsertCharacterAsync(new Character(1462, "Beast"));

    var results = await _writer.SearchCharactersAsync("eas");

    Assert.Contains(results, c => c.ComicVineId == 1462);
  }

  [Fact]
  public async Task SearchCharacters_RespectsLimit()
  {
    for (var i = 0; i < 5; i++)
    {
      await _writer.UpsertCharacterAsync(new Character(i, $"Spider-Match{i}"));
    }

    var results = await _writer.SearchCharactersAsync("Spider", 3);

    Assert.Equal(3, results.Count);
  }

  [Fact]
  public async Task SearchCharacters_OrdersResultsAlphabetically()
  {
    await _writer.UpsertCharacterAsync(new Character(1, "Ziggy Spider"));
    await _writer.UpsertCharacterAsync(new Character(2, "Amazing Spider"));

    var results = await _writer.SearchCharactersAsync("Spider");

    Assert.Equal(["Amazing Spider", "Ziggy Spider"], results.Select(c => c.Name));
  }

  [Fact]
  public async Task SearchCharacters_ReturnsImageUrlAndSiteDetailUrl()
  {
    await _writer.UpsertCharacterAsync(new Character(176719, "Soft Serve", imageUrl: "https://example.com/soft-serve-icon.jpg",
      siteDetailUrl: "https://comicvine.gamespot.com/soft-serve/4005-176719/"));

    var results = await _writer.SearchCharactersAsync("soft");

    var match = Assert.Single(results);
    Assert.Equal("https://example.com/soft-serve-icon.jpg", match.ImageUrl);
    Assert.Equal("https://comicvine.gamespot.com/soft-serve/4005-176719/", match.SiteDetailUrl);
  }
}
