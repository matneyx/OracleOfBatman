using Bunit;
using MudBlazor.Services;
using OracleOfBatman.Domain;
using OracleOfBatman.Web.Components.Shared;
using Path = OracleOfBatman.Domain.Path;

namespace OracleOfBatman.Web.Tests;

public class PathTableTests : BunitContext
{
  public PathTableTests()
  {
    Services.AddMudServices();
  }

  [Fact]
  public void RendersOneBodyRowPerHop()
  {
    var jason = new Character(1, "Jason Voorhees");
    var freddy = new Character(2, "Freddy Krueger");
    var batman = new Character(1699, "Batman");
    var path = new Path(
      [jason, freddy, batman],
      [
        new Hop(jason, freddy, new Issue(10, "Freddy vs. Jason vs. Ash")),
        new Hop(freddy, batman, new Issue(20, "Cracked")),
      ]);

    var cut = Render<PathTable>(p => p
      .Add(c => c.Path, path));

    Assert.Equal(2, cut.FindAll("tbody tr").Count);
  }

  [Fact]
  public void RendersFromIssueAndToCards_WithAConnectorCellBetweenEach()
  {
    // Each row reads as a sentence: From | connector | Issue | connector | To. The connector
    // cells' contents aren't pinned here — they're words today and meant to become icons.
    var jason = new Character(1, "Jason Voorhees");
    var batman = new Character(1699, "Batman");
    var path = new Path([jason, batman], [new Hop(jason, batman, new Issue(20, "Cracked"))]);

    var cut = Render<PathTable>(p => p
      .Add(c => c.Path, path));

    var cells = cut.FindAll("tbody tr td");
    Assert.Equal(5, cells.Count);
    Assert.NotNull(cells[0].QuerySelector("[data-testid='character-card-1']"));
    Assert.NotNull(cells[2].QuerySelector("[data-testid='issue-card-20']"));
    Assert.NotNull(cells[4].QuerySelector("[data-testid='character-card-1699']"));
  }
}
