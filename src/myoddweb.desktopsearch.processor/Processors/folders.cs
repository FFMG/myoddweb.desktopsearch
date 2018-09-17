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
using System;
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
    private readonly IPersister _persister;

    /// <inheritdoc />
    /// <summary>
    /// We only process one item at a time here.
    /// </summary>
    public int MaxUpdatesToProcess => 1;

    /// <summary>
    /// The performance counter.
    /// </summary>
    private readonly IPerformanceCounter _counter;
    #endregion

    public Folders(IPerformanceCounter counter, IPersister persister, ILogger logger, IDirectory directory)
    {
      // save the counter
      _counter = counter ?? throw new ArgumentNullException(nameof(counter));

      // set the persister.
      _persister = persister ?? throw new ArgumentNullException(nameof(persister));

      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      // save the directory parser
      _directory = directory ?? throw new ArgumentNullException(nameof(directory));
    }

    /// <inheritdoc />
    public void Stop()
    {
      _counter?.Dispose();
    }

    /// <inheritdoc />
    public async Task<int> WorkAsync(CancellationToken token)
    {
      var tsActual = DateTime.UtcNow;
      try
      {
        // then get _all_ the file updates that we want to do.
        var pendingUpdate = await GetPendingFolderUpdateAndMarkDirectoryProcessedAsync(token).ConfigureAwait(false);
        if (null == pendingUpdate)
        {
          return 0;
        }

        // process the update.
        await ProcessFolderUpdateAsync(pendingUpdate, token).ConfigureAwait( false );

        // we processed one update
        return 1;
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Directories Processor - Work");
        throw;
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        throw;
      }
      finally
      {
        _counter?.IncremenFromUtcTime(tsActual);
      }
    }

    /// <summary>
    /// Process a single folder update
    /// </summary>
    /// <param name="pendingFolderUpdate"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task ProcessFolderUpdateAsync(PendingFolderUpdate pendingFolderUpdate, CancellationToken token)
    {
      try
      {
        switch (pendingFolderUpdate.PendingUpdateType)
        {
          case UpdateType.Created:
            await WorkCreatedAsync(pendingFolderUpdate, token).ConfigureAwait(false);
            break;

          case UpdateType.Deleted:
            await WorkDeletedAsync(pendingFolderUpdate, token).ConfigureAwait(false);
            break;

          case UpdateType.Changed:
            // renamed or content/settingss changed
            await WorkChangedAsync(pendingFolderUpdate, token).ConfigureAwait(false);
            break;

          default:
            throw new ArgumentOutOfRangeException();
        }
      }
      catch (Exception)
      {
        // something did not work ... re-touch the directory
        await TouchDirectoryAsync(pendingFolderUpdate.FolderId, pendingFolderUpdate.PendingUpdateType, token).ConfigureAwait(false);

        // done
        throw;
      }
    }

    #region Workers
    /// <summary>
    /// A folder was created
    /// </summary>
    /// <param name="pendingUpdate"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task WorkCreatedAsync(PendingFolderUpdate pendingUpdate, CancellationToken token)
    {
      var directory = pendingUpdate.Directory;
      if (null == directory)
      {
        // we are done 
        return;
      }

      // get the files in that directory.
      var files = await _directory.ParseDirectoryAsync(directory, token).ConfigureAwait(false);
      if (files == null)
      {
        // there is nothing to add to this directory so we are done.
        return;
      }
      
      var transaction = await _persister.BeginWrite(token).ConfigureAwait(false);
      if (null == transaction)
      {
        throw new Exception("Unable to get transaction!");
      }

      try
      {
        // and add them to the persiser
        if (await _persister.Folders.Files.AddOrUpdateFilesAsync(files, transaction, token).ConfigureAwait(false))
        {
          // log what we just did
          _logger.Verbose($"Found {files.Count} file(s) in the new directory: {directory.FullName}.");
        }
        else
        {
          _logger.Error($"Unable to add {files.Count} file(s) from the new directory: {directory.FullName}.");
        }

        // we are done
        _persister.Commit(transaction);
      }
      catch
      {
        _persister.Rollback(transaction);
        throw;
      }
    }

    /// <summary>
    /// A folder was deleted
    /// </summary>
    /// <param name="pendingUpdate"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task WorkDeletedAsync(PendingFolderUpdate pendingUpdate, CancellationToken token)
    {
      var transaction = await _persister.BeginWrite(token).ConfigureAwait(false);
      if (null == transaction)
      {
        throw new Exception("Unable to get transaction!");
      }

      try
      {
        // using the foler id is the fastest.
        await _persister.Folders.Files.DeleteFilesAsync(pendingUpdate.FolderId, transaction, token);

        // we are done
        _persister.Commit(transaction);
      }
      catch
      {
        _persister.Rollback(transaction);
        throw;
      }
    }

    /// <summary>
    /// A folder was changed
    /// </summary>
    /// <param name="pendingUpdate"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task WorkChangedAsync(PendingFolderUpdate pendingUpdate, CancellationToken token)
    {
      // look for the directory name, if we found nothing on record
      // then we will have to go and look in the database.
      var directory = pendingUpdate.Directory;

      // if we have no directory then we have nothing...
      if (directory == null)
      {
        // we are done 
        return;
      }

      // Get the files currently on record
      // this can be null if we have nothing.
      var filesOnRecord = pendingUpdate.Files;

      // then get the ones in the file.
      var filesOnFile = await _directory.ParseDirectoryAsync(directory, token).ConfigureAwait(false);

      // we want to add all the files that are on disk but not on record.
      var filesToAdd = helper.File.RelativeComplement(filesOnRecord, filesOnFile);

      // we want to remove all the files that are on record but not on file.
      var filesToRemove = helper.File.RelativeComplement(filesOnFile, filesOnRecord);

      // if we have nothing to do... then get out
      if (!filesToRemove.Any() && !filesToAdd.Any())
      {
        return;
      }

      var transaction = await _persister.BeginWrite(token).ConfigureAwait(false);
      if (null == transaction)
      {
        throw new Exception("Unable to get transaction!");
      }

      try
      {
        // We know that the helper functions never return anything null...
        if (filesToRemove.Any())
        {
          await _persister.Folders.Files.DeleteFilesAsync(filesToRemove, transaction, token).ConfigureAwait(false);
        }

        // anything to add?
        if (filesToAdd.Any())
        {
          await _persister.Folders.Files.AddOrUpdateFilesAsync(filesToAdd, transaction, token).ConfigureAwait(false);
        }

        // we are done
        _persister.Commit(transaction);
      }
      catch
      {
        _persister.Rollback(transaction);
        throw;
      }
    }
    #endregion

    #region Database calls
    /// <summary>
    /// Get the folder updates
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<PendingFolderUpdate> GetPendingFolderUpdateAndMarkDirectoryProcessedAsync( CancellationToken token)
    {
      // get the transaction
      var transaction = await _persister.BeginWrite(token).ConfigureAwait(false);
      if (null == transaction)
      {
        //  we probably cancelled.
        return null;
      }
      try
      {
        var pendingUpdates = await _persister.Folders.FolderUpdates.GetPendingFolderUpdatesAsync(MaxUpdatesToProcess, transaction, token).ConfigureAwait(false);
        if (null == pendingUpdates)
        {
          _logger.Error("Unable to get any pending folder updates.");

          // we will now return null
          _persister.Commit(transaction);
          return null;
        }

        var pendingUpdate = pendingUpdates.FirstOrDefault();
        if (null == pendingUpdate)
        {
          // we will now return null
          _persister.Commit(transaction);
          return null;
        }

        // the first thing we will do is mark the folder as processed.
        // if anything goes wrong _after_ that we will try and 'touch' it again.
        // by doing it that way around we ensure that we never keep the transaction.
        // and we don't run the risk of someone else trying to process this again.
        await _persister.Folders.FolderUpdates.MarkDirectoryProcessedAsync(pendingUpdate.FolderId, transaction, token).ConfigureAwait(false);

        // we are done here.
        _persister.Commit(transaction);

        // return null if we cancelled.
        return pendingUpdates.FirstOrDefault();
      }
      catch
      {
        _persister.Rollback(transaction);
        return null;
      }
    }
 
    /// <summary>
    /// Touch the directory to mark it for an update again.
    /// </summary>
    /// <param name="folderId"></param>
    /// <param name="type"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task TouchDirectoryAsync(long folderId, UpdateType type, CancellationToken token)
    {
      var transaction = await _persister.BeginWrite(token).ConfigureAwait(false);
      if (null == transaction)
      {
        throw new Exception("Unable to get transaction!");
      }

      try
      {
        // touch this directory again.
        await _persister.Folders.FolderUpdates.TouchDirectoriesAsync( new []{ folderId }, type, transaction, token).ConfigureAwait(false);

        // we are done
        _persister.Commit(transaction);
      }
      catch
      {
        _persister.Rollback(transaction);
        throw;
      }
    }
    #endregion
  }
}
