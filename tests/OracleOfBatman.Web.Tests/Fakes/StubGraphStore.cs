using OracleOfBatman.Domain;
using OracleOfBatman.Graph;
using Path = OracleOfBatman.Domain.Path;

namespace OracleOfBatman.Web.Tests.Fakes;

/// <summary>
///   Minimal IGraphStore for Home.razor's rendering tests — only GetCharacterAsync is
///   configurable, since that's all this page's own tests need; everything else returns a
///   harmless default rather than implementing the full decision logic FakeGraphStore
///   (OracleOfBatman.Graph.Tests) already covers.
/// </summary>
public sealed class StubGraphStore(Character? characterToReturn = null) : IGraphStore
{
  public Task<bool> PathExistsAsync(int characterAComicVineId, int characterBComicVineId) => Task.FromResult(false);

  public Task<Path?> FindShortestPathAsync(int characterAComicVineId, int characterBComicVineId, int maxDepth) =>
    Task.FromResult<Path?>(null);

  public async Task RecordSeedUseAsync(int characterAComicVineId, int characterBComicVineId) => throw new NotImplementedException();

  public Task UpsertCharacterAsync(Character character) => Task.CompletedTask;

  public Task<Character?> GetCharacterAsync(int comicVineId) =>
    Task.FromResult(characterToReturn?.ComicVineId == comicVineId ? characterToReturn : null);

  public async Task UpsertCreditedInAsync(int comicVineCharacterId, IReadOnlyList<Issue> issueCredits) => throw new NotImplementedException();

  public Task<Issue?> GetIssueAsync(int comicVineId) => Task.FromResult<Issue?>(null);

  public Task UpsertIssueAsync(Issue issue) => Task.CompletedTask;

  public Task<IReadOnlyList<Character>> SearchCharactersAsync(string query, int limit = 20) =>
    Task.FromResult<IReadOnlyList<Character>>([]);

  public async Task<Character?> GetLeastRecentlyIngestedCharacterAsync(IReadOnlyCollection<int> excludedIds) => throw new NotImplementedException();
}
