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
  public interface IProcessors
  {
    /// <summary>
    /// How often we want to run the processors.
    /// </summary>
    int EventsProcessorMs { get; }

    /// <summary>
    /// How often we want to run the maintenance.
    /// </summary>
    int MaintenanceProcessorMinutes { get; }

    /// <summary>
    /// The number of files we want to do per processing events.
    /// Don't make that number too small as it will take forever to parse
    /// But also not too big as it blocks the database when/if there is work to do.
    /// </summary>
    int UpdatesFilesPerEvent { get; }

    /// <summary>
    /// The number of words we want parse per file events.
    /// </summary>
    int UpdatesWordsPerFilesPerEvent { get; }

    /// <summary>
    /// The number of folders we want to do per processing events.
    /// Don't make that number too small as it will take forever to parse
    /// But also not too big as it blocks the database when/if there is work to do.
    /// </summary>
    int UpdatesFolderPerEvent { get; }

    /// <summary>
    /// The number of parsed words we want to process at a time.
    /// </summary>
    int UpdateWordParsedPerEvent { get; }

    /// <summary>
    /// List of file patterns that we ignore.
    /// </summary>
    IList<IIgnoreFile> IgnoreFiles { get; }
  }
}