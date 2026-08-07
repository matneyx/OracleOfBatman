using OracleOfBatman.Domain;
using OracleOfBatman.Graph.ComicVine;
using OracleOfBatman.Graph.Tests.Fakes;

namespace OracleOfBatman.Graph.Tests;

/// <summary>
///   ADR-0015's unified enrichment fetch: one Comic Vine /issue/{id}/ request per Issue,
///   triggered by ImageUrl being unknown, populating image/Volume/name together and writing
///   the result back so it's never re-fetched. Replaces the two separate, uncoordinated
///   mechanisms (Volume/TPB-name fallback during ingest, thumbnail lazy-fetch in Home.razor)
///   that used to independently hit the same endpoint.
/// </summary>
public class IssueEnrichmentServiceTests
{
  [Fact]
  public async Task EnrichIfNeededAsync_ReturnsIssueUnchanged_WhenImageUrlAlreadyKnown()
  {
    var issue = new Issue(500, "Some Issue", "https://example.com/already-known.jpg");
    var issueSource = new FakeComicVineIssueSource([]);
    var graphStore = new FakeGraphStore();
    var service = new IssueEnrichmentService(issueSource, graphStore);

    var result = await service.EnrichIfNeededAsync(issue);

    Assert.Equal(issue, result);
    Assert.Empty(issueSource.FetchedIds);
  }

  [Fact]
  public async Task EnrichIfNeededAsync_FetchesAndPersistsImageVolumeAndSiteDetailUrl_WhenImageUrlIsNull()
  {
    var issue = new Issue(500, "Some Issue");
    var comicVineIssue = new ComicVineIssue
    {
      Id = 500,
      Name = "Some Issue",
      Image = new ComicVineImage { IconUrl = "https://example.com/cover.jpg" },
      SiteDetailUrl = "https://comicvine.gamespot.com/some-issue/4000-500/",
      Volume = new ComicVineVolume { Id = 9, Name = "The Volume Title" }
    };
    var issueSource = new FakeComicVineIssueSource(new Dictionary<int, ComicVineIssue> { [500] = comicVineIssue });
    var graphStore = new FakeGraphStore();
    var service = new IssueEnrichmentService(issueSource, graphStore);

    var result = await service.EnrichIfNeededAsync(issue);

    Assert.Equal("https://example.com/cover.jpg", result.ImageUrl);
    Assert.Equal(9, result.VolumeId);
    Assert.Equal("The Volume Title", result.VolumeName);
    Assert.Equal("https://comicvine.gamespot.com/some-issue/4000-500/", result.SiteDetailUrl);

    // Written back so nobody ever needs to fetch this issue again (ADR-0015).
    var persisted = await graphStore.GetIssueAsync(500);
    Assert.Equal(result, persisted);
  }

  [Fact]
  public async Task EnrichIfNeededAsync_FillsInNameOnlyWhenItWasBlank()
  {
    var issue = new Issue(500, null); // e.g. a TPB/omnibus with no name of its own yet
    var comicVineIssue = new ComicVineIssue
    {
      Id = 500,
      Name = "TPB",
      Image = new ComicVineImage { IconUrl = "https://example.com/cover.jpg" }
    };
    var issueSource = new FakeComicVineIssueSource(new Dictionary<int, ComicVineIssue> { [500] = comicVineIssue });
    var graphStore = new FakeGraphStore();
    var service = new IssueEnrichmentService(issueSource, graphStore);

    var result = await service.EnrichIfNeededAsync(issue);

    Assert.Equal("TPB", result.Name);
  }

  [Fact]
  public async Task EnrichIfNeededAsync_KeepsTheExistingNameWhenAlreadyKnown()
  {
    var issue = new Issue(500, "A Real Issue Name");
    var comicVineIssue = new ComicVineIssue
    {
      Id = 500,
      Name = "A Different Name From The Fetch",
      Image = new ComicVineImage { IconUrl = "https://example.com/cover.jpg" }
    };
    var issueSource = new FakeComicVineIssueSource(new Dictionary<int, ComicVineIssue> { [500] = comicVineIssue });
    var graphStore = new FakeGraphStore();
    var service = new IssueEnrichmentService(issueSource, graphStore);

    var result = await service.EnrichIfNeededAsync(issue);

    Assert.Equal("A Real Issue Name", result.Name);
  }

  [Fact]
  public async Task EnrichIfNeededAsync_StillEnrichesNameVolumeAndSiteDetailUrl_WhenTheIssueHasNoCoverImage()
  {
    // Some Comic Vine issues genuinely have no cover image — that must not silently
    // abort enrichment of everything else (the actual bug behind the missing IssueCard
    // link: a NullReferenceException on the image was getting swallowed by a catch that
    // was only ever meant to guard against Comic Vine failures, not real bugs).
    var issue = new Issue(500, "Some Issue");
    var comicVineIssue = new ComicVineIssue
    {
      Id = 500,
      Name = "Some Issue",
      Image = null,
      SiteDetailUrl = "https://comicvine.gamespot.com/some-issue/4000-500/",
      Volume = new ComicVineVolume { Id = 9, Name = "The Volume Title" }
    };
    var issueSource = new FakeComicVineIssueSource(new Dictionary<int, ComicVineIssue> { [500] = comicVineIssue });
    var graphStore = new FakeGraphStore();
    var service = new IssueEnrichmentService(issueSource, graphStore);

    var result = await service.EnrichIfNeededAsync(issue);

    Assert.Equal("https://comicvine.gamespot.com/some-issue/4000-500/", result.SiteDetailUrl);
    Assert.Equal("The Volume Title", result.VolumeName);
  }

  [Fact]
  public async Task EnrichIfNeededAsync_ReturnsTheOriginalIssueUnchanged_WhenComicVineFails()
  {
    // Same resilience concern as the old ResolveIssueNameAsync (ADR-0010 incident) — a
    // rate-limit/network blip while rendering must not crash the page, just leave the
    // issue un-enriched for now so a later render can retry.
    var issue = new Issue(500, "Some Issue");
    var issueSource = new FakeComicVineIssueSource([], new HashSet<int> { 500 });
    var graphStore = new FakeGraphStore();
    var service = new IssueEnrichmentService(issueSource, graphStore);

    var result = await service.EnrichIfNeededAsync(issue);

    Assert.Equal(issue, result);
  }
}
