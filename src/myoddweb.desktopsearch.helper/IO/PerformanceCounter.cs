//This file is part of Myoddweb.DesktopSearch.
//
//    Myoddweb.DesktopSearch is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    Myoddweb.DesktopSearch is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with Myoddweb.DesktopSearch.  If not, see<https://www.gnu.org/licenses/gpl-3.0.en.html>.
using System;
using System.Diagnostics;
using System.Linq;
using myoddweb.desktopsearch.interfaces.Configs;
using myoddweb.desktopsearch.interfaces.IO;
using ILogger = myoddweb.desktopsearch.interfaces.Logging.ILogger;

namespace myoddweb.desktopsearch.helper.IO
{
  public abstract class PerformanceCounter : IPerformanceCounter, IDisposable
  {
    /// <summary>
    /// The base performance counter, (if needed).
    /// </summary>
    private System.Diagnostics.PerformanceCounter _counterBase;

    /// <summary>
    /// Return a performance counter, making sure that the category is created properly.
    /// </summary>
    /// <param name="performance"></param>
    /// <param name="counterName"></param>
    /// <param name="type"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    protected System.Diagnostics.PerformanceCounter CreatePerformanceCounter(IPerformance performance, string counterName, PerformanceCounterType type, ILogger logger)
    {
      Initialise(performance, counterName, type, logger );

      // now create the counters.
      return new System.Diagnostics.PerformanceCounter
      {
        CategoryName = performance.CategoryName,
        CounterName = counterName,
        MachineName = ".",
        ReadOnly = false,
        RawValue = 0
      };
    }

    /// <summary>
    /// Make sure that we have a valid category.
    /// </summary>
    /// <param name="performance"></param>
    /// <param name="counterName"></param>
    /// <param name="type"></param>
    /// <param name="logger"></param>
    private void Initialise(IPerformance performance, string counterName, PerformanceCounterType type, ILogger logger )
    {
      var counterCreationDataCollection = new CounterCreationDataCollection
      {
        new CounterCreationData(
          counterName,
          counterName,
          type
        )
      };

      string baseCounterName = null;
      switch (type)
      {
        case PerformanceCounterType.AverageBase:
          throw new ArgumentException( $"You cannot create a base counter for {counterName}!");

        case PerformanceCounterType.AverageTimer32:
          baseCounterName = $"{counterName} base";
          counterCreationDataCollection.Add(new CounterCreationData(
            baseCounterName,
            baseCounterName,
            PerformanceCounterType.AverageBase
          ));
          break;
      }

      if (!PerformanceCounterCategory.Exists(performance.CategoryName))
      {
        PerformanceCounterCategory.Create(performance.CategoryName, performance.CategoryHelp,
          PerformanceCounterCategoryType.SingleInstance,
          counterCreationDataCollection);

        logger.Information($"Created performance category: {performance.CategoryName} for counter: {counterName}");
      }
      else
      if (!PerformanceCounterCategory.CounterExists(counterName, performance.CategoryName))
      {
        var category = PerformanceCounterCategory.GetCategories().First(cat => cat.CategoryName == performance.CategoryName);
        var counters = category.GetCounters();
        foreach (var counter in counters)
        {
          counterCreationDataCollection.Add(new CounterCreationData(counter.CounterName, counter.CounterHelp, counter.CounterType));
        }

        PerformanceCounterCategory.Delete(performance.CategoryName);
        PerformanceCounterCategory.Create(performance.CategoryName, performance.CategoryHelp,
          PerformanceCounterCategoryType.SingleInstance,
          counterCreationDataCollection);

        logger.Information($"Updated performance category: {performance.CategoryName} for counter: {counterName}");
      }

      switch (type)
      {
        case PerformanceCounterType.AverageBase:
          throw new ArgumentException($"You cannot create a base counter for {counterName}!");

        case PerformanceCounterType.AverageTimer32:
          _counterBase = new System.Diagnostics.PerformanceCounter
          {
            CategoryName = performance.CategoryName,
            CounterName = baseCounterName,
            MachineName = ".",
            ReadOnly = false,
            RawValue = 0
          };
          break;
      }

    }

    /// <summary>
    /// Derived class can increment the counter given a UTC start time.
    /// </summary>
    protected abstract void OnIncremenFromUtcTime(DateTime startTime);

    /// <summary>
    /// Derived class can increment the counter.
    /// </summary>
    protected abstract void OnIncrement();

    /// <summary>
    /// Dispose of the code.
    /// </summary>
    protected abstract void OnDispose();

    /// <inheritdoc/>
    public void IncremenFromUtcTime(DateTime startTime)
    {
      // first we get the base class to do it
      // then we will update the base.
      OnIncremenFromUtcTime( startTime );

      // we might not have a base.
      _counterBase?.Increment();
    }

    /// <inheritdoc />
    public void Increment()
    {
      OnIncrement();
    }

    public void Dispose()
    {
      OnDispose();
      _counterBase?.Dispose();
    }
  }
}
