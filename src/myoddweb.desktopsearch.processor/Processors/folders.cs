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

    /// <summary>
    /// True if start has been called.
    /// </summary>
    private bool _canWork;
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
    public async Task<bool> WorkAsync(CancellationToken token)
    {
      // if we cannot work ... then don't
      if (false == _canWork)
      {
        // this is not an error
        return true;
      }

      try
      {
        // start timing how long the entire operation will take
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        // then get _all_ the file updates that we want to do.
        var pendingUpdates = await GetPendingFolderUpdatesAsync(token).ConfigureAwait(false);

        // the number of updates we actually did.
        long processedFolders = 0;
        try
        {
          // then we go around and do chunks of data one at a time.
          // this is to prevent massive chunks of data from being processed.
          for (var start = 0; start < pendingUpdates.Count; start += (int)_numberOfFoldersToProcess)
          {
            // get out if we cancelled.
            if (token.IsCancellationRequested)
            {
              break;
            }

            if (!await ProcessFolderUpdates(pendingUpdates, start, token).ConfigureAwait(false))
            {
              return false;
            }

            // if we are here we processed those files.
            processedFolders += _numberOfFoldersToProcess;
          }

          // return if we made it.
          return !token.IsCancellationRequested;
        }
        finally
        {
          stopwatch.Stop();
          _logger.Verbose($"Processed {(processedFolders > (pendingUpdates?.Count ?? 0) ? (pendingUpdates?.Count ?? 0) : processedFolders)} pending folder updates (Time Elapsed: {stopwatch.Elapsed:g})");

          // Adjust the number of items we will be doing the next time.
          AdjustNumberOfFoldersToProcess(processedFolders, stopwatch.ElapsedMilliseconds);
        }
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        return false;
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
        _currentNumberOfFoldersToProcess = (long)(_currentNumberOfFoldersToProcess * 1.1);
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

    private async Task<bool> ProcessFolderUpdates(List<PendingFolderUpdate> pendingUpdates, int start,CancellationToken token)
    {
      // make sure that we never do more than the total number of items
      // from our current start position.
      var max = pendingUpdates.Count;
      var count = _numberOfFoldersToProcess + start > max ? max - start : (int) _numberOfFoldersToProcess;

      // and process those files.
      return await ProcessFolderUpdates(pendingUpdates.GetRange(start, count), token).ConfigureAwait(false);
    }

    private async Task<bool> ProcessFolderUpdates(IReadOnlyCollection<PendingFolderUpdate> pendingUpdates, CancellationToken token)
    { 
      if (null == pendingUpdates || !pendingUpdates.Any())
      {
        // this is not an error
        return true;
      }

      // get the transaction
      var transaction = await _persister.BeginTransactionAsync().ConfigureAwait(false);
      if (null == transaction)
      {
        //  we probably cancelled.
        return false;
      }

      try
      {
        // process all the data one at a time.
        foreach (var pendingFolderUpdate in pendingUpdates)
        {
          if (token.IsCancellationRequested)
          {
            return false;
          }

          if (!await ProcessFolderUpdate(transaction, pendingFolderUpdate, token).ConfigureAwait(false))
          {
            return false;
          }
        }

        // we made it!
        if (token.IsCancellationRequested)
        {
          _persister.Rollback(transaction);
        }
        else
        {
          _persister.Commit(transaction);
        }
      }
      catch
      {
        _persister.Rollback(transaction);
        throw;
      }
      return !token.IsCancellationRequested;
    }

    /// <summary>
    /// Process a single folder update
    /// </summary>
    /// <param name="transaction"></param>
    /// <param name="pendingFolderUpdate"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> ProcessFolderUpdate(IDbTransaction transaction, PendingFolderUpdate pendingFolderUpdate, CancellationToken token )
    {
      try
      {
        if (token.IsCancellationRequested)
        {
          return false;
        }
        switch (pendingFolderUpdate.PendingUpdateType)
        {
          case UpdateType.Created:
            return await WorkCreatedAsync(pendingFolderUpdate.FolderId, transaction, token).ConfigureAwait(false);

          case UpdateType.Deleted:
            return await WorkDeletedAsync(pendingFolderUpdate.FolderId, transaction, token).ConfigureAwait(false);

          case UpdateType.Changed:
            // renamed or content/settingss changed
            return await WorkChangedAsync(pendingFolderUpdate.FolderId, transaction, token).ConfigureAwait(false);

          default:
            throw new ArgumentOutOfRangeException();
        }
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        return false;
      }
    }

    /// <summary>
    /// A folder was created
    /// </summary>
    /// <param name="folderId"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<bool> WorkCreatedAsync(long folderId, IDbTransaction transaction, CancellationToken token)
    {
      var directory = await _persister.GetDirectoryAsync(folderId, transaction, token).ConfigureAwait(false);
      if (null == directory)
      {
        return false;
      }

      // get the files in that directory.
      var files = await _directory.ParseDirectoryAsync(directory, token).ConfigureAwait(false);
      if (files != null)
      {
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
      }

      // return if we cancelled.
      return !token.IsCancellationRequested;
    }

    /// <summary>
    /// A folder was deleted
    /// </summary>
    /// <param name="folderId"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task<bool> WorkDeletedAsync(long folderId, IDbTransaction transaction, CancellationToken token)
    {
      return Task.FromResult(true);
    }

    /// <summary>
    /// A folder was changed
    /// </summary>
    /// <param name="folderId"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<bool> WorkChangedAsync(long folderId, IDbTransaction transaction, CancellationToken token)
    {
      // Get the files currently on record
      // this can be null if we have nothing.
      var filesOnRecord = await _persister.GetFilesAsync(folderId, transaction, token).ConfigureAwait(false);

      // look for the directory name, if we found nothing on record
      // then we will have to go and look in the database.
      DirectoryInfo directory;
      if (null == filesOnRecord || !filesOnRecord.Any())
      {
        directory = await _persister.GetDirectoryAsync(folderId, transaction, token).ConfigureAwait(false);
      }
      else
      {
        var fi = filesOnRecord.First();
        directory = fi.Directory;
      }

      // if we have no directory then we have nothing...
      if (directory == null)
      {
        return false;
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

      if (filesToAdd.Any())
      {
        await _persister.AddOrUpdateFilesAsync(filesToAdd, transaction, token).ConfigureAwait(false);
      }

      // return if we cancelled.
      return !token.IsCancellationRequested;
    }
    
    /// <summary>
    /// Get the folder updates
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<List<PendingFolderUpdate>> GetPendingFolderUpdatesAsync(CancellationToken token)
    {
      var transaction = await _persister.BeginTransactionAsync().ConfigureAwait(false);
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
          return null;
        }

        if (!token.IsCancellationRequested)
        {
          _persister.Commit(transaction);
        }
        else
        {
          _persister.Rollback(transaction);
        }

        // return null if we cancelled.
        return !token.IsCancellationRequested ? pendingUpdates : null;
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        _persister.Rollback(transaction);
        return null;
      }
    }
  }
}
