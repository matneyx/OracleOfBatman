using Neo4j.Driver;
using OracleOfBatman.Graph;
using OracleOfBatman.Graph.ComicVine;

// Runs the ADR-0010 crawl for two seed Characters, populating Neo4j with whatever
// Characters/Connections it discovers. --seed-id takes a Comic Vine character id directly;
// name-based lookup (MVP.md ticket 5's original --seed <name>) is a deferred follow-up.

var seedIds = new List<int>();
var budget = 200;

for (var i = 0; i < args.Length; i++)
{
  switch (args[i])
  {
    case "--seed-id":
      seedIds.Add(int.Parse(args[++i]));
      break;
    case "--budget":
      budget = int.Parse(args[++i]);
      break;
  }
}

if (seedIds.Count != 2)
{
  Console.Error.WriteLine("Usage: --seed-id <comicVineId> --seed-id <comicVineId> [--budget <maxNewCharacters>]");
  return 1;
}

var comicVineApiKey = Environment.GetEnvironmentVariable("COMIC_VINE_API_KEY")
  ?? throw new InvalidOperationException("COMIC_VINE_API_KEY is required.");
var neo4jUri = Environment.GetEnvironmentVariable("NEO4J_URI") ?? "bolt://localhost:7687";
var neo4jUsername = Environment.GetEnvironmentVariable("NEO4J_USERNAME") ?? "neo4j";
var neo4jPassword = Environment.GetEnvironmentVariable("NEO4J_PASSWORD") ?? "changeme";
var neo4jDatabase = Environment.GetEnvironmentVariable("NEO4J_DATABASE");

using var httpClient = new HttpClient { BaseAddress = new Uri("https://comicvine.gamespot.com/api/") };
httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("OracleOfBatman/0.1 (+https://github.com/matneyx/OracleOfBatman)");

var characterRateLimiter = new ComicVineRateLimiter(200, TimeSpan.FromHours(1));
var issueRateLimiter = new ComicVineRateLimiter(200, TimeSpan.FromHours(1));
var searchRateLimiter = new ComicVineRateLimiter(200, TimeSpan.FromHours(1));
var comicVineSource = new ComicVineApiClient(httpClient, comicVineApiKey, characterRateLimiter, issueRateLimiter,
  searchRateLimiter);

await using var driver = GraphDatabase.Driver(neo4jUri, AuthTokens.Basic(neo4jUsername, neo4jPassword));
var graphStore = new Neo4jGraphWriter(driver, neo4jDatabase);
var issueEnrichmentService = new IssueEnrichmentService(comicVineSource, graphStore);

await graphStore.EnsureSchemaAsync();

var crawler = new ConnectionCrawler(comicVineSource, graphStore, issueEnrichmentService,
  trace: Console.WriteLine);

var token = new CancellationTokenSource(TimeSpan.FromMinutes(30)).Token;

var result = await crawler.PopulateConnectionsAsync(seedIds[0], seedIds[1], budget, token);

Console.WriteLine(result.Connected
  ? $"Connected after fetching {result.CharactersFetched} new character(s) and {result.IssuesFetched} issue cast(s)."
  : $"Not connected after fetching {result.CharactersFetched} new character(s) and {result.IssuesFetched} issue cast(s) — budget ({budget}) exhausted or every signal exhausted first.");

var characterCount = await graphStore.GetSummaryAsync();
Console.WriteLine($"Neo4j now has {characterCount} Character(s) total.");

return 0;
