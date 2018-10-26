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
using ILogger = myoddweb.desktopsearch.interfaces.Logging.ILogger;

namespace myoddweb.desktopsearch.helper.Performance
{
  internal class Manager
  {
    #region Static Member Variables
    /// <summary>
    /// The one and only instance of the manager
    /// </summary>
    private static Manager _instance;

    /// <summary>
    /// Static lock so that the same lock is used for all instances.
    /// </summary>
    private static readonly object Lock = new object();
    #endregion

    #region Member Variables
    /// <summary>
    /// Static list of counters that might need to be re-created.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, CounterDataCount> _counters = new ConcurrentDictionary<Guid, CounterDataCount>();
    #endregion

    private Manager()
    {
    }

    /// <summary>
    /// Get the one and only instance of the manager.
    /// </summary>
    public static Manager Instance
    {
      get
      {
        if (_instance != null)
        {
          return _instance;
        }

        lock (Lock)
        {
          // just before we create the instance, we check one more time.
          return _instance ?? (_instance = new Manager());
        }
      }
    }

    /// <summary>
    /// Make sure that we have a valid category.
    /// </summary>
    /// <param name="performance"></param>
    /// <param name="counter"></param>
    /// <param name="logger"></param>
    public void Initialise(IPerformance performance, Counter counter, ILogger logger)
    {
      // we will need to re-create all the counters...
      DisposeAllCounters();

      // enter the lock so we can create the counter.
      lock (Lock)
      {
        // build the collection data
        var counterCreationDataCollection = BuildCollectionData(counter);

        // if the category does not exist, try and create it.
        if (!PerformanceCounterCategory.Exists(performance.CategoryName))
        {
          PerformanceCounterCategory.Create(performance.CategoryName, performance.CategoryHelp,
            PerformanceCounterCategoryType.SingleInstance, counterCreationDataCollection);
          logger.Information($"Created performance category: {performance.CategoryName} for counter: {counter.Name}");
          return;
        }

        if (PerformanceCounterCategory.CounterExists(counter.Name, performance.CategoryName))
        {
          return;
        }

        var category = PerformanceCounterCategory.GetCategories()
          .First(cat => cat.CategoryName == performance.CategoryName);
        var counters = category.GetCounters();
        foreach (var c in counters)
        {
          counterCreationDataCollection.Add(new CounterCreationData(c.CounterName, c.CounterHelp, c.CounterType));
        }

        PerformanceCounterCategory.Delete(performance.CategoryName);
        PerformanceCounterCategory.Create(performance.CategoryName, performance.CategoryHelp,
          PerformanceCounterCategoryType.SingleInstance, counterCreationDataCollection);

        logger.Information($"Updated performance category: {performance.CategoryName} for counter: {counter.Name}");
      }
    }

    #region Private functions
    /// <summary>
    /// Dispose and remove a counter.
    /// </summary>
    private void DisposeAllCounters()
    {
      lock (Lock)
      {
        foreach (var counter in _counters)
        {
          counter.Value?.Dispose();
        }
        _counters.Clear();
      }
    }

    /// <summary>
    /// Get this counter, (if we have one).
    /// </summary>
    private CounterDataCount Counter(Counter counter)
    {
      if (_counters.TryGetValue(counter.Guid, out var existingCounter))
      {
        return existingCounter;
      }

      // we use the lock in case it is being re-created.
      lock (Lock)
      {
        var cd = !MustUseBase(counter.Type) ? new CounterDataCount(counter) : new CounterDataTimer(counter, BaseName(counter));
        _counters[counter.Guid] = cd;
      }
      return _counters[counter.Guid];
    }
    #endregion

    #region Private static functions
    /// <summary>
    /// Build the collection data.
    /// </summary>
    /// <param name="counter"></param>
    /// <returns></returns>
    private CounterCreationDataCollection BuildCollectionData(Counter counter)
    {
      // Create the collection data. 
      var counterCreationDataCollection = new CounterCreationDataCollection
      {
        new CounterCreationData(
          counter.Name,
          counter.Name,
          GetPerformanceCounterType(counter.Type)
        )
      };

      if (MustUseBase(counter.Type))
      {
        counterCreationDataCollection.Add(new CounterCreationData(
          BaseName(counter),
          BaseName(counter),
          PerformanceCounterType.AverageBase
        ));
      }
      return counterCreationDataCollection;
    }

    /// <summary>
    /// Base counter name.
    /// </summary>
    /// <returns></returns>
    private static string BaseName(Counter counter)
    {
      return $"{counter.Name}Base";
    }

    /// <summary>
    /// Must use a base average.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    private static bool MustUseBase(Type type)
    {
      switch (type)
      {
        case Type.Timed:
          return true;

        case Type.CountPerSeconds:
          return false;

        default:
          throw new ArgumentOutOfRangeException(nameof(type), type, null);
      }
    }
    
    private static PerformanceCounterType GetPerformanceCounterType(Type type)
    {
      switch (type)
      {
        case Type.Timed:
          return PerformanceCounterType.AverageTimer32;

        case Type.CountPerSeconds:
          return PerformanceCounterType.RateOfCountsPerSecond64;

        default:
          throw new ArgumentOutOfRangeException(nameof(type), type, null);
      }
    }
    #endregion

    #region Internal functions
    /// <summary>
    /// Dispose of a single counter.
    /// </summary>
    /// <param name="guid"></param>
    internal void Dispose(Guid guid)
    {
      // then remove it from out list.
      // so we can re-get it if need be.
      lock (Lock)
      {
        if (_counters.TryRemove(guid, out var counter))
        {
          counter?.Dispose();
        }
      }
    }

    /// <summary>
    /// Increment the given counter.
    /// </summary>
    /// <param name="counter"></param>
    internal void Increment( Counter counter )
    {
      Counter(counter)?.Increment();
    }

    /// <summary>
    /// Increment the counter given a Utc time
    /// </summary>
    /// <param name="counter"></param>
    /// <param name="startTime"></param>
    internal void IncremenFromUtcTime(Counter counter, DateTime startTime)
    {
      Counter(counter)?.IncremenFromUtcTime(startTime);
    }
    #endregion
  }
}