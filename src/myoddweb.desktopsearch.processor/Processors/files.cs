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
    /// All the file parsers.
    /// </summary>
    private readonly List<IFileParser> _parsers;
    #endregion

    public Files( List<IFileParser> parsers, long numberOfFilesToProcessPerEvent, long maxNumberOfMs, IPersister persister, ILogger logger)
    {
      // make sure that the parsers are valid.
      _parsers = parsers ?? throw new ArgumentNullException(nameof(parsers));

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
    public async Task WorkAsync(CancellationToken token)
    {
      try
      {
        // start timing how long the entire operation will take
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        // then get _all_ the file updates that we want to do.
        var pendingUpdates = await GetPendingFileUpdatesAsync(token).ConfigureAwait(false);

        if (null == pendingUpdates)
        {
          //  probably was canceled.
          return;
        }

        // the number of updates we actually did.
        long processedFiles = 0;
        try
        {
          // then we go around and do chunks of data one at a time.
          // this is to prevent massive chunks of data from being processed.
          var tasks = new List<Task>();
          for (var start = 0; start < pendingUpdates.Count; start += (int) _numberOfFilesToProcess)
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // run this group of files.
            tasks.Add(ProcessFileUpdates(pendingUpdates, start, token));

            // if we are here we processed those files.
            processedFiles += _numberOfFilesToProcess;
          }

          // then wait for the tasks to complete.
          await Task.WhenAll(tasks.ToArray()).ConfigureAwait(false);
        }
        finally
        {
          // now display how many files were actually handled.
          stopwatch.Stop();
          if (processedFiles > 0)
          {
            _logger.Verbose(
              $"Processed {(processedFiles > pendingUpdates.Count ? pendingUpdates.Count : processedFiles)} pending file updates (Time Elapsed: {stopwatch.Elapsed:g})");
          }

          // Adjust the number of items we will be doing the next time.
          AdjustNumberOfFilesToProcess(processedFiles, stopwatch.ElapsedMilliseconds);
        }
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Files Processor - Work");
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
        //  just add 10%
        var calc = (long)Math.Ceiling(_currentNumberOfFilesToProcess * 1.1);
        if (_currentNumberOfFilesToProcess == calc)
        {
          _currentNumberOfFilesToProcess = calc + 1;
        }
        else
        {
          _currentNumberOfFilesToProcess = calc;
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

      IDbTransaction transaction = null;
      try
      {
        // get the transaction
        transaction = await _persister.BeginTransactionAsync(token).ConfigureAwait(false);
        if (null == transaction)
        {
          //  we probably cancelled.
          return false;
        }

        // process all the data one at a time.
        foreach (var pendingFileUpdate in pendingUpdates)
        {
          // get out if needed.
          token.ThrowIfCancellationRequested();

          if (await ProcessFileUpdate(transaction, pendingFileUpdate, token).ConfigureAwait(false))
          {
            continue;
          }

          // something did not work, so roll back and get out.
          _persister.Rollback(transaction);
          return false;
        }

        // mark all the files as done.
        if (!await _persister.MarkFilesProcessedAsync(pendingUpdates.Select(u => u.FileId), transaction, token).ConfigureAwait(false))
        {
          _persister.Rollback(transaction);
          return false;
        }

        // we made it!
        _persister.Commit(transaction);
        return true;
      }
      catch (OperationCanceledException)
      {
        if (null != transaction)
        {
          _persister.Rollback(transaction);
        }
        _logger.Warning("Received cancellation request - Process files update");
        throw;
      }
      catch
      {
        if (null != transaction)
        {
          _persister.Rollback(transaction);
        }
        throw;
      }
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
      IDbTransaction transaction = null;
      try
      {
        // get the transaction
        transaction = await _persister.BeginTransactionAsync(token).ConfigureAwait(false);
        if (null == transaction)
        {
          //  we probably cancelled.
          return null;
        }

        var pendingUpdates = await _persister
          .GetPendingFileUpdatesAsync(_currentNumberOfFilesToProcess, transaction, token).ConfigureAwait(false);
        if (null == pendingUpdates)
        {
          _logger.Error("Unable to get any pending files updates.");
          // we will now return null
          // but at least we will commit.

          _persister.Rollback(transaction);
          return null;
        }

        _persister.Commit(transaction);
        return pendingUpdates;
      }
      catch (OperationCanceledException)
      {
        if (null != transaction)
        {
          _persister.Rollback(transaction);
        }
        _logger.Warning("Received cancellation request - Get pending updates");
        throw;
      }
      catch (Exception e)
      {
        if (null != transaction)
        {
          _persister.Rollback(transaction);
        }
        _logger.Exception(e);
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
      await ProcessFile( file, fileId, transaction, token ).ConfigureAwait(false);

      // return if we cancelled.
      return true;
    }

    /// <summary>
    /// Process a file.
    /// </summary>
    /// <param name="file"></param>
    /// <param name="fileId"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task ProcessFile(FileInfo file, long fileId, IDbTransaction transaction, CancellationToken token)
    {
      var tasks = new List<Task>();
      foreach (var parser in _parsers)
      {
        if (!helper.File.IsExtension(file, parser.Extenstions))
        {
          continue;
        }
        tasks.Add( parser.ParseAsync(file, token ) );
      }

      // do we have any work to do?
      if (!tasks.Any())
      {
        return;
      }

      // then wait for them all.
      await Task.WhenAll(tasks.ToArray()).ConfigureAwait(false);
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
      await ProcessFile(file, fileId, transaction, token).ConfigureAwait(false);

      // return if we cancelled.
      return true;
    }
  }
}
