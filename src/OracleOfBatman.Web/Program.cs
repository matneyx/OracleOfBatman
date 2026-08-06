using MudBlazor.Services;
using Neo4j.Driver;
using OracleOfBatman.Graph;
using OracleOfBatman.Graph.ComicVine;
using OracleOfBatman.Web.Components;

LoadDotEnvIfPresent();

var builder = WebApplication.CreateBuilder(args);

// Add MudBlazor services
builder.Services.AddMudServices();

// Add services to the container.
builder.Services.AddRazorComponents()
  .AddInteractiveServerComponents();

var neo4jUri = Environment.GetEnvironmentVariable("NEO4J_URI") ?? "bolt://localhost:7687";
var neo4jUsername = Environment.GetEnvironmentVariable("NEO4J_USERNAME") ?? "neo4j";
var neo4jPassword = Environment.GetEnvironmentVariable("NEO4J_PASSWORD") ?? "changeme";
var neo4jDatabase = Environment.GetEnvironmentVariable("NEO4J_DATABASE");

builder.Services.AddSingleton(GraphDatabase.Driver(neo4jUri, AuthTokens.Basic(neo4jUsername, neo4jPassword)));
builder.Services.AddScoped<IGraphStore>(sp => new Neo4jGraphWriter(sp.GetRequiredService<IDriver>(), neo4jDatabase));

var comicVineApiKey = Environment.GetEnvironmentVariable("COMIC_VINE_API_KEY");
if (comicVineApiKey is not null)
{
  builder.Services.AddHttpClient();
  builder.Services.AddScoped(sp =>
  {
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    httpClient.BaseAddress = new Uri("https://comicvine.gamespot.com/api/");
    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
      "OracleOfBatman/0.1 (+https://github.com/matneyx/OracleOfBatman)");

    var characterRateLimiter = new ComicVineRateLimiter(200, TimeSpan.FromHours(1));
    var issueRateLimiter = new ComicVineRateLimiter(200, TimeSpan.FromHours(1));
    var searchRateLimiter = new ComicVineRateLimiter(200, TimeSpan.FromHours(1));
    return new ComicVineApiClient(httpClient, comicVineApiKey, characterRateLimiter, issueRateLimiter, searchRateLimiter);
  });
  builder.Services.AddScoped<IComicVineCharacterSource>(sp => sp.GetRequiredService<ComicVineApiClient>());
  builder.Services.AddScoped<IComicVineIssueSource>(sp => sp.GetRequiredService<ComicVineApiClient>());
  builder.Services.AddScoped<IComicVineCharacterSearchSource>(sp => sp.GetRequiredService<ComicVineApiClient>());
  builder.Services.AddScoped(sp => new ConnectionCrawler(sp.GetRequiredService<IComicVineCharacterSource>(),
    sp.GetRequiredService<IComicVineIssueSource>(), sp.GetRequiredService<IGraphStore>()));
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
  app.UseExceptionHandler("/Error", true);
  // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
  app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
  .AddInteractiveServerRenderMode();

app.Run();

// Web is launched by external tooling that doesn't inherit an already-`source`d shell
// environment (unlike Ingest, run directly via `dotnet run` from a shell that has it) — so
// load .env ourselves if present, without overwriting real environment variables.
static void LoadDotEnvIfPresent()
{
  var directory = new DirectoryInfo(AppContext.BaseDirectory);
  while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OracleOfBatman.slnx")))
  {
    directory = directory.Parent;
  }

  var envPath = directory is null ? null : Path.Combine(directory.FullName, ".env");
  if (envPath is null || !File.Exists(envPath))
  {
    return;
  }

  foreach (var line in File.ReadAllLines(envPath))
  {
    var trimmed = line.Trim();
    if (trimmed.Length == 0 || trimmed.StartsWith('#'))
    {
      continue;
    }

    var separatorIndex = trimmed.IndexOf('=');
    if (separatorIndex < 0)
    {
      continue;
    }

    var key = trimmed[..separatorIndex].Trim();
    var value = trimmed[(separatorIndex + 1)..].Trim();
    if (Environment.GetEnvironmentVariable(key) is null)
    {
      Environment.SetEnvironmentVariable(key, value);
    }
  }
}
