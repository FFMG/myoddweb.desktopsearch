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
using ILogger = myoddweb.desktopsearch.interfaces.Logging.ILogger;

namespace myoddweb.desktopsearch.helper.Performance
{
  public abstract class Counter : ICounter
  {
    #region Member variables
    /// <inheritdoc />
    public string CategoryName { get; }

    /// <summary>
    /// The category help, if we have one.
    /// </summary>
    public string CategoryHelp { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public Type Type { get; }
    #endregion

    /// <summary>
    /// Return a performance counter, making sure that the category is created properly.
    /// </summary>
    /// <param name="categoryName"></param>
    /// <param name="categoryHelp"></param>
    /// <param name="counterName"></param>
    /// <param name="type"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    protected Counter(string categoryName, string categoryHelp, string counterName, Type type, ILogger logger)
    {
      CategoryName = categoryName;
      CategoryHelp = categoryHelp;

      // save the counter name
      Name = counterName ?? throw new ArgumentNullException( nameof(counterName));

      // save the performance type.
      Type = type;

      // initialise it.
      Manager.Instance.Initialise(this, logger );
    }

    /// <inheritdoc />
    public ICounterEvent Start()
    {
      switch (Type)
      {
        case Type.Timed:
          return new CounterEventTimer( this, Manager.Instance);

        case Type.CountPerSeconds:
          return new CounterEventCount( this, Manager.Instance);

        default:
          throw new ArgumentOutOfRangeException();
      }
    }

    public void Dispose()
    {
      Manager.Instance.Dispose( Name );
    }
  }
}
