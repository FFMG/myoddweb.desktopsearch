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
using System.Data.Common;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.parser.IO
{
  /// <inheritdoc />
  internal class FilesSystemEventsParser : SystemEventsParser
  {
    /// <summary>
    /// The folder persister that will allow us to add/remove folders.
    /// </summary>
    private readonly IPersister _persister;

    /// <summary>
    /// The current transaction.
    /// </summary>
    private DbTransaction _currentTransaction;

    public FilesSystemEventsParser(
      IPersister persister,
      IReadOnlyCollection<DirectoryInfo> ignorePaths, int eventsParserMs, ILogger logger) :
      base( ignorePaths, eventsParserMs, logger)
    {
      _persister = persister ?? throw new ArgumentNullException(nameof(persister));
    }

    #region Process File events
    /// <summary>
    /// Check if we can process this file or not.
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    private bool CanProcessFile(FileInfo file)
    {
      if (null == file)
      {
        return false;
      }
      // it is a file that we can read?
      // we do not check delted files as they are ... deleted.
      if (!helper.File.CanReadFile(file))
      {
        return false;
      }

      // do we monitor this directory?
      if (helper.File.IsSubDirectory(IgnorePaths, file.Directory))
      {
        return false;
      }
      return true;
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
    protected override async Task ProcessCreatedAsync(string fullPath, CancellationToken token)
    {
      var file = helper.File.FileInfo(fullPath, Logger );
      if (!CanProcessFile(file))
      {
        return;
      }

      // the given file is going to be processed.
      Logger.Verbose($"File: {fullPath} (Created)");

      // just add the file.
      await _persister.AddOrUpdateFileAsync( file, _currentTransaction, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task ProcessDeletedAsync(string fullPath, CancellationToken token)
    {
      var file = helper.File.FileInfo(fullPath, Logger);
      if (null == file)
      {
        return;
      }

      // we cannot call CanProcessFile as it is now deleted.
      if (helper.File.IsSubDirectory(IgnorePaths, file.Directory))
      {
        return;
      }

      // the given file is going to be processed.
      Logger.Verbose($"File: {fullPath} (Deleted)");

      // do we have the file on record?
      if (await _persister.FileExistsAsync(file, _currentTransaction, token).ConfigureAwait(false))
      {
        // just delete the folder.
        await _persister.DeleteFileAsync(file, _currentTransaction, token).ConfigureAwait(false);
      }
    }

    /// <inheritdoc />
    protected override Task ProcessChangedAsync(string fullPath, CancellationToken token)
    {
      var file = helper.File.FileInfo(fullPath, Logger);
      if (!CanProcessFile(file))
      {
        return Task.FromResult<object>(null);
      }

      // the given file is going to be processed.
      Logger.Verbose($"File: {fullPath} (Changed)");
      return Task.FromResult<object>(null);
    }

    /// <inheritdoc />
    protected override async Task ProcessRenamedAsync(string path, string oldPath, CancellationToken token)
    {
      // get the new name as well as the one one
      // either of those could be null
      var file = helper.File.FileInfo( path, Logger);
      var oldFile = helper.File.FileInfo( oldPath, Logger);

      // if both are null then we cannot do anything with it
      if (null == file && null == oldFile)
      {
        Logger.Error($"I was unable to use the renamed files, (old:{oldPath} / new:{path})");
        return;
      }

      // if the old directory is null then we can use 
      // the new directory only.
      if (null == oldFile)
      {
        // but of course, only if it is usable as well...
        if (!CanProcessFile(file))
        {
          return;
        }

        // just add the new directly.
        if (!await _persister.AddOrUpdateFileAsync(file, _currentTransaction, token).ConfigureAwait(false))
        {
          Logger.Error( $"Unable to add file {path} during rename.");
        }
        return;
      }

      // so we now know that the old file is not null
      // so the new directory could be null and/or not usubale.
      // so, if we cannot use it, we will simply delete the old one.
      if (!CanProcessFile(file))
      {
        // if the old path is not an ignored path
        // then we might be able to delete that file.
        if (helper.File.IsSubDirectory(IgnorePaths, oldFile.Directory))
        {
          return;
        }

        // if the old file does not exist on record ... then there is nothing more for us to do .
        if (!await _persister.FileExistsAsync(oldFile, _currentTransaction, token).ConfigureAwait(false))
        {
          return;
        }

        // delete the old folder only, in case it did exist.
        if (!await _persister.DeleteFileAsync(oldFile, _currentTransaction, token).ConfigureAwait(false))
        {
          Logger.Error( $"Unable to remove old file {oldPath} durring rename");
        }
        return;
      }

      // the given file is going to be processed.
      Logger.Verbose($"File: {path} > {oldPath} (Renamed)");

      // at this point we know we have a new file that we can use
      // and an old file that we could also use.
      //
      // if we do not have the old file on record then it is not a rename
      // but rather is is a new one.
      if (!await _persister.FileExistsAsync(oldFile, _currentTransaction, token).ConfigureAwait(false))
      {
        // just add the new directly.
        if (!await _persister.AddOrUpdateFileAsync(file, _currentTransaction, token).ConfigureAwait(false))
        {
          Logger.Error($"Unable to add file {path} during rename.");
        }
        return;
      }

      // we have the old name on record so we can try and rename it.
      // if we ever have an issue, it could be because we are trying to rename to
      // something that already exists.
      if (-1 == await _persister.RenameOrAddFileAsync(file, oldFile, _currentTransaction, token).ConfigureAwait(false))
      {
        Logger.Error( $"Unable to rename file {path} > {oldPath}");
      }
    }
    #endregion
  }
}
