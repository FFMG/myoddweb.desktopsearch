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

    public Files(List<IFileParser> parsers, long numberOfFilesToProcessPerEvent, long maxNumberOfMs,
      IPersister persister, ILogger logger)
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
        var calc = (long) Math.Ceiling(_currentNumberOfFilesToProcess * 1.1);
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
      _currentNumberOfFilesToProcess = (long) (_currentNumberOfFilesToProcess * 0.95);

      // and make suse that we do not have silly values.
      if (_currentNumberOfFilesToProcess <= 0)
      {
        _currentNumberOfFilesToProcess = 1;
      }
    }

    private async Task ProcessFileUpdates(List<PendingFileUpdate> pendingUpdates, int start, CancellationToken token)
    {
      // make sure that we never do more than the total number of items
      // from our current start position.
      var max = pendingUpdates.Count;
      var count = _numberOfFilesToProcess + start > max ? max - start : (int) _numberOfFilesToProcess;

      // and process those files.
      await ProcessFileUpdates(pendingUpdates.GetRange(start, count), token).ConfigureAwait(false);
    }

    /// <summary>
    /// Process a group of pending updates
    /// </summary>
    /// <param name="pendingUpdates"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task ProcessFileUpdates(IReadOnlyCollection<PendingFileUpdate> pendingUpdates,
      CancellationToken token)
    {
      if (null == pendingUpdates || !pendingUpdates.Any())
      {
        // this is not an error, just nothing to do.
        return;
      }

      try
      {
        // process all the data one at a time.
        foreach (var pendingFileUpdate in pendingUpdates)
        {
          // get out if needed.
          token.ThrowIfCancellationRequested();

          // try and process the file
          await ProcessFileUpdate(pendingFileUpdate, token).ConfigureAwait(false);
        }
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Process files update");
        throw;
      }
    }

    /// <summary>
    /// Process a single list update
    /// </summary>
    /// <param name="pendingFileUpdate"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task ProcessFileUpdate(PendingFileUpdate pendingFileUpdate, CancellationToken token)
    {
      var transaction = await _persister.BeginTransactionAsync(token).ConfigureAwait(false);
      if (null == transaction)
      {
        return;
      }

      try
      {
        // process that files.
        await ProcessFileUpdateWithinTransactionAsync(pendingFileUpdate, transaction, token).ConfigureAwait(false);

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

    private async Task ProcessFileUpdateWithinTransactionAsync(PendingFileUpdate pendingFileUpdate, IDbTransaction transaction, CancellationToken token)
    {
      try
      {
        switch (pendingFileUpdate.PendingUpdateType)
        {
          case UpdateType.Created:
            await WorkCreatedAsync(pendingFileUpdate.FileId, transaction, token).ConfigureAwait(false);
            break;

          case UpdateType.Deleted:
            await WorkDeletedAsync(pendingFileUpdate.FileId, transaction, token).ConfigureAwait(false);
            break;

          case UpdateType.Changed:
            // renamed or content/settingss changed
            await WorkChangedAsync(pendingFileUpdate.FileId, transaction, token).ConfigureAwait(false);
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
    /// Get the folder updates
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<List<PendingFileUpdate>> GetPendingFileUpdatesAsync( CancellationToken token)
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
        var pendingUpdates = await _persister.GetPendingFileUpdatesAsync(_currentNumberOfFilesToProcess, transaction, token).ConfigureAwait(false);
        if (null == pendingUpdates)
        {
          _logger.Error("Unable to get any pending files updates.");
          // we will now return null
          _persister.Rollback(transaction);
          return null;
        }

        _persister.Commit(transaction);
        return pendingUpdates;
      }
      catch (OperationCanceledException)
      {
        _persister.Rollback(transaction);
        _logger.Warning("Received cancellation request - Get pending updates");
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
    /// A folder was deleted
    /// </summary>
    /// <param name="fileId"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<bool> WorkDeletedAsync(long fileId, IDbTransaction transaction, CancellationToken token)
    {
      // because the file was deleted from the db  we must remove the words for it.
      // we could technically end up with orphan words, (words with no id)
      // but that's not really that important.
      await DeleteFileFromFilesAndWordsAsync(fileId, transaction, token).ConfigureAwait(false);

      // always true ... otherwsise we throw.
      return true;
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
      var file = await GetFileAsync(fileId, transaction, token).ConfigureAwait(false);
      if (null == file)
      {
        _logger.Warning( $"Unable to find, and process created file id: {fileId}.");
        return true;
      }

      //  process it...
      await ProcessFile( file, fileId, transaction, token ).ConfigureAwait(false);

      // return that the work was complete.
      return true;
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
      var file = await GetFileAsync(fileId, transaction, token).ConfigureAwait(false);
      if (null == file)
      {
        _logger.Warning($"Unable to find, and process changed id: {fileId}.");
        return true;
      }

      //  process it...
      await ProcessFile(file, fileId, transaction, token).ConfigureAwait(false);

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
      var tasks = new List<Task<Words>>();
      foreach (var parser in _parsers)
      {
        tasks.Add( ProcessFile(parser, file, token));
      }

      // do we have any work to do?
      if (!tasks.Any())
      {
        await DeleteFileFromFilesAndWordsAsync(fileId, transaction, token).ConfigureAwait(false);
        return;
      }

      // wait for all the parsers to do their work
      var totalWords = await Task.WhenAll(tasks.ToArray()).ConfigureAwait(false);

      // merge them all into one.
      var words = new Words( totalWords );
  
      // do we have any work to do?
      if (!words.Any())
      {
        // we found no words ... so remove whatever we might have.
        await DeleteFileFromFilesAndWordsAsync(fileId, transaction, token).ConfigureAwait(false);
        return;
      }

      // add all the words of that file
      await _persister.AddOrUpdateWordsToFileAsync( words, fileId, transaction, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Delete all the wods for that file, (if there were any)
    /// And mark it as processed.
    /// </summary>
    /// <param name="fileId"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task DeleteFileFromFilesAndWordsAsync(long fileId, IDbTransaction transaction, CancellationToken token)
    {
      // remove the words
      await _persister.DeleteFileFromFilesAndWordsAsync(fileId, transaction, token).ConfigureAwait(false);
    }

    private async Task<Words> ProcessFile( IFileParser parser, FileInfo file, CancellationToken token)
    {
      if( !parser.Supported(file))
      {
        // nothing to do.
        return new Words();
      }

      // look for the words
      var words = await parser.ParseAsync(file, _logger, token).ConfigureAwait( false );
      if (words.Any())
      {
        // if we found any, log it.
        _logger.Verbose($"Parser : {parser.Name} processed {words.Count} words.");
      }
      return words;
    }

    /// <summary>
    /// Get a File from the persister.
    /// </summary>
    /// <param name="fileId"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<FileInfo> GetFileAsync( long fileId, IDbTransaction transaction, CancellationToken token)
    {
      // get the file that changed.
      return await _persister.GetFileAsync(fileId, transaction, token).ConfigureAwait(false);
    }
  }
}
