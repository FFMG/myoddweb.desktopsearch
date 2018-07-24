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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.processor.Processors
{
  internal class Folders : IProcessor
  {
    #region Member Variables
    /// <summary>
    /// The number of folders we want to process.
    /// </summary>
    private readonly long _numberOfFoldersToProcess;

    /// <summary>
    /// The logger that we will be using to log messages.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The directory parser we will be using.
    /// </summary>
    private readonly IDirectory _directory;

    /// <summary>
    /// The persister.
    /// </summary>
    private readonly IPersister _perister;

    /// <summary>
    /// True if start has been called.
    /// </summary>
    private bool _canWork;
    #endregion

    public Folders(long numberOfFoldersToProcessPerEvent, IPersister persister, ILogger logger, IDirectory directory)
    {
      // the number of folders to process.
      _numberOfFoldersToProcess = numberOfFoldersToProcessPerEvent;

      // set the persister.
      _perister = persister ?? throw new ArgumentNullException(nameof(persister));

      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      // save the directory parser
      _directory = directory ?? throw new ArgumentNullException(nameof(directory));
    }

    /// <inheritdoc />
    public void Start()
    {
      _canWork = true;
    }

    /// <inheritdoc />
    public void Stop()
    {
      _canWork = false;
    }

    /// <inheritdoc />
    public async Task WorkAsync(CancellationToken token)
    {
      // if we cannot work ... then don't
      if (false == _canWork)
      {
        return;
      }

      try
      {
        var pendingUpdates = await GetPendingFolderUpdatesAsync(token).ConfigureAwait( false );
        if (null == pendingUpdates || !pendingUpdates.Any())
        {
          return;
        }

        // process all the data one at a time.
        foreach (var pendingFolderUpdate in pendingUpdates)
        {
          if (token.IsCancellationRequested)
          {
            return;
          }

          switch (pendingFolderUpdate.PendingUpdateType )
          {
            case UpdateType.Created:
              await WorkCreatedAsync(pendingFolderUpdate.FolderId, token).ConfigureAwait(false);
              break;

            case UpdateType.Deleted:
              await WorkDeletedAsync(pendingFolderUpdate.FolderId, token).ConfigureAwait(false);
              break;

            case UpdateType.Changed:
              // renamed or content/settingss changed
              await WorkChangedAsync(pendingFolderUpdate.FolderId, token).ConfigureAwait(false);
              break;

            default:
              throw new ArgumentOutOfRangeException();
          }
        }

        // remove the ones we processed.
        await MarkDirectoriesProcessedAsync(pendingUpdates, token).ConfigureAwait(false);
      }
      catch (Exception e)
      {
        _logger.Exception(e);
      }
    }

    /// <summary>
    /// A folder was created
    /// </summary>
    /// <param name="folderId"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task WorkCreatedAsync(long folderId, CancellationToken token)
    {
      var transaction = await _perister.BeginTransactionAsync().ConfigureAwait(false);
      try
      {
        var directory = await _perister.GetDirectoryAsync(folderId, transaction, token).ConfigureAwait(false);
        if (null != directory)
        {
          // get the files in that directory.
          var files = await _directory.ParseDirectoryAsync(directory, token).ConfigureAwait( false );
          if (files != null)
          {
            // and add them to the persiser
            if (await _perister.AddOrUpdateFilesAsync(files, transaction, token).ConfigureAwait(false))
            {
              // log what we just did
              _logger.Verbose($"Found {files.Count} file(s) in the new directory: {directory.FullName}.");
            }
            else
            {
              _logger.Error( $"Unable to add {files.Count} file(s) from the new directory: {directory.FullName}.");
            }
          }
        }

        // if we cancelled the tansaction
        // then we must just rollback
        if (!token.IsCancellationRequested)
        {
          _perister.Commit();
        }
        else
        {
          _perister.Rollback();
        }
      }
      catch (Exception e)
      {
        _perister.Rollback();
        _logger.Exception(e);
      }
    }

    /// <summary>
    /// A folder was deleted
    /// </summary>
    /// <param name="folderId"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task WorkDeletedAsync(long folderId, CancellationToken token)
    {
      return Task.CompletedTask;
    }

    /// <summary>
    /// A folder was changed
    /// </summary>
    /// <param name="folderId"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task WorkChangedAsync(long folderId, CancellationToken token)
    {
      var transaction = await _perister.BeginTransactionAsync().ConfigureAwait(false);
      try
      {
        // Get the files currently on record
        // this can be null if we have nothing.
        var filesOnRecord = await _perister.GetFilesAsync(folderId, transaction, token).ConfigureAwait(false);

        // look for the directory name, if we found nothing on record
        // then we will have to go and look in the database.
        DirectoryInfo directory;
        if (null == filesOnRecord || !filesOnRecord.Any())
        {
          directory = await _perister.GetDirectoryAsync(folderId, transaction, token).ConfigureAwait(false);
        }
        else
        {
          var fi = filesOnRecord.First();
          directory = fi.Directory;
        }

        // if we have no directory then we have nothing...
        if (directory == null)
        {
          _perister.Rollback();
          return;
        }

        // then get the ones in the file.
        var filesOnFile = await _directory.ParseDirectoryAsync(directory, token).ConfigureAwait(false);

        // we want to add all the files that are on disk but not on record.
        var filesToAdd = helper.File.RelativeComplement( filesOnRecord, filesOnFile );

        // we want to remove all the files that are on record but not on file.
        var filesToRemove = helper.File.RelativeComplement(filesOnFile, filesOnRecord);

        // We know that the helper functions never return anything null...
        if (filesToRemove.Any())
        {
          await _perister.DeleteFilesAsync(filesToRemove, transaction, token).ConfigureAwait(false);
        }

        if (filesToAdd.Any())
        {
          await _perister.AddOrUpdateFilesAsync(filesToAdd, transaction, token).ConfigureAwait(false);
        }

        // if we cancelled the tansaction
        // then we must just rollback
        if (!token.IsCancellationRequested)
        {
          _perister.Commit();
        }
        else
        {
          _perister.Rollback();
        }
      }
      catch (Exception e)
      {
        _perister.Rollback();
        _logger.Exception(e);
      }
    }

    /// <summary>
    /// Mark those items as compelte.
    /// </summary>
    /// <param name="pendingUpdates"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> MarkDirectoriesProcessedAsync(IEnumerable<PendingFolderUpdate> pendingUpdates, CancellationToken token)
    {
      var transaction = await _perister.BeginTransactionAsync().ConfigureAwait(false);
      try
      {
        if( !await _perister.MarkDirectoriesProcessedAsync(pendingUpdates.Select(p => p.FolderId).ToList(), transaction, token).ConfigureAwait(false) )
        {
          _perister.Rollback();
          return false;
        }
        // if we cancelled the tansaction
        // then we must just rollback
        if (!token.IsCancellationRequested)
        {
          _perister.Commit();
          return true;
        }
        _perister.Rollback();
        return false;
      }
      catch (Exception e)
      {
        _perister.Rollback();
        _logger.Exception(e);
        return false;
      }
    }

    /// <summary>
    /// Get the folder updates
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<List<PendingFolderUpdate>> GetPendingFolderUpdatesAsync(CancellationToken token)
    {
      var transaction = await _perister.BeginTransactionAsync().ConfigureAwait(false);
      try
      {
        var pendingUpdates = await _perister.GetPendingFolderUpdatesAsync(_numberOfFoldersToProcess, transaction, token).ConfigureAwait(false);
        if (null == pendingUpdates)
        {
          _perister.Rollback();
          _logger.Error( "Unable to get any pending folder updates." );
          return null;
        }
        // if we cancelled the tansaction
        // then we must just rollback
        if (!token.IsCancellationRequested)
        {
          _perister.Commit();
          return pendingUpdates;
        }

        _perister.Rollback();
        return null;
      }
      catch (Exception e)
      {
        _perister.Rollback();
        _logger.Exception(e);
      }
      return null;
    }
  }
}
