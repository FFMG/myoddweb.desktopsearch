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
using System.Data.Common;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.IO;
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

    /// <summary>
    /// The current transaction.
    /// </summary>
    private DbTransaction _currentTransaction;

    public DirectoriesSystemEventsParser( IPersister persister, IDirectory directory, int eventsParserMs, ILogger logger) :
      base( directory, eventsParserMs, logger)
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
      if (!helper.File.CanReadDirectory(directory))
      {
        return false;
      }

      // do we monitor this directory?
      return !Directory.IsIgnored(directory);
    }
    #endregion

    #region Abstract Process events
    /// <inheritdoc />
    protected override void ProcessEventsStart()
    {
      if (_currentTransaction != null)
      {
        Logger.Warning("Trying to start an event processing when the previous one does not seem to have ended.");
        return;
      }
      _currentTransaction = _persister.BeginTransactionAsync().Result;
    }

    /// <inheritdoc />
    protected override void ProcessEventsEnd(bool hadErrors)
    {
      if (_currentTransaction == null)
      {
        Logger.Warning("Trying to complete an event processing when it does not seem to have been started.");
        return;
      }

      try
      {
        if (hadErrors)
        {
          _persister.Rollback();
        }
        else
        {
          _persister.Commit();
        }
      }
      catch (Exception e)
      {
        Logger.Exception(e);
        throw;
      }
      finally
      {
        _currentTransaction = null;
      }
    }

    /// <inheritdoc />
    protected override async Task ProcessCreatedAsync(string path, CancellationToken token)
    {
      var directory = helper.File.DirectoryInfo(path, Logger);
      if (!CanProcessDirectory(directory))
      {
        return;
      }

      // the given file is going to be processed.
      Logger.Verbose($"Directory: {path} (Created)");

      // just add the directory.
      await _persister.AddOrUpdateDirectoryAsync(directory, _currentTransaction, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task ProcessDeletedAsync(string path, CancellationToken token)
    {
      var directory = helper.File.DirectoryInfo(path, Logger );
      if (null == directory)
      {
        return;
      }

      // we cannot call CanProcessDirectory as it is now deleted.
      if (Directory.IsIgnored( directory ))
      {
        return;
      }

      // the given file is going to be processed.
      Logger.Verbose($"File: {path} (Deleted)");

      // do we have the directory on record?
      if (await _persister.DirectoryExistsAsync(directory, _currentTransaction, token).ConfigureAwait(false))
      {
        // just delete the directory.
        await _persister.DeleteDirectoryAsync(directory, _currentTransaction, token).ConfigureAwait(false);
      }
    }

    /// <inheritdoc />
    protected override Task ProcessChangedAsync(string path, CancellationToken token)
    {
      var directory = helper.File.DirectoryInfo(path, Logger);
      if (!CanProcessDirectory(directory))
      {
        return Task.CompletedTask;
      }

      // the given file is going to be processed.
      Logger.Verbose($"Directory: {path} (Changed)");
      return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override async Task ProcessRenamedAsync(string path, string oldPath, CancellationToken token)
    {
      // get the new name as well as the one one
      // either of those could be null
      var directory = helper.File.DirectoryInfo(path, Logger);
      var oldDirectory = helper.File.DirectoryInfo(oldPath, Logger);

      // if both are null then we cannot do anything with it
      if (null == directory && null == oldDirectory)
      {
        Logger.Error($"I was unable to use the renamed drectories, (old:{oldPath} / new:{path})");
        return;
      }

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
        if (!await _persister.AddOrUpdateDirectoryAsync(directory, _currentTransaction, token).ConfigureAwait(false))
        {
          Logger.Error($"Unable to add directory {path} during rename.");
        }
        return;
      }

      // so we now know that the old directory is not null
      // so the new directory could be null or not usubale.
      // so, if we cannot use it, we will simply delete the old one.
      if (!CanProcessDirectory(directory))
      {
        // delete the old folder only, in case it did exist.
        if (!await _persister.DeleteDirectoryAsync(oldDirectory, _currentTransaction, token).ConfigureAwait(false))
        {
          Logger.Error($"Unable to remove old file {oldPath} durring rename");
        }
        return;
      }

      // the given directory is going to be processed.
      Logger.Verbose($"Directory: {oldPath} > {path} (Renamed)");

      // at this point we know we have a new directory that we can use
      // and an old directory that we could also use.
      //
      // if we do not have the old directory on record then it is not a rename
      // but rather is is a new one.
      if (!await _persister.DirectoryExistsAsync(oldDirectory, _currentTransaction, token).ConfigureAwait(false))
      {
        // just add the new directly.
        if (!await _persister.AddOrUpdateDirectoryAsync( directory, _currentTransaction, token).ConfigureAwait(false))
        {
          Logger.Error($"Unable to add directory {path} during rename.");
        }
        return;
      }

      // we have the old name on record so we can try and rename it.
      // if we ever have an issue, it could be because we are trying to rename to
      // something that already exists.
      if (-1 == await _persister.RenameOrAddDirectoryAsync( directory, oldDirectory, _currentTransaction, token).ConfigureAwait(false))
      {
        Logger.Error($"Unable to rename directory {path} > {oldPath}");
      }
    }
    #endregion
  }
}
