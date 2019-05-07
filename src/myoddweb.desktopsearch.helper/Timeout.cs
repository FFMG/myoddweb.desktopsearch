using System;
using System.Diagnostics;

namespace myoddweb.desktopsearch.helper
{
  public class Timeout
  {
    /// <summary>
    /// Value if we never want to timeout.
    /// </summary>
    public const int Infinite = -1;

    /// <summary>
    /// The given timout value.
    /// </summary>
    private readonly int _timeoutMs;

    /// <summary>
    /// The stopwatch
    /// </summary>
    private readonly Stopwatch _stopwatch = new Stopwatch();

    /// <summary>
    /// Contructor
    /// </summary>
    /// <param name="timeoutMs">The number of ms we want to wait before we timeout.</param>
    public Timeout(int timeoutMs)
    {
      if (timeoutMs < Infinite)
      {
        throw new ArgumentOutOfRangeException(nameof(timeoutMs));
      }
      if (timeoutMs == 0)
      {
        throw new TimeoutException("The time out time in ms was 0, so the condition was never tested");
      }
      _timeoutMs = timeoutMs;
      _stopwatch.Start();
    }

    /// <summary>
    /// Throw if the timeout has been reached.
    /// </summary>
    /// <exception cref="System.TimeoutException">The token has had cancellation requested.</exception>
    public void ThrowIfTimeoutReached()
    {
      if (_timeoutMs != Infinite && _stopwatch.ElapsedMilliseconds > _timeoutMs)
      {
        throw new TimeoutException( $"Timeout {_stopwatch.ElapsedMilliseconds}/{_timeoutMs} has been reached.");
      }
    }
  }
}
