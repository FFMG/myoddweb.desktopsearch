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
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using myoddweb.desktopsearch.interfaces.Configs;
using myoddweb.desktopsearch.interfaces.IO;
using ILogger = myoddweb.desktopsearch.interfaces.Logging.ILogger;

namespace myoddweb.desktopsearch.helper.IO
{
  public abstract class PerformanceCounter : IPerformanceCounter, IDisposable
  {
    #region Member variables
    /// <summary>
    /// Static list of counters that might need to be re-created.
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, System.Diagnostics.PerformanceCounter> Counters = new ConcurrentDictionary<Guid, System.Diagnostics.PerformanceCounter>();

    /// <summary>
    /// Static list of base counters that might need to be re-created.
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, System.Diagnostics.PerformanceCounter> BaseCounters = new ConcurrentDictionary<Guid, System.Diagnostics.PerformanceCounter>();

    /// <summary>
    /// Static lock so that the same lock is used for all instances.
    /// </summary>
    private static readonly object Lock = new object();

    /// <summary>
    /// The performance configuration
    /// </summary>
    private readonly IPerformance _performance;

    /// <summary>
    /// The counter name
    /// </summary>
    private readonly string _counterName;

    /// <summary>
    /// The Guid of this counter.
    /// </summary>
    private readonly Guid _guid = Guid.NewGuid();

    /// <summary>
    /// Base counter name.
    /// </summary>
    /// <returns></returns>
    private string BaseName => $"{_counterName}Base";

    /// <summary>
    /// Do we use a base counter or not.
    /// </summary>
    private bool UseBase { get; }
    #endregion

    /// <summary>
    /// Get this counter, (if we have one).
    /// </summary>
    private System.Diagnostics.PerformanceCounter Counter
    {
      get
      {
        if (Counters.TryGetValue(_guid, out var counter))
        {
          return counter;
        }

        // we use the lock in case it is being re-created.
        lock (Lock)
        {
          var category = PerformanceCounterCategory.GetCategories().First(cat => cat.CategoryName == _performance.CategoryName);
          Counters[_guid] = category.GetCounters().FirstOrDefault(c => c.CounterName == _counterName);
          Counters[_guid].ReadOnly = false;
        }
        return Counters[_guid];
      }
    }

    /// <summary>
    /// Get the Base Counter if we have one.
    /// </summary>
    private System.Diagnostics.PerformanceCounter CounterBase
    {
      get
      {
        if (!UseBase)
        {
          return null;
        }

        if (BaseCounters.TryGetValue(_guid, out var counter))
        {
          return counter;
        }

        // we use the lock in case it is being re-created.
        lock (Lock)
        {
          var category = PerformanceCounterCategory.GetCategories().First(cat => cat.CategoryName == _performance.CategoryName);
          BaseCounters[_guid] = category.GetCounters().FirstOrDefault(c => c.CounterName == BaseName);
          BaseCounters[_guid].ReadOnly = false;
        }
        return BaseCounters[_guid];
      }
    }

    /// <summary>
    /// Return a performance counter, making sure that the category is created properly.
    /// </summary>
    /// <param name="performance"></param>
    /// <param name="counterName"></param>
    /// <param name="type"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    protected PerformanceCounter(IPerformance performance, string counterName, PerformanceCounterType type, ILogger logger)
    {
      // performance
      _performance = performance ?? throw new ArgumentNullException(nameof(performance));

      // save the counter name
      _counterName = counterName ?? throw new ArgumentNullException( nameof(counterName));

      // check if we must use base
      UseBase = MustUseBase(type);

      // initialise it.
      Initialise(performance, type, logger );
    }
    
    /// <summary>
    /// Make sure that we have a valid category.
    /// </summary>
    /// <param name="performance"></param>
    /// <param name="type"></param>
    /// <param name="logger"></param>
    private void Initialise(IPerformance performance, PerformanceCounterType type, ILogger logger)
    {
      // we will need to re-create all the counters...
      DisposeAllCounters();

      // enter the lock so we can create the counter.
      lock (Lock)
      {
        // build the collection data
        var counterCreationDataCollection = BuildCollectionData(type);

        if (!PerformanceCounterCategory.Exists(performance.CategoryName))
        {
          PerformanceCounterCategory.Create(performance.CategoryName, performance.CategoryHelp, PerformanceCounterCategoryType.SingleInstance, counterCreationDataCollection);
          logger.Information($"Created performance category: {performance.CategoryName} for counter: {_counterName}");
        }
        else if (!PerformanceCounterCategory.CounterExists(_counterName, performance.CategoryName))
        {
          var category = PerformanceCounterCategory.GetCategories().First(cat => cat.CategoryName == performance.CategoryName);
          var counters = category.GetCounters();
          foreach (var counter in counters)
          {
            counterCreationDataCollection.Add(new CounterCreationData(counter.CounterName, counter.CounterHelp,counter.CounterType));
          }

          PerformanceCounterCategory.Delete(performance.CategoryName);
          PerformanceCounterCategory.Create(performance.CategoryName, performance.CategoryHelp,PerformanceCounterCategoryType.SingleInstance,counterCreationDataCollection);

          logger.Information($"Updated performance category: {performance.CategoryName} for counter: {_counterName}");
        }
      }
    }

    /// <summary>
    /// Dispose and remove a counter.
    /// </summary>
    private static void DisposeAllCounters()
    {
      lock (Lock)
      {
        foreach (var counter in Counters)
        {
          counter.Value?.Dispose();
        }
        Counters.Clear();

        foreach (var counter in BaseCounters)
        {
          counter.Value?.Dispose();
        }
        BaseCounters.Clear();
      }
    }

    /// <summary>
    /// Dispose of a single counter.
    /// </summary>
    /// <param name="guid"></param>
    private static void Dispose(Guid guid)
    {
      // then remove it from out list.
      // so we can re-get it if need be.
      lock (Lock)
      {
        if (BaseCounters.TryRemove(guid, out var baseCounter))
        {
          baseCounter?.Dispose();
        }
        if (Counters.TryRemove(guid, out var counter))
        {
          counter?.Dispose();
        }
      }
    }

    /// <summary>
    /// Must use a base average.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    private static bool MustUseBase(PerformanceCounterType type)
  {
    switch (type)
    {
      case PerformanceCounterType.AverageBase:
        return false;

      case PerformanceCounterType.AverageTimer32:
        return true;

      default:
        return false;
    }
  }

    /// <summary>
    /// Build the collection data.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    private CounterCreationDataCollection BuildCollectionData( PerformanceCounterType type)
    {
      // Create the collection data. 
      var counterCreationDataCollection = new CounterCreationDataCollection
      {
        new CounterCreationData(
          _counterName,
          _counterName,
          type
        )
      };

      if (UseBase)
      {
        counterCreationDataCollection.Add(new CounterCreationData(
          BaseName,
          BaseName,
          PerformanceCounterType.AverageBase
        ));
      }
      return counterCreationDataCollection;
    }

    /// <inheritdoc/>
    public void IncremenFromUtcTime(DateTime startTime)
    {
      var tsDiff = (DateTime.UtcNow - startTime);
      Counter.IncrementBy(tsDiff.Ticks);

      // we might not have a base.
      CounterBase?.Increment();
    }

    /// <inheritdoc />
    public void Increment()
    {
      Counter.Increment();
      CounterBase?.Increment();
    }

    public void Dispose()
    {
      Dispose(_guid);
    }
  }
}
