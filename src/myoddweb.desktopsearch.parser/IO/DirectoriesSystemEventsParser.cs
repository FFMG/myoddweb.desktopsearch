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
    protected override async Task ProcessCreatedAsync(string path, CancellationToken token)
    {
      var directory = Helper.File.DirectoryInfo(path, Logger);
      if (!CanProcessDirectory(directory))
      {
        return;
      }

      // the given file is going to be processed.
      Logger.Verbose($"Directory: {path} (Created)");

      // just add the directory.
      await _persister.AddOrUpdateDirectoryAsync(directory, null, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task ProcessDeletedAsync(string path, CancellationToken token)
    {
      var directory = Helper.File.DirectoryInfo(path, Logger );
      if (null == directory)
      {
        return;
      }

      // we cannot call CanProcessDirectory as it is now deleted.
      if (Helper.File.IsSubDirectory(IgnorePaths, directory ))
      {
        return;
      }

      // the given file is going to be processed.
      Logger.Verbose($"File: {path} (Deleted)");

      // just delete the folder.
      await _persister.DeleteDirectoryAsync(directory, null, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override Task ProcessChangedAsync(string path, CancellationToken token)
    {
      var directory = Helper.File.DirectoryInfo(path, Logger);
      if (!CanProcessDirectory(directory))
      {
        return Task.FromResult<object>(null);
      }

      // the given file is going to be processed.
      Logger.Verbose($"Directory: {path} (Changed)");
      return Task.FromResult<object>(null);
    }

    /// <inheritdoc />
    protected override async Task ProcessRenamedAsync(string path, string oldPath, CancellationToken token)
    {
      // get the new name as well as the one one
      // either of those could be null
      var directory = Helper.File.DirectoryInfo(path, Logger);
      var oldDirectory = Helper.File.DirectoryInfo(oldPath, Logger);

      // if both are null then we cannot do anything with it
      if (null == directory && null == oldDirectory)
      {
        Logger.Error($"I was unable to use the renamed drectories, (old:{oldPath} / new:{path})");
        return;
      }

      // we will need a transaction
      var transaction = _persister.BeginTransaction();

      // if the old directory is null then we can use 
      // the new directory only.
      if (null == oldDirectory)
      {
        // but of course, only if it is usable as well...
        if (!CanProcessDirectory(directory))
        {
          return;
        }

        // just add the new directly.
        if (!await _persister.AddOrUpdateDirectoryAsync(directory, transaction, token).ConfigureAwait(false))
        {
          _persister.Rollback(transaction);
          return;
        }
        _persister.Commit(transaction);
        return;
      }

      // so we now know that the old directory is not null
      // so the new directory could be null or not usubale.
      // so, if we cannot use it, we will simply delete the old one.
      if (!CanProcessDirectory(directory))
      {
        // delete the old folder only, in case it did exist.
        if (!await _persister.DeleteDirectoryAsync(oldDirectory, transaction, token).ConfigureAwait(false))
        {
          _persister.Rollback(transaction);
          return;
        }

        _persister.Commit(transaction);
        return;
      }

      // the given directory is going to be processed.
      Logger.Verbose($"Directory: {oldPath} > {path} (Renamed)");

      // at this point we know we have a new directory that we can use
      // and an old directory that we can also use.
      // so we want to rename the old one with the name of the new one.
      if (-1 == await _persister.RenameOrAddDirectoryAsync( directory, oldDirectory, transaction, token).ConfigureAwait(false))
      {
        _persister.Rollback(transaction);
        return;
      }
      _persister.Commit(transaction);
    }
    #endregion
  }
}
