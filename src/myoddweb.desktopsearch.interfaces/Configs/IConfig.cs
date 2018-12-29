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
    /// The maintenance variables.
    /// </summary>
    IMaintenance Maintenance { get; }

    /// <summary>
    /// All the loggers
    /// </summary>
    IList<ILogger> Loggers { get; }

    /// <summary>
    /// The processors variables
    /// </summary>
    IProcessors Processors { get; }

    /// <summary>
    /// The web server information
    /// </summary>
    IWebServer WebServer { get; }

    /// <summary>
    /// The database connection information
    /// </summary>
    IDatabase Database { get; }

    /// <summary>
    /// The performance category/counter manager.
    /// </summary>
    IPerformance Performance { get; }

    /// <summary>
    /// The maximum word length
    /// So if the max len is 3 and the word is hello
    /// The word is ignored.
    /// </summary>
    int MaxNumCharactersPerWords { get; }

    /// <summary>
    /// The longest number of characters per part
    /// If the word is 'hello' but the parts cannot be more than 3
    /// Then the searchable words are 'hel', 'ell', 'llo' ... only 3 letters.
    /// </summary>
    int MaxNumCharactersPerParts { get; }
  }
}