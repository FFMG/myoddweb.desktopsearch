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
using System.Data;
using System.Diagnostics;
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
    /// The number of files we want to process.
    /// </summary>
    private long _currentNumberOfFoldersToProcess;

    /// <summary>
    /// The maximum amount of time a cycle should take.
    /// </summary>
    private readonly long _maxNumberOfMs;

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
    #endregion

    public Folders(long numberOfFoldersToProcessPerEvent, long maxNumberOfMs, IPersister persister, ILogger logger, IDirectory directory)
    {
      // the number of folders to process.
      _numberOfFoldersToProcess = numberOfFoldersToProcessPerEvent;
      _currentNumberOfFoldersToProcess = numberOfFoldersToProcessPerEvent;

      // save the max amount of time we want to take.
      _maxNumberOfMs = maxNumberOfMs;

      // set the persister.
      _persister = persister ?? throw new ArgumentNullException(nameof(persister));

      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      // save the directory parser
      _directory = directory ?? throw new ArgumentNullException(nameof(directory));
    }

    /// <inheritdoc />
    public async Task WorkAsync(CancellationToken token)
    {
      try
      {
        // start timing how long the entire operation will take
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        // then get _all_ the file updates that we want to do.
        var pendingUpdates = await GetPendingFolderUpdatesAsync(token).ConfigureAwait(false);
        if (null == pendingUpdates)
        {
          return;
        }

        // the number of updates we actually did.
        long processedFolders = 0;
        try
        {
          // then we go around and do chunks of data one at a time.
          // this is to prevent massive chunks of data from being processed.
          var tasks = new List<Task>();
          for (var start = 0; start < pendingUpdates.Count; start += (int) _numberOfFoldersToProcess)
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // run this group of folders.
            tasks.Add(ProcessFolderUpdates(pendingUpdates, start, token));

            // if we are here we processed those files.
            processedFolders += _numberOfFoldersToProcess;
          }

          // then wait for the tasks to finish
          await Task.WhenAll(tasks.ToArray()).ConfigureAwait(false);
        }
        finally
        {
          stopwatch.Stop();
          if (processedFolders > 0)
          {
            _logger.Verbose(
              $"Processed {(processedFolders > pendingUpdates.Count ? pendingUpdates.Count : processedFolders)} pending folder updates (Time Elapsed: {stopwatch.Elapsed:g})");
          }

          // Adjust the number of items we will be doing the next time.
          AdjustNumberOfFoldersToProcess(processedFolders, stopwatch.ElapsedMilliseconds);
        }
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Directories Processor - Work");
        throw;
      }
      catch (Exception e)
      {
        _logger.Exception(e);
      }
    }

    /// <summary>
    /// Adjust the current number of items to process.
    /// If we are fast enough, then we might as well do as much as posible.
    /// </summary>
    /// <param name="processedFolders"></param>
    /// <param name="elapsedMilliseconds"></param>
    private void AdjustNumberOfFoldersToProcess(long processedFolders, long elapsedMilliseconds)
    {
      // did we do as many as we could do? If not it means that there are simply not enough
      // folders to process anymore, in the case, the amount of time does not really matter.
      // We could also have broken out of the list...
      // 'processedFolders' could be bigger than the number to process because of the loop above.
      if (processedFolders < _currentNumberOfFoldersToProcess)
      {
        return;
      }

      // did we go faster than expected?
      if (elapsedMilliseconds < _maxNumberOfMs * 0.9) //  remove a little bit so we don't always re-adjust.
      {
        //  just add 10%
        var calc = (long)Math.Ceiling(_currentNumberOfFoldersToProcess * 1.1);
        if (_currentNumberOfFoldersToProcess == calc )
        {
          _currentNumberOfFoldersToProcess = calc + 1;
        }
        else
        {
          _currentNumberOfFoldersToProcess = calc;
        }
        return;
      }

      // or did we go slower?
      // we never want to go over the time limit.
      if (elapsedMilliseconds <= _maxNumberOfMs)
      {
        return;
      }

      // remove 5%
      _currentNumberOfFoldersToProcess = (long)(_currentNumberOfFoldersToProcess * 0.95);

      // and make suse that we do not have silly values.
      if (_currentNumberOfFoldersToProcess <= 0)
      {
        _currentNumberOfFoldersToProcess = 1;
      }
    }

    private async Task ProcessFolderUpdates(List<PendingFolderUpdate> pendingUpdates, int start,CancellationToken token)
    {
      // make sure that we never do more than the total number of items
      // from our current start position.
      var max = pendingUpdates.Count;
      var count = _numberOfFoldersToProcess + start > max ? max - start : (int) _numberOfFoldersToProcess;

      // and process those files.
      await ProcessFolderUpdates(pendingUpdates.GetRange(start, count), token).ConfigureAwait(false);
    }

    private async Task ProcessFolderUpdates(IReadOnlyCollection<PendingFolderUpdate> pendingUpdates, CancellationToken token)
    { 
      if (null == pendingUpdates || !pendingUpdates.Any())
      {
        // this is not an error, just nothing to do.
        return;
      }

      try
      {
        // process all the data one at a time.
        foreach (var pendingFolderUpdate in pendingUpdates)
        {
          // get out if needed.
          token.ThrowIfCancellationRequested();

          // process this folder update.
          await ProcessFolderUpdateAsync(pendingFolderUpdate, token).ConfigureAwait(false);
        }
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Process pending folder updates");
        throw;
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
      var transaction = await _persister.BeginTransactionAsync(token).ConfigureAwait(false);
      if (null == transaction)
      {
        return;
      }

      try
      {
        // try and do the work in transaction
        await ProcessFolderUpdateWithinTransactionAsync(pendingFolderUpdate, transaction, token).ConfigureAwait(false);
        
        // mark it as complete.
        await _persister.MarkDirectoryProcessedAsync(pendingFolderUpdate.FolderId, transaction, token).ConfigureAwait(false);

        // we are done
        _persister.Commit(transaction);
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        _persister.Rollback(transaction);
        throw;
      }
    }

    private async Task ProcessFolderUpdateWithinTransactionAsync(PendingFolderUpdate pendingFolderUpdate, IDbTransaction transaction, CancellationToken token)
    {
      try
      {
        switch (pendingFolderUpdate.PendingUpdateType)
        {
          case UpdateType.Created:
            await WorkCreatedAsync(pendingFolderUpdate.FolderId, transaction, token).ConfigureAwait(false);
            break;

          case UpdateType.Deleted:
            await WorkDeletedAsync(pendingFolderUpdate.FolderId, transaction, token).ConfigureAwait(false);
            break;

          case UpdateType.Changed:
            // renamed or content/settingss changed
            await WorkChangedAsync(pendingFolderUpdate.FolderId, transaction, token).ConfigureAwait(false);
            break;

          default:
            throw new ArgumentOutOfRangeException();
        }
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
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task WorkCreatedAsync(long folderId, IDbTransaction transaction, CancellationToken token)
    {
      var directory = await GetDirectoryAsync(folderId, transaction, token).ConfigureAwait(false);
      if (null == directory)
      {
        // we are done 
        return;
      }

      // get the files in that directory.
      var files = await _directory.ParseDirectoryAsync(directory, token).ConfigureAwait(false);
      if (files == null)
      {
        // we are done 
        return;
      }

      // and add them to the persiser
      if (await _persister.AddOrUpdateFilesAsync(files, transaction, token).ConfigureAwait(false))
      {
        // log what we just did
        _logger.Verbose($"Found {files.Count} file(s) in the new directory: {directory.FullName}.");
      }
      else
      {
        _logger.Error($"Unable to add {files.Count} file(s) from the new directory: {directory.FullName}.");
      }

      // mark it as processed.
      await _persister.MarkDirectoryProcessedAsync(folderId, transaction, token).ConfigureAwait(false);
    }

    /// <summary>
    /// A folder was deleted
    /// </summary>
    /// <param name="folderId"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task WorkDeletedAsync(long folderId, IDbTransaction transaction, CancellationToken token)
    {
      return Task.CompletedTask;
    }

    /// <summary>
    /// A folder was changed
    /// </summary>
    /// <param name="folderId"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task WorkChangedAsync(long folderId, IDbTransaction transaction, CancellationToken token)
    {
      // Get the files currently on record
      // this can be null if we have nothing.
      var filesOnRecord = await GetFilesAsync(folderId, transaction, token).ConfigureAwait(false);

      // look for the directory name, if we found nothing on record
      // then we will have to go and look in the database.
      DirectoryInfo directory;
      if (null == filesOnRecord || !filesOnRecord.Any())
      {
        directory = await GetDirectoryAsync(folderId, transaction, token).ConfigureAwait(false);
      }
      else
      {
        var fi = filesOnRecord.First();
        directory = fi.Directory;
      }

      // if we have no directory then we have nothing...
      if (directory == null)
      {
        // we are done 
        return;
      }

      // then get the ones in the file.
      var filesOnFile = await _directory.ParseDirectoryAsync(directory, token).ConfigureAwait(false);

      // we want to add all the files that are on disk but not on record.
      var filesToAdd = helper.File.RelativeComplement(filesOnRecord, filesOnFile);

      // we want to remove all the files that are on record but not on file.
      var filesToRemove = helper.File.RelativeComplement(filesOnFile, filesOnRecord);

      // We know that the helper functions never return anything null...
      if (filesToRemove.Any())
      {
        await _persister.DeleteFilesAsync(filesToRemove, transaction, token).ConfigureAwait(false);
      }

      // anything to add?
      if (filesToAdd.Any())
      {
        await _persister.AddOrUpdateFilesAsync(filesToAdd, transaction, token).ConfigureAwait(false);
      }

      // mark it as processed.
      await _persister.MarkDirectoryProcessedAsync(folderId, transaction, token).ConfigureAwait(false);
    }
    
    /// <summary>
    /// Get the folder updates
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<List<PendingFolderUpdate>> GetPendingFolderUpdatesAsync( CancellationToken token)
    {
      // get the transaction
      var transaction = await _persister.BeginTransactionAsync(token).ConfigureAwait(false);
      if (null == transaction)
      {
        //  we probably cancelled.
        return null;
      }
      try
      {

        var pendingUpdates = await _persister.GetPendingFolderUpdatesAsync(_currentNumberOfFoldersToProcess, transaction, token).ConfigureAwait(false);
        if (null == pendingUpdates)
        {
          _logger.Error("Unable to get any pending folder updates.");
          // we will now return null
          // but at least we will commit.
        }

        _persister.Commit(transaction);

        // return null if we cancelled.
        return pendingUpdates;
      }
      catch (OperationCanceledException)
      {
        _persister.Rollback(transaction);
        throw;
      }
      catch (Exception e)
      {
        _persister.Rollback(transaction);
        _logger.Exception(e);
        return null;
      }
    }

    /// <summary>
    /// Get a list of files in a directory
    /// </summary>
    /// <param name="folderId"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<List<FileInfo>> GetFilesAsync(long folderId, IDbTransaction transaction, CancellationToken token)
    {
      return await _persister.GetFilesAsync(folderId, transaction, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Get a directory given a folder id.
    /// </summary>
    /// <param name="folderId"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<DirectoryInfo> GetDirectoryAsync(long folderId, IDbTransaction transaction, CancellationToken token)
    {
      return await _persister.GetDirectoryAsync(folderId, transaction, token).ConfigureAwait(false);
    }
  }
}
