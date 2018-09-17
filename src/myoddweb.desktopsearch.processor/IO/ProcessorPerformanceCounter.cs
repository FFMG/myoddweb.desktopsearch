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
using myoddweb.desktopsearch.interfaces.Configs;
using ILogger = myoddweb.desktopsearch.interfaces.Logging.ILogger;

namespace myoddweb.desktopsearch.processor.IO
{
  internal class ProcessorPerformanceCounter : helper.IO.PerformanceCounter
  {
    /// <summary>
    /// The performance counter
    /// </summary>
    private readonly PerformanceCounter _counter;

    public ProcessorPerformanceCounter( IPerformance performance, string counterName, ILogger logger )
    {
      // now create the counters.
      _counter = CreatePerformanceCounter( performance, counterName, PerformanceCounterType.AverageTimer32, logger );
    }

    /// <inheritdoc/>
    protected override void OnDispose()
    {
      _counter.Dispose();
    }

    /// <inheritdoc/>
    protected override void OnIncremenFromUtcTime(DateTime startTime)
    {
      var tsDiff = (DateTime.UtcNow - startTime);
      _counter?.IncrementBy(tsDiff.Ticks);
    }

    /// <inheritdoc/>
    protected override void OnIncrement()
    {
      throw new NotImplementedException();
    }
  }
}
