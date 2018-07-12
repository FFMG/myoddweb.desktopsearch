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
  internal class FilesSystemEventsParser : SystemEventsParser
  {
    public FilesSystemEventsParser(IReadOnlyCollection<DirectoryInfo> ignorePaths, int eventsParserMs, ILogger logger) :
      base( ignorePaths, eventsParserMs, logger)
    {
    }

    #region Process File events
    /// <summary>
    /// Process a file/directory that was renamed.
    /// </summary>
    /// <param name="file"></param>
    /// <param name="renameEvent"></param>
    /// <returns></returns>
    private void ProcessRenameFileInfo(FileInfo file, RenamedEventArgs renameEvent)
    {
      if (null == file)
      {
        throw new ArgumentNullException(nameof(file));
      }

      // can we process this?
      if (!CanProcessFile(file, renameEvent.ChangeType))
      {
        return;
      }

      Logger.Verbose($"File: {renameEvent.OldFullPath} to {renameEvent.FullPath}");
    }

    /// <summary>
    /// Parse a normal file event
    /// </summary>
    /// <param name="file"></param>
    /// <param name="fileEvent"></param>
    /// <returns></returns>
    private void ProcessFileInfo(FileInfo file, FileSystemEventArgs fileEvent)
    {
      if (null == file)
      {
        throw new ArgumentNullException(nameof(file));
      }

      // can we process this?
      if (!CanProcessFile(file, fileEvent.ChangeType))
      {
        return;
      }

      // the given file is going to be processed.
      Logger.Verbose($"File: {fileEvent.FullPath} ({fileEvent.ChangeType})");
    }

    /// <summary>
    /// Check if we can process this file or not.
    /// </summary>
    /// <param name="file"></param>
    /// <param name="types"></param>
    /// <returns></returns>
    private bool CanProcessFile(FileInfo file, WatcherChangeTypes types)
    {
      // it is a file that we can read?
      // we do not check delted files as they are ... deleted.
      if (!IsDeleted(types) && !Helper.File.CanReadFile(file))
      {
        return false;
      }

      // do we monitor this directory?
      if (Helper.File.IsSubDirectory(IgnorePaths, file.Directory))
      {
        return false;
      }
      return true;
    }

    /// <inheritdoc />
    protected override void ProcessEvent(string fullPath, FileSystemEventArgs e)
    {
      var file = new FileInfo(e.FullPath);
      if (e is RenamedEventArgs renameEvent)
      {
        ProcessRenameFileInfo(file, renameEvent);
      }
      else
      {
        ProcessFileInfo(file, e);
      }
    }
    #endregion
  }
}
