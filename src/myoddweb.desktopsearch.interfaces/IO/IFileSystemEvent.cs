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
using System.IO;

namespace myoddweb.desktopsearch.interfaces.IO
{
  public interface IFileSystemEvent
  {
    /// <summary>
    /// The event type
    /// </summary>
    WatcherChangeTypes ChangeType { get; }

    /// <summary>
    /// If this is a directory or a file
    /// </summary>
    bool IsDirectory { get; }

    /// <summary>
    /// The directory event
    /// </summary>
    DirectoryInfo Directory { get; }

    /// <summary>
    /// The file event
    /// </summary>
    FileInfo File { get; }

    /// <summary>
    /// In the case of a rename the 'old' directory.
    /// </summary>
    DirectoryInfo OldDirectory { get; }

    /// <summary>
    /// In the case of a rename, the 'old' file
    /// </summary>
    FileInfo OldFile { get; }

    /// <summary>
    /// Get the full name, (either directory or file).
    /// </summary>
    string FullName { get; }

    /// <summary>
    /// Get the old full name, (either directory or file).
    /// </summary>
    string OldFullName { get; }

    /// <summary>
    /// Check if the event is of a certain type.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    bool Is(WatcherChangeTypes type);
  }
}