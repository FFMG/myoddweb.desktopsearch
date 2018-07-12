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
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.parser.IO
{
  /// <inheritdoc />
  internal class DirectoriesSystemEventsParser : SystemEventsParser
  {
    /// <summary>
    /// The folder persister that will allow us to add/remove folders.
    /// </summary>
    private readonly IPersister _persister;

    public DirectoriesSystemEventsParser(
      IPersister persister,
      IReadOnlyCollection<DirectoryInfo> ignorePaths, int eventsParserMs, ILogger logger) :
      base( ignorePaths, eventsParserMs, logger)
    {
      _persister = persister ?? throw new ArgumentNullException(nameof(persister));
    }

    #region Process Directory events
    /// <summary>
    /// Check if we can process this directory or not.
    /// </summary>
    /// <param name="directory"></param>
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
    protected override async Task ProcessCreatedAsync(string fullPath, CancellationToken token)
    {
      var directory = new DirectoryInfo(fullPath);
      if (!CanProcessDirectory(directory))
      {
        return;
      }

      // the given file is going to be processed.
      Logger.Verbose($"Directory: {fullPath} (Created)");

      // just add the folder.
      await _persister.AddOrUpdateDirectoryAsync(directory, null, token);
    }

    /// <inheritdoc />
    protected override async Task ProcessDeletedAsync(string fullPath, CancellationToken token)
    {
      var directory = new DirectoryInfo(fullPath);

      // we cannot call CanProcessFile as it is now deleted.
      if (Helper.File.IsSubDirectory(IgnorePaths, directory ))
      {
        return;
      }

      // the given file is going to be processed.
      Logger.Verbose($"File: {fullPath} (Deleted)");

      // just delete the folder.
      await _persister.DeleteDirectoryAsync(directory, null, token);
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
    protected override async Task ProcessRenamedAsync(string fullPath, string oldFullPath, CancellationToken token)
    {
      var transaction = _persister.BeginTransaction();
      var directory = new DirectoryInfo(fullPath);
      var oldDirectory = new DirectoryInfo(oldFullPath);
      if (!CanProcessDirectory(directory))
      {
        // delete the old folder only, in case it did exist.
        if (!await _persister.DeleteDirectoryAsync(directory, transaction, token))
        {
          _persister.Rollback(transaction);
          return;
        }

        _persister.Commit(transaction);
        return;
      }

      // the given file is going to be processed.
      Logger.Verbose($"Directory: {oldFullPath} > {fullPath} (Renamed)");

      // first delete the old folder.
      if (-1 == await _persister.RenameDirectoryAsync( directory, oldDirectory, transaction, token))
      {
        _persister.Rollback(transaction);
        return;
      }
      _persister.Commit(transaction);
    }
    #endregion
  }
}
