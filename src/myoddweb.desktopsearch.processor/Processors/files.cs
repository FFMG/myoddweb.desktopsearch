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
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.processor.Processors
{
  internal class Files : IProcessor
  {
    #region Member Variables
    /// <summary>
    /// The number of files we want to process.
    /// </summary>
    private readonly long _numberOfFilesToProcess;

    /// <summary>
    /// The number of files we want to process.
    /// </summary>
    private long _currentNumberOfFilesToProcess;

    /// <summary>
    /// The maximum amount of time a cycle should take.
    /// </summary>
    private readonly long _maxNumberOfMs;

    /// <summary>
    /// The logger that we will be using to log messages.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The persister.
    /// </summary>
    private readonly IPersister _persister;

    /// <summary>
    /// True if start has been called.
    /// </summary>
    private bool _canWork;
    #endregion

    public Files(long numberOfFilesToProcessPerEvent, long maxNumberOfMs, IPersister persister, ILogger logger)
    {
      // The number of files to process
      _numberOfFilesToProcess = numberOfFilesToProcessPerEvent;
      _currentNumberOfFilesToProcess = numberOfFilesToProcessPerEvent;

      // save the max amount of time we want to take.
      _maxNumberOfMs = maxNumberOfMs;

      // set the persister.
      _persister = persister ?? throw new ArgumentNullException(nameof(persister));

      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
        var pendingUpdates = await GetPendingFileUpdatesAsync(token).ConfigureAwait(false);

        // the number of updates we actually did.
        long processedFiles = 0;
        try
        {
          // then we go around and do chunks of data one at a time.
          // this is to prevent massive chunks of data from being processed.
          for (var start = 0; start < pendingUpdates.Count; start += (int)_numberOfFilesToProcess)
          {
            // get out if we cancelled.
            if (token.IsCancellationRequested)
            {
              break;
            }

            if (!await ProcessFileUpdates(pendingUpdates, start, token).ConfigureAwait(false))
            {
              return false;
            }

            // if we are here we processed those files.
            processedFiles += _numberOfFilesToProcess;
          }

          // return if we made it.
          return !token.IsCancellationRequested;
        }
        finally
        {
          // now display how many files were actually handled.
          stopwatch.Stop();
          _logger.Verbose($"Processed {(processedFiles > (pendingUpdates?.Count ?? 0) ? (pendingUpdates?.Count ?? 0) : processedFiles)} pending file updates (Time Elapsed: {stopwatch.Elapsed:g})");

          // Adjust the number of items we will be doing the next time.
          AdjustNumberOfFilesToProcess(processedFiles, stopwatch.ElapsedMilliseconds);
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
    /// <param name="processedFiles"></param>
    /// <param name="elapsedMilliseconds"></param>
    private void AdjustNumberOfFilesToProcess(long processedFiles, long elapsedMilliseconds)
    {
      // did we do as many as we could do? If not it means that there are simply not enough
      // files to process anymore, in the case, the amount of time does not really matter.
      // We could also have broken out of the list...
      // 'processedFiles' could be bigger than the number to process because of the loop above.
      if (processedFiles < _currentNumberOfFilesToProcess)
      {
        return;
      }

      // did we go faster than expected?
      if (elapsedMilliseconds < _maxNumberOfMs * 0.9) //  remove a little bit so we don't always re-adjust.
      {
        //  just add 10%
        _currentNumberOfFilesToProcess = (long)(_currentNumberOfFilesToProcess * 1.1);
        return;
      }

      // or did we go slower?
      // we never want to go over the time limit.
      if (elapsedMilliseconds <= _maxNumberOfMs)
      {
        return;
      }

      // remove 5%
      _currentNumberOfFilesToProcess = (long)(_currentNumberOfFilesToProcess * 0.95);

      // and make suse that we do not have silly values.
      if (_currentNumberOfFilesToProcess <= 0)
      {
        _currentNumberOfFilesToProcess = 1;
      }
    }

    private async Task<bool> ProcessFileUpdates(List<PendingFileUpdate> pendingUpdates, int start, CancellationToken token)
    {
      // make sure that we never do more than the total number of items
      // from our current start position.
      var max = pendingUpdates.Count;
      var count = _numberOfFilesToProcess + start > max ? max - start : (int)_numberOfFilesToProcess;

      // and process those files.
      return await ProcessFileUpdates(pendingUpdates.GetRange( start, count), token).ConfigureAwait(false);
    }

    /// <summary>
    /// Process a group of pending updates
    /// </summary>
    /// <param name="pendingUpdates"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> ProcessFileUpdates(IReadOnlyCollection<PendingFileUpdate> pendingUpdates, CancellationToken token)
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
        foreach (var pendingFileUpdate in pendingUpdates)
        {
          if (token.IsCancellationRequested)
          {
            return false;
          }

          if( !await ProcessFileUpdate(transaction, pendingFileUpdate, token).ConfigureAwait(false))
          {
            // something went wrong or the task was cancelled.
            return false;
          }
        }

        // mark all the files as done.
        if (!await _persister.MarkFilesProcessedAsync(pendingUpdates.Select(u => u.FileId), transaction, token).ConfigureAwait(false))
        {
          _persister.Rollback(transaction);
          return false;
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
    /// Process a single list update
    /// </summary>
    /// <param name="transaction"></param>
    /// <param name="pendingFileUpdate"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> ProcessFileUpdate( IDbTransaction transaction, PendingFileUpdate pendingFileUpdate, CancellationToken token)
    {
      try
      {
        if (token.IsCancellationRequested)
        {
          return false;
        }

        switch (pendingFileUpdate.PendingUpdateType)
        {
          case UpdateType.Created:
            return await WorkCreatedAsync(pendingFileUpdate.FileId, transaction, token).ConfigureAwait(false);

          case UpdateType.Deleted:
            return await WorkDeletedAsync(pendingFileUpdate.FileId, transaction, token).ConfigureAwait(false);

          case UpdateType.Changed:
            // renamed or content/settingss changed
            return await WorkChangedAsync(pendingFileUpdate.FileId, transaction, token).ConfigureAwait(false);

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
    /// Get the folder updates
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<List<PendingFileUpdate>> GetPendingFileUpdatesAsync( CancellationToken token)
    {
      var transaction = await _persister.BeginTransactionAsync().ConfigureAwait(false);
      if (null == transaction)
      {
        //  we probably cancelled.
        return null;
      }

      try
      {
        var pendingUpdates = await _persister.GetPendingFileUpdatesAsync(_currentNumberOfFilesToProcess, transaction, token).ConfigureAwait(false);
        if (null == pendingUpdates)
        {
          _logger.Error("Unable to get any pending files updates.");
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
    /// A folder was created
    /// </summary>
    /// <param name="fileId"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<bool> WorkCreatedAsync(long fileId, IDbTransaction transaction, CancellationToken token)
    {
      // get the file we just created.
      var file = await _persister.GetFileAsync(fileId, transaction, token).ConfigureAwait(false);

      //  process it...
      await ProcessFile( fileId, file, transaction, token ).ConfigureAwait(false);

      // return if we cancelled.
      return !token.IsCancellationRequested;
    }

    /// <summary>
    /// Process a file.
    /// </summary>
    /// <param name="fileId"></param>
    /// <param name="file"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private Task ProcessFile( long fileId, FileInfo file, IDbTransaction transaction, CancellationToken token)
    {
      return Task.CompletedTask;
    }

    /// <summary>
    /// A folder was changed
    /// </summary>
    /// <param name="fileId"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<bool> WorkChangedAsync(long fileId, IDbTransaction transaction, CancellationToken token)
    {
      // get the file that changed.
      var file = await _persister.GetFileAsync(fileId, transaction, token).ConfigureAwait(false);

      //  process it...
      await ProcessFile(fileId, file, transaction, token).ConfigureAwait(false);

      // return if we cancelled.
      return !token.IsCancellationRequested;
    }
  }
}
