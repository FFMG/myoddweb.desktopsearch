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
using myoddweb.desktopsearch.interfaces.Configs;
using ILogger = myoddweb.desktopsearch.interfaces.Logging.ILogger;

namespace myoddweb.desktopsearch.helper.Performance
{
  public abstract class Counter : IPerformanceCounter, IDisposable
  {
    #region Member variables
    /// <summary>
    /// The performance configuration
    /// </summary>
    private readonly IPerformance _performance;

    /// <summary>
    /// Get the category name, if we have one.
    /// </summary>
    public string CategoryName => _performance?.CategoryName;

    /// <summary>
    /// The counter name
    /// </summary>
    internal Guid Guid { get; } = Guid.NewGuid();

    /// <summary>
    /// The counter name
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The performance counter type.
    /// </summary>
    public Type Type { get; }
    #endregion

    /// <summary>
    /// Return a performance counter, making sure that the category is created properly.
    /// </summary>
    /// <param name="performance"></param>
    /// <param name="counterName"></param>
    /// <param name="type"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    protected Counter(IPerformance performance, string counterName, Type type, ILogger logger)
    {
      // performance
      _performance = performance ?? throw new ArgumentNullException(nameof(performance));

      // save the counter name
      Name = counterName ?? throw new ArgumentNullException( nameof(counterName));

      // save the performance type.
      Type = type;

      // initialise it.
      Manager.Instance.Initialise(performance, this, logger );
    }

    /// <inheritdoc/>
    public void IncremenFromUtcTime(DateTime startTime)
    {
      Manager.Instance.IncremenFromUtcTime( this, startTime );
    }

    /// <inheritdoc />
    public void Increment()
    {
      Manager.Instance.Increment( this );
    }

    public void Dispose()
    {
      Manager.Instance.Dispose(Guid);
    }
  }
}
