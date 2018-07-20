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
    private const long NumberOfFoldersToProcess = 20;

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
    private readonly IPersister _perister;

    /// <summary>
    /// True if start has been called.
    /// </summary>
    private bool _canWork;
    #endregion

    public Folders(IPersister persister, ILogger logger, IDirectory directory)
    {
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
      try
      {
        // if we cannot work ... then don't
        if (false == _canWork)
        {
          return;
        }

        var pendingUpdates = await GetPendingFolderUpdatesAsync(token);
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
            case FolderUpdateType.Created:
              await WorkCreatedAsync(pendingFolderUpdate.FolderId, token).ConfigureAwait(false);
              break;

            case FolderUpdateType.Deleted:
              await WorkDeletedAsync(pendingFolderUpdate.FolderId, token).ConfigureAwait(false);
              break;

            case FolderUpdateType.Changed:
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
        _perister.Commit();
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
    public async Task WorkDeletedAsync(long folderId, CancellationToken token)
    {
    }

    /// <summary>
    /// A folder was changed
    /// </summary>
    /// <param name="folderId"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task WorkChangedAsync(long folderId, CancellationToken token)
    {
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
        _perister.Commit();
        return true;
      }
      catch (Exception e)
      {
        _perister.Rollback();
        _logger.Exception(e);
        return false;
      }
    }

    private async Task<List<PendingFolderUpdate>> GetPendingFolderUpdatesAsync(CancellationToken token)
    {
      var transaction = await _perister.BeginTransactionAsync().ConfigureAwait(false);
      try
      {
        var pendingUpdates = await _perister.GetPendingFolderUpdatesAsync(NumberOfFoldersToProcess, transaction, token).ConfigureAwait(false);
        if (null == pendingUpdates)
        {
          _perister.Rollback();
          _logger.Error( "Unable to get any pending folder updates." );
          return null;
        }
        _perister.Commit();
        return pendingUpdates;
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
