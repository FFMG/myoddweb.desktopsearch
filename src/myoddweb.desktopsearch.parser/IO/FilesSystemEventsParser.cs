﻿//This file is part of Myoddweb.DesktopSearch.
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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
    /// Check if we can process this file or not.
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    private bool CanProcessFile(FileInfo file)
    {
      // it is a file that we can read?
      // we do not check delted files as they are ... deleted.
      if (!Helper.File.CanReadFile(file))
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
    #endregion

    #region Abstract Process events
    /// <inheritdoc />
    protected override Task ProcessCreatedAsync(string fullPath, CancellationToken token)
    {
      var file = new FileInfo(fullPath);
      if (!CanProcessFile(file))
      {
        return Task.FromResult<object>(null);
      }

      // the given file is going to be processed.
      Logger.Verbose($"File: {fullPath} (Created)");
      return Task.FromResult<object>(null);
    }

    /// <inheritdoc />
    protected override Task ProcessDeletedAsync(string fullPath, CancellationToken token)
    {
      var file = new FileInfo(fullPath);

      // we cannot call CanProcessFile as it is now deleted.
      if (Helper.File.IsSubDirectory(IgnorePaths, file.Directory))
      {
        return Task.FromResult<object>(null);
      }

      // the given file is going to be processed.
      Logger.Verbose($"File: {fullPath} (Deleted)");
      return Task.FromResult<object>(null);
    }

    /// <inheritdoc />
    protected override Task ProcessChangedAsync(string fullPath, CancellationToken token)
    {
      var file = new FileInfo(fullPath);
      if (!CanProcessFile(file))
      {
        return Task.FromResult<object>(null);
      }

      // the given file is going to be processed.
      Logger.Verbose($"File: {fullPath} (Changed)");
      return Task.FromResult<object>(null);
    }

    /// <inheritdoc />
    protected override Task ProcessRenamedAsync(string fullPath, string oldFullPath, CancellationToken token)
    {
      var file = new FileInfo(fullPath);
      if (!CanProcessFile(file))
      {
        return Task.FromResult<object>(null);
      }

      // the given file is going to be processed.
      Logger.Verbose($"File: {fullPath} > {oldFullPath} (Renamed)");
      return Task.FromResult<object>(null);
    }
    #endregion
  }
}