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
using System.Collections.Generic;
using System.IO;
using myoddweb.desktopsearch.interfaces.Logging;

namespace myoddweb.desktopsearch.parser.IO
{
  /// <inheritdoc />
  internal class DirectoriesSystemEventsParser : SystemEventsParser
  {
    public DirectoriesSystemEventsParser(IReadOnlyCollection<DirectoryInfo> ignorePaths, int eventsParserMs, ILogger logger) :
      base( ignorePaths, eventsParserMs, logger)
    {
    }

    #region Process Directory events
    /// <summary>
    /// Process a file/directory that was renamed.
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="renameEvent"></param>
    /// <returns></returns>
    private void ProcessRenameDirectoryInfo(DirectoryInfo directory, RenamedEventArgs renameEvent)
    {
      if (null == directory)
      {
        throw new ArgumentNullException(nameof(directory));
      }

      if (!CanProcessDirectory(directory, renameEvent.ChangeType))
      {
        return;
      }

      Logger.Verbose($"Directory: {renameEvent.OldFullPath} to {renameEvent.FullPath}");
    }

    /// <summary>
    /// Parse a normal file event
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="fileEvent"></param>
    /// <returns></returns>
    private void ProcessDirectoryInfo(DirectoryInfo directory, FileSystemEventArgs fileEvent)
    {
      // it is a directory
      if (!CanProcessDirectory(directory, fileEvent.ChangeType))
      {
        return;
      }
      Logger.Verbose($"Directory: {fileEvent.FullPath} ({fileEvent.ChangeType})");
    }

    /// <summary>
    /// Check if we can process this directory or not.
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="types"></param>
    /// <returns></returns>
    private bool CanProcessDirectory(DirectoryInfo directory, WatcherChangeTypes types)
    {
      // it is a file that we can read?
      // it is a file that we can read?
      if (!IsDeleted(types) && !Helper.File.CanReadDirectory(directory))
      {
        return false;
      }

      // do we monitor this directory?
      if (Helper.File.IsSubDirectory(IgnorePaths, directory))
      {
        return false;
      }
      return true;
    }

    /// <inheritdoc />
    protected override void ProcessEvent(string fullPath, FileSystemEventArgs e)
    {
      var directory = new DirectoryInfo(fullPath);
      if (e is RenamedEventArgs renameEvent)
      {
        ProcessRenameDirectoryInfo(directory, renameEvent);
      }
      else
      {
        ProcessDirectoryInfo(directory, e);
      }
    }
    #endregion
  }
}
