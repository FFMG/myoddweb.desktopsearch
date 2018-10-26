using System;
using System.Diagnostics;
using System.Linq;

namespace myoddweb.desktopsearch.helper.Performance
{
  internal class CounterDataCount : IDisposable
  {
    /// <summary>
    /// The counter we will be using
    /// </summary>
    protected PerformanceCounter Counter { get; set; }

    public CounterDataCount(Counter counter)
    {
      var category = PerformanceCounterCategory.GetCategories().First(cat => cat.CategoryName == counter.CategoryName);
      Counter = category.GetCounters().FirstOrDefault(c => c.CounterName == counter.Name);
      if (Counter != null)
      {
        Counter.ReadOnly = false;
      }
    }

    /// <inheritdoc />
    /// <summary>
    /// Dispose of the counter.
    /// </summary>
    public virtual void Dispose()
    {
      Counter?.Dispose();
      Counter = null;
    }

    /// <summary>
    /// Increment the given counter.
    /// </summary>
    public virtual void Increment()
    {
      Counter?.Increment();
    }

    /// <summary>
    /// Increment the counter given a Utc time
    /// </summary>
    /// <param name="startTime"></param>
    public virtual void IncremenFromUtcTime(DateTime startTime)
    {
      var tsDiff = (DateTime.UtcNow - startTime);
      var performanceCounterTicks = tsDiff.Ticks * Stopwatch.Frequency / TimeSpan.TicksPerSecond;
      Counter?.IncrementBy(performanceCounterTicks);
    }
  }
}
