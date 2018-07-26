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
using System.Data.Common;
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
    private readonly IPersister _perister;

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
      _perister = persister ?? throw new ArgumentNullException(nameof(persister));

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
    public async Task WorkAsync(CancellationToken token)
    {
      // if we cannot work ... then don't
      if (false == _canWork)
      {
        return;
      }

      // get the transaction
      var transaction = await _perister.BeginTransactionAsync().ConfigureAwait(false);
      try
      {
        var pendingUpdates = await GetPendingFileUpdatesAsync(transaction, token).ConfigureAwait(false);
        if (null == pendingUpdates || !pendingUpdates.Any())
        {
          _perister.Rollback(transaction);
          return;
        }

        // process all the data one at a time.
        foreach (var pendingFolderUpdate in pendingUpdates)
        {
          if (token.IsCancellationRequested)
          {
            _perister.Rollback(transaction);
            return;
          }

          switch (pendingFolderUpdate.PendingUpdateType)
          {
            case UpdateType.Created:
              await WorkCreatedAsync(pendingFolderUpdate.FileId, transaction, token).ConfigureAwait(false);
              break;

            case UpdateType.Deleted:
              await WorkDeletedAsync(pendingFolderUpdate.FileId, transaction, token).ConfigureAwait(false);
              break;

            case UpdateType.Changed:
              // renamed or content/settingss changed
              await WorkChangedAsync(pendingFolderUpdate.FileId, transaction, token).ConfigureAwait(false);
              break;

            default:
              throw new ArgumentOutOfRangeException();
          }
        }

        // remove the ones we processed.
        await MarkDirectoriesProcessedAsync(pendingUpdates, transaction, token).ConfigureAwait(false);

        // all done
        if (!token.IsCancellationRequested)
        {
          _perister.Commit(transaction);
        }
        else
        {
          _perister.Rollback(transaction);
        }
      }
      catch (Exception e)
      {
        _perister.Rollback(transaction);
        _logger.Exception(e);
      }
    }

    private async Task<bool> MarkDirectoriesProcessedAsync(IEnumerable<PendingFileUpdate> pendingUpdates, IDbTransaction transaction, CancellationToken token)
    {
      return await _perister.MarkFilesProcessedAsync(pendingUpdates.Select(p => p.FileId).ToList(), transaction, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Get the folder updates
    /// </summary>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<List<PendingFileUpdate>> GetPendingFileUpdatesAsync(IDbTransaction transaction, CancellationToken token)
    {
      var pendingUpdates = await _perister.GetPendingFileUpdatesAsync(_numberOfFilesToProcess, transaction, token).ConfigureAwait(false);
      if (null == pendingUpdates)
      {
        _logger.Error("Unable to get any pending files updates.");
        return null;
      }

      // return if we cancelled.
      return !token.IsCancellationRequested ? pendingUpdates : null;
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
      var file = await _perister.GetFileAsync(fileId, transaction, token).ConfigureAwait(false);

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
      var file = await _perister.GetFileAsync(fileId, transaction, token).ConfigureAwait(false);

      //  process it...
      await ProcessFile(fileId, file, transaction, token).ConfigureAwait(false);

      // return if we cancelled.
      return !token.IsCancellationRequested;
    }
  }
}
