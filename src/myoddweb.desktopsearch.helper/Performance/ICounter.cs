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

namespace myoddweb.desktopsearch.helper.Performance
{
  public interface ICounter : IDisposable
  {
    /// <summary>
    /// The performance counter type.
    /// </summary>
    Type Type { get; }

    /// <summary>
    /// The counter name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Get the category name, if we have one.
    /// </summary>
    string CategoryName { get; }

    /// <summary>
    /// Start an event ... when we dispose of the event
    /// We will update the counters accordingly.
    /// </summary>
    /// <returns></returns>
    ICounterEvent Start();
  }
}
