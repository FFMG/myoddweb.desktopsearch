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

namespace myoddweb.desktopsearch.helper.Performance
{
  internal class CounterDataTimer : CounterDataCount
  {
    /// <summary>
    /// The counter we will be using
    /// </summary>
    protected PerformanceCounter Base { get; set; }

    public CounterDataTimer( ICounter counter, string baseName ) : base(counter)
    {
      var category = PerformanceCounterCategory.GetCategories().First(cat => cat.CategoryName == counter.CategoryName);
      Base = category.GetCounters().FirstOrDefault(c => c.CounterName == baseName);
      if (Base != null)
      {
        Base.ReadOnly = false;
      }
    }

    /// <inheritdoc />
    public override void Increment()
    {
      base.Increment();
      Base?.Increment();
    }

    /// <inheritdoc />
    public override void IncremenFromUtcTime(DateTime startTime)
    {
      base.IncremenFromUtcTime(startTime);
      Base?.Increment();
    }
  }
}
