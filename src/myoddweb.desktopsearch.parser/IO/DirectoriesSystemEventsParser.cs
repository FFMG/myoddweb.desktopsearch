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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
    /// Check if we can process this directory or not.
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="types"></param>
    /// <returns></returns>
    private bool CanProcessDirectory(DirectoryInfo directory)
    {
      // it is a directory that we can read?
      if (!Helper.File.CanReadDirectory(directory))
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
    #endregion

    #region Abstract Process events
    /// <inheritdoc />
    protected override Task ProcessCreatedAsync(string fullPath, CancellationToken token)
    {
      var directory = new DirectoryInfo(fullPath);
      if (!CanProcessDirectory(directory))
      {
        return Task.FromResult<object>(null);
      }

      // the given file is going to be processed.
      Logger.Verbose($"Directory: {fullPath} (Created)");
      return Task.FromResult<object>(null);
    }

    /// <inheritdoc />
    protected override Task ProcessDeletedAsync(string fullPath, CancellationToken token)
    {
      var directory = new DirectoryInfo(fullPath);

      // we cannot call CanProcessFile as it is now deleted.
      if (Helper.File.IsSubDirectory(IgnorePaths, directory ))
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
      var directory = new DirectoryInfo(fullPath);
      if (!CanProcessDirectory(directory))
      {
        return Task.FromResult<object>(null);
      }

      // the given file is going to be processed.
      Logger.Verbose($"Directory: {fullPath} (Changed)");
      return Task.FromResult<object>(null);
    }

    /// <inheritdoc />
    protected override Task ProcessRenamedAsync(string fullPath, string oldFullPath, CancellationToken token)
    {
      var directory = new DirectoryInfo(fullPath);
      if (!CanProcessDirectory(directory))
      {
        return Task.FromResult<object>(null);
      }

      // the given file is going to be processed.
      Logger.Verbose($"Directory: {fullPath} > {oldFullPath} (Renamed)");
      return Task.FromResult<object>(null);
    }
    #endregion
  }
}
