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
using System.Collections.Generic;

namespace myoddweb.desktopsearch.interfaces.Configs
{
  public interface IConfig
  {
    /// <summary>
    /// The paths information.
    /// </summary>
    IPaths Paths { get; }

    /// <summary>
    /// All the timers.
    /// </summary>
    ITimers Timers { get; }

    /// <summary>
    /// All the loggers
    /// </summary>
    List<ILogger> Loggers { get; }

    /// <summary>
    /// The processors variables
    /// </summary>
    IProcessors Processors { get; }

    /// <summary>
    /// The web server information
    /// </summary>
    IWebServer WebServer { get; }

    /// <summary>
    /// The maximum number of characters we will concider when processing a word.
    /// The word is still saved... but we just don't keep parts longer than this number.
    /// </summary>
    int MaxNumCharacters { get; }
  }
}