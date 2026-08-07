namespace OracleOfBatman.Domain.Tests;

public class PathTests
{
  [Fact]
  public void BatmanNumber_IsHopCount_NotStoredSeparately()
  {
    var softServe = new Character(176719, "Soft Serve");
    var beast = new Character(15694, "Beast");
    var bloodscream = new Character(15734, "Bloodscream");

    var path = new Path(
      [softServe, beast, bloodscream],
      [
        new Hop(softServe, beast, new Issue(111, "Some Issue")),
        new Hop(beast, bloodscream, new Issue(222, "Some Other Issue"))
      ]);

    Assert.Equal(2, path.BatmanNumber);
  }

  [Fact]
  public void DirectConnection_HasBatmanNumberOne()
  {
    var jimHammond = new Character(12605, "Jim Hammond");
    var jeff = new Character(157242, "Jeff the Land Shark");

    var path = new Path(
      [jimHammond, jeff],
      [new Hop(jimHammond, jeff, new Issue(739613, "Some Issue"))]);

    Assert.Equal(1, path.BatmanNumber);
  }
}
