using OracleOfBatman.Graph.ComicVine;

namespace OracleOfBatman.Graph.Tests.Fakes;

/// <summary>In-memory IComicVineIssueSource backed by a hand-built issue map, so
/// ConnectionCrawler tests don't depend on real HTTP.</summary>
public sealed class FakeComicVineIssueSource(Dictionary<int, ComicVineIssue> issues, IReadOnlySet<int>? failingIds = null) : IComicVineIssueSource
{
    private readonly List<int> _fetchedIds = [];

    public IReadOnlyList<int> FetchedIds => _fetchedIds;

    public Task<ComicVineIssue> GetIssueAsync(int comicVineId)
    {
        _fetchedIds.Add(comicVineId);

        // Simulates a real Comic Vine failure (rate limit, network blip, ...) —
        // ComicVineApiClient.GetIssueAsync throws HttpRequestException in that case.
        if (failingIds?.Contains(comicVineId) == true)
        {
            throw new HttpRequestException($"Simulated Comic Vine failure for issue {comicVineId}");
        }

        if (!issues.TryGetValue(comicVineId, out var issue))
        {
            throw new KeyNotFoundException($"No fake issue registered for Comic Vine id {comicVineId}");
        }

        return Task.FromResult(issue);
    }
}
