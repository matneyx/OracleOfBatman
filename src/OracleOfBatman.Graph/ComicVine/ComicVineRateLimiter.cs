namespace OracleOfBatman.Graph.ComicVine;

/// <summary>
///   Sliding-window throttle for outgoing Comic Vine requests — a permit is a
///   request slot; each one refills itself <paramref name="window" /> after being granted,
///   rather than needing a manually-tracked timestamp queue. Exists because a session with no
///   throttling at all once burned through 3504 requests/hour against Comic Vine's 200/hour
///   limit (ADR-0004) — not a loop bug, just unpaced legitimate traffic.
/// </summary>
public sealed class ComicVineRateLimiter(int maxRequestsPerWindow, TimeSpan window)
{
  private readonly SemaphoreSlim _semaphore = new(maxRequestsPerWindow, maxRequestsPerWindow);

  public async Task WaitForSlotAsync(CancellationToken token = default)
  {
    await _semaphore.WaitAsync(token);
    // Once granted, the permit's return is unconditional — CancellationToken.None on both
    // the delay and the continuation, not the caller's token, so a caller cancelling
    // afterward can neither lose the release (the original bug) nor return it early.
    _ = Task.Delay(window, CancellationToken.None).ContinueWith(_ => _semaphore.Release(), CancellationToken.None);
  }
}
