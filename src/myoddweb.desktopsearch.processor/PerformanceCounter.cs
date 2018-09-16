using System;
using myoddweb.desktopsearch.interfaces.IO;
using System.Diagnostics;
using System.Linq;
using myoddweb.desktopsearch.interfaces.Logging;

namespace myoddweb.desktopsearch.processor
{
  internal class PerformanceCounter : IPerformanceCounter
  {
    private readonly System.Diagnostics.PerformanceCounter _counter;
    private readonly System.Diagnostics.PerformanceCounter _counterBase;

    public PerformanceCounter( string categoryName, string categoryHelp, string counterName, ILogger logger )
    {
      var counterCreationDataCollection = new CounterCreationDataCollection
      {
        new CounterCreationData(
          counterName,
          counterName,
          PerformanceCounterType.AverageTimer32
        ),
        new CounterCreationData(
          $"{counterName} base",
          $"{counterName} base",
          PerformanceCounterType.AverageBase
        )
      };

      if (!PerformanceCounterCategory.Exists(categoryName))
      {
        PerformanceCounterCategory.Create(categoryName, categoryHelp,
          PerformanceCounterCategoryType.SingleInstance,
          counterCreationDataCollection);

        logger.Information($"Created performance category: {categoryName} for counter: {counterName}");
      }
      else
      if (!PerformanceCounterCategory.CounterExists(counterName, categoryName))
      {
        var category = PerformanceCounterCategory.GetCategories().First( cat => cat.CategoryName == categoryName);
        var counters = category.GetCounters();
        foreach (var counter in counters)
        {
          counterCreationDataCollection.Add(new CounterCreationData(counter.CounterName, counter.CounterHelp,  counter.CounterType));
        }

        PerformanceCounterCategory.Delete(categoryName);
        PerformanceCounterCategory.Create(categoryName, categoryHelp,
          PerformanceCounterCategoryType.SingleInstance,
          counterCreationDataCollection);

        logger.Information($"Updated performance category: {categoryName} for counter: {counterName}");
      }

      // now create the counter.
      _counter = new System.Diagnostics.PerformanceCounter
      {
        CategoryName = categoryName,
        CounterName = counterName,
        MachineName = ".",
        ReadOnly = false,
        RawValue = 0
      };

      _counterBase = new System.Diagnostics.PerformanceCounter
      {
        CategoryName = categoryName,
        CounterName = $"{counterName} base",
        MachineName = ".",
        ReadOnly = false,
        RawValue = 0
      };
    }

    /// <inheritdoc/>
    public void IncremenFromUtcTime(DateTime startTime)
    {
      var tsDiff = (DateTime.UtcNow - startTime);
      _counter?.IncrementBy(tsDiff.Ticks);
      _counterBase?.Increment();
    }
  }
}
