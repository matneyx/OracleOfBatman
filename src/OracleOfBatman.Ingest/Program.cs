using Neo4j.Driver;
using OracleOfBatman.Domain;
using OracleOfBatman.Ingest;
using OracleOfBatman.Ingest.ComicVine;

// One-off seed from cached sample responses (docs/raw-api-responses), not a live crawl —
// proves the Domain types + Neo4j write path end-to-end before ADR-0007's real crawl
// (rate-limited API calls, bidirectional expansion) gets built.

var samplesDir = Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "docs", "raw-api-responses");

var jimHammond = ComicVineXmlReader.ReadCharacter(Path.Combine(samplesDir, "jim-hammond.xml"));
var jeff = ComicVineXmlReader.ReadCharacter(Path.Combine(samplesDir, "jeff-the-land-shark.xml"));
var issue = ComicVineXmlReader.ReadIssue(Path.Combine(samplesDir, "issue-example.xml"));

var neo4jUri = Environment.GetEnvironmentVariable("NEO4J_URI") ?? "bolt://localhost:7687";
var neo4jUsername = Environment.GetEnvironmentVariable("NEO4J_USERNAME") ?? "neo4j";
var neo4jPassword = Environment.GetEnvironmentVariable("NEO4J_PASSWORD") ?? "changeme";
var neo4jDatabase = Environment.GetEnvironmentVariable("NEO4J_DATABASE");

await using var driver = GraphDatabase.Driver(neo4jUri, AuthTokens.Basic(neo4jUsername, neo4jPassword));
var writer = new Neo4jGraphWriter(driver, neo4jDatabase);

await writer.UpsertCharacterAsync(jimHammond.ToDomain());
await writer.UpsertCharacterAsync(jeff.ToDomain());
foreach (var character in issue.CharacterCredits)
{
    await writer.UpsertCharacterAsync(character.ToDomain());
}

var publishedAt = DateOnly.TryParse(issue.CoverDate, out var coverDate) ? coverDate : (DateOnly?)null;

// Comic Vine's same-issue co-occurrence can't tell us the real Interaction Tier (ADR-0007)
// — SharedScene is the pragmatic default guess pending human curation, not Direct
// Interaction (too strong a claim for "merely credited on the same issue").
for (var i = 0; i < issue.CharacterCredits.Count; i++)
{
    for (var j = i + 1; j < issue.CharacterCredits.Count; j++)
    {
        var connection = new Connection(
            issue.CharacterCredits[i].Id,
            issue.CharacterCredits[j].Id,
            issue.Id,
            publishedAt,
            InteractionTier.SharedScene,
            Confidence.Unverified);
        await writer.UpsertConnectionAsync(connection);
    }
}

var (characterCount, connectionCount) = await writer.GetSummaryAsync();
Console.WriteLine($"Neo4j now has {characterCount} Character(s) and {connectionCount} Connection(s).");

static string FindRepoRoot(string startDirectory)
{
    var directory = new DirectoryInfo(startDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OracleOfBatman.slnx")))
    {
        directory = directory.Parent;
    }

    return directory?.FullName
        ?? throw new InvalidOperationException("Could not locate repo root (OracleOfBatman.slnx not found above " + startDirectory + ").");
}
