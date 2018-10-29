using System;

namespace myoddweb.desktopsearch.helper.Performance
{
  internal class CounterEventCount : ICounterEvent
  {
    /// <summary>
    /// The instance manager
    /// </summary>
    private readonly Manager _manager;

    /// <summary>
    /// The counter that owns this
    /// </summary>
    private readonly ICounter _counter;

    public CounterEventCount(ICounter counter, Manager manager)
    {
      // save the counter.
      _counter = counter ?? throw new ArgumentNullException(nameof(counter));

      // make sure that the manager is not null
      _manager = manager ?? throw new ArgumentNullException(nameof(manager));
    }

    public void Dispose()
    {
      // log the time it took...
      _manager.Increment(_counter);
    }
  }
}
