namespace OracleOfBatman.Domain.Tests;

public class IssueTests
{
  [Fact]
  public void IngestionDate_DefaultsToNull()
  {
    // A stub Issue (id + name only, gleaned free from a Character's own credit list) hasn't
    // actually been enriched yet — no ingestion date until that happens (ADR-0016).
    var issue = new Issue(111, "Some Issue");

    Assert.Null(issue.IngestionDateTime);
  }

  [Fact]
  public void IngestionDate_CanBeSet()
  {
    var controlDateTime = DateTime.Now;

    var issue = new Issue(111, "Some Issue", ingestionDateTime: controlDateTime);

    Assert.Equal(controlDateTime, issue.IngestionDateTime);
  }

  [Fact]
  public void CharacterCredits_DefaultsToEmpty()
  {
    // The raw Comic Vine cast list (ADR-0016) — populated at enrichment, not stub-creation.
    var issue = new Issue(111, "Some Issue");

    Assert.Empty(issue.CharacterCredits);
  }

  [Fact]
  public void CharacterCredits_CanBeSet()
  {
    var issue = new Issue(111, "Some Issue", characterCredits: [12605, 157242]);

    Assert.Equal([12605, 157242], issue.CharacterCredits);
  }

  [Fact]
  public void PathUseCount_DefaultsToZero()
  {
    var issue = new Issue(111, "Some Issue");

    Assert.Equal(0, issue.PathUseCount);
  }

  [Fact]
  public void PathUseCount_CanBeSet()
  {
    var issue = new Issue(111, "Some Issue", pathUseCount: 4);

    Assert.Equal(4, issue.PathUseCount);
  }
}
