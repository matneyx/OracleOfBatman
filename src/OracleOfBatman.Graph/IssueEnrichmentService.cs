using OracleOfBatman.Domain;
using OracleOfBatman.Graph.ComicVine;

namespace OracleOfBatman.Graph;

public class IssueEnrichmentService(IComicVineIssueSource issueSource, IGraphStore graphStore)
{

  public async Task<Issue> EnrichIfNeededAsync(Issue issue)
  {
    if (!string.IsNullOrEmpty(issue.ImageUrl))
    {
      return issue;
    }

    try
    {
      // Fetch
      var sourceIssue = await issueSource.GetIssueAsync(issue.ComicVineId);

      // update incoming issue
      issue.Name ??= sourceIssue.Name;
      issue.ImageUrl ??= sourceIssue.Image?.IconUrl;
      issue.SiteDetailUrl ??= sourceIssue.SiteDetailUrl;
      issue.CharacterCredits = [.. sourceIssue.CharacterCredits.Select(c => c.Id)];

      if (sourceIssue.Volume != null)
      {
        issue.VolumeId = sourceIssue.Volume.Id;
        issue.VolumeName = sourceIssue.Volume.Name;
      }

      // Update
      await graphStore.UpsertIssueAsync(issue);

      return issue;
    }
    catch(HttpRequestException _) // TODO: Log this if we ever go live
    {
      return issue;
    }

  }
}
