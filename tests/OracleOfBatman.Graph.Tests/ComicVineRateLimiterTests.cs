using OracleOfBatman.Graph.ComicVine;

namespace OracleOfBatman.Graph.Tests;

/// <summary>
///   Guards against the incident this type exists to prevent: with no throttling at
///   all, a single session burned through 3504 requests/hour against Comic Vine's 200/hour
///   limit (ADR-0004) — not a loop bug, just unthrottled legitimate traffic.
/// </summary>
public class ComicVineRateLimiterTests
{
  [Fact]
  public async Task WaitForSlotAsync_BlocksOnceTheWindowLimitIsReached()
  {
    var limiter = new ComicVineRateLimiter(2, TimeSpan.FromMinutes(1));

    await limiter.WaitForSlotAsync(TestContext.Current.CancellationToken);
    await limiter.WaitForSlotAsync(TestContext.Current.CancellationToken);
    var thirdSlot = limiter.WaitForSlotAsync(TestContext.Current.CancellationToken);

    // Proving it's genuinely blocked (not proving how long) — a short real-time race
    // against Task.Delay avoids needing a fake clock for this first test.
    var completed = await Task.WhenAny(thirdSlot, Task.Delay(TimeSpan.FromMilliseconds(200)));

    Assert.NotSame(thirdSlot, completed);
  }

  [Fact]
  public async Task WaitForSlotAsync_ReleasesThePermitEvenIfTheGrantingCallersTokenIsCancelledAfterward()
  {
    var limiter = new ComicVineRateLimiter(1, TimeSpan.FromMilliseconds(20));
    using var cts = new CancellationTokenSource();

    await limiter.WaitForSlotAsync(cts.Token);
    // Cancelling *after* the permit was already granted must not block its eventual
    // release — the token was only ever meant to guard the wait itself.
    await cts.CancelAsync();

    var nextSlot = limiter.WaitForSlotAsync(TestContext.Current.CancellationToken);
    var completed = await Task.WhenAny(nextSlot, Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken));

    Assert.Same(nextSlot, completed);
  }
}
