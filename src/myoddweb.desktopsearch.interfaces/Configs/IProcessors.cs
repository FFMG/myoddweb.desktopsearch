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
    /// When the file/folders updates are compelte
    /// the is the amount of time we want to wait
    /// before we check again...
    /// </summary>
    int QuietEventsProcessorMs { get; }

    /// <summary>
    /// If we still have files/folders/... to check
    /// this is the amount of time between processing we want to wai.t
    /// </summary>
    int BusyEventsProcessorMs { get; }

    /// <summary>
    /// The number of files we want to do per processing events.
    /// Don't make that number too small as it will take forever to parse
    /// But also not too big as it blocks the database when/if there is work to do.
    /// </summary>
    int UpdatesPerFilesEvent { get; }

    /// <summary>
    /// The number of folders we want to do per processing events.
    /// Don't make that number too small as it will take forever to parse
    /// But also not too big as it blocks the database when/if there is work to do.
    /// </summary>
    int UpdatesPerFolderEvent { get; }

    /// <summary>
    /// The number of fileid we want to parse per events.
    /// There is no point in doing too many at once as they lock the db
    /// </summary>
    int UpdateFileIdsEvent { get; }

    /// <summary>
    /// List of file patterns that we ignore.
    /// </summary>
    IList<IIgnoreFile> IgnoreFiles { get; }
  }
}