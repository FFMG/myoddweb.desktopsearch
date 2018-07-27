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

    public Files(long numberOfFilesToProcessPerEvent, IPersister persister, ILogger logger)
    {
      // The number of files to process
      _numberOfFilesToProcess = numberOfFilesToProcessPerEvent;

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
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        var pendingUpdates = await GetPendingFileUpdatesAsync(token).ConfigureAwait(false);
        try
        {
          if (null == pendingUpdates || !pendingUpdates.Any())
          {
            // this is not an error
            return true;
          }

          // process all the data one at a time.
          foreach (var pendingFileUpdate in pendingUpdates)
          {
            await ProcessFileUpdate(pendingFileUpdate, token).ConfigureAwait(false);
            if (token.IsCancellationRequested)
            {
              return false;
            }
          }
        }
        finally
        {
          stopwatch.Stop();
          _logger.Verbose($"Processed {pendingUpdates?.Count ?? 0} pending file updates (Time Elapsed: {stopwatch.Elapsed:g})");
        }
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        return false;
      }
      return !token.IsCancellationRequested;
    }

    private async Task ProcessFileUpdate(PendingFileUpdate pendingFileUpdate, CancellationToken token)
    {
      var transaction = await _persister.BeginTransactionAsync().ConfigureAwait(false);
      if (null == transaction)
      {
        //  we probably cancelled.
        return;
      }

      try
      {
        if (token.IsCancellationRequested)
        {
          _persister.Rollback(transaction);
          return;
        }

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

        // mark it as done.
        await _persister.MarkFileProcessedAsync(pendingFileUpdate.FileId, transaction, token).ConfigureAwait(false);

        // all done
        if (!token.IsCancellationRequested)
        {
          _persister.Commit(transaction);
        }
        else
        {
          _persister.Rollback(transaction);
        }
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        _persister.Rollback(transaction);
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
        var pendingUpdates = await _persister.GetPendingFileUpdatesAsync(_numberOfFilesToProcess, transaction, token).ConfigureAwait(false);
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
    public Task WorkDeletedAsync(long folderId, IDbTransaction transaction, CancellationToken token)
    {
      return Task.CompletedTask;
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
