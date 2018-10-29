using System;

namespace myoddweb.desktopsearch.helper.Performance
{
  internal class CounterEventTimer : ICounterEvent
  {
    /// <summary>
    /// The instance manager
    /// </summary>
    private readonly Manager _manager;

    /// <summary>
    /// The counter that owns this
    /// </summary>
    private readonly Counter _counter;

    /// <summary>
    /// When this timer started
    /// </summary>
    private readonly DateTime _start;

    public CounterEventTimer( Counter counter, Manager manager)
    {
      // start the timer.
      _start = DateTime.UtcNow;

      // save the counter.
      _counter = counter ?? throw new ArgumentNullException(nameof(counter));

      // make sure that the manager is not null
      _manager = manager ?? throw new ArgumentNullException( nameof(manager));
    }

    public void Dispose()
    {
      // log the time it took...
      _manager.IncremenFromUtcTime(_counter, _start);
    }
  }
}
