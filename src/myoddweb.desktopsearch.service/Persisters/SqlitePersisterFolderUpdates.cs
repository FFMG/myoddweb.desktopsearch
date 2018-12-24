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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.helper.Persisters;
using myoddweb.desktopsearch.interfaces.Enums;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal class SqlitePersisterFolderUpdates : IFolderUpdates
  {
    /// <inheritdoc />
    public IConnectionFactory Factory { get; set; }

    /// <summary>
    /// The folder updates helper
    /// </summary>
    private FolderUpdatesHelper _foldersUpdatesHelper;

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The folders interface
    /// </summary>
    private readonly IFolders _folders;

    /// <summary>
    /// The files interface.
    /// </summary>
    private readonly IFiles _files;

    public SqlitePersisterFolderUpdates(IFiles files, IFolders folders, ILogger logger)
    {
      //  the files interface.
      _folders = folders ?? throw new ArgumentNullException(nameof(folders));

      //  the files interface.
      _files = files ?? throw new ArgumentNullException(nameof(files));

      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public void Prepare(IPersister persister, IConnectionFactory factory)
    {
      // no readonly event posible here.
      if (factory.IsReadOnly)
      {
        return;
      }

      // sanity check.
      Contract.Assert(_foldersUpdatesHelper == null);
      Contract.Assert(Factory == null );
      _foldersUpdatesHelper = new FolderUpdatesHelper(factory, Tables.FolderUpdates);
      Factory = factory;
    }

    /// <inheritdoc />
    public void Complete(IConnectionFactory factory, bool success)
    {
      if (Factory != factory )
      {
        return;
      }

      _foldersUpdatesHelper?.Dispose();
      _foldersUpdatesHelper = null;
      Factory = null;
    }

    /// <inheritdoc />
    public async Task<bool> TouchDirectoriesAsync(IReadOnlyCollection<DirectoryInfo> directories, UpdateType type, CancellationToken token)
    {
      // if it is not a valid id then there is nothing for us to do.
      if (!directories.Any())
      {
        return true;
      }

      var folderIds = await _folders.GetDirectoriesIdAsync(directories, token, false).ConfigureAwait(false);

      // we can now process all the folders.
      return await TouchDirectoriesAsync(folderIds, type, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> TouchDirectoriesAsync(IReadOnlyCollection<long> folderIds, UpdateType type, CancellationToken token)
    {
      // first mark that folder id as procesed.
      if (!await MarkDirectoriesProcessedAsync( folderIds, token).ConfigureAwait(false))
      {
        return false;
      }

      // sanity check
      Contract.Assert( _foldersUpdatesHelper != null );

      try
      {
        await _foldersUpdatesHelper.TouchAsync(folderIds, type, token).ConfigureAwait(false);
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Rebuild directories list");
        throw;
      }
      catch (Exception ex)
      {
        _logger.Exception(ex);
        throw;
      }

      // return if this cancelled or not
      return true;
    }

    /// <inheritdoc />
    public async Task<bool> MarkDirectoriesProcessedAsync( IReadOnlyCollection<DirectoryInfo> directories, CancellationToken token)
    {
      var folderIds = await _folders.GetDirectoriesIdAsync(directories, token, false).ConfigureAwait(false);

      // we can now use the folder id to flag this as done.
      return await MarkDirectoriesProcessedAsync(folderIds, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> MarkDirectoryProcessedAsync(long folderId,CancellationToken token)
    {
      // if it is not a valid id then there is nothing for us to do.
      if (folderId < 0)
      {
        return true;
      }
      return await MarkDirectoriesProcessedAsync(new List<long> {folderId}, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> MarkDirectoriesProcessedAsync(IEnumerable<long> folderIds, CancellationToken token)
    {
      try
      {
        // sanity check
        Contract.Assert( _foldersUpdatesHelper != null );
        await _foldersUpdatesHelper.DeleteAsync(folderIds.ToList(), token).ConfigureAwait(false);
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Delete multiple directories from update list");
        throw;
      }
      catch (Exception ex)
      {
        _logger.Exception(ex);
        throw;
      }

      // return if this cancelled or not
      return true;
    }

    /// <inheritdoc />
    public async Task<IList<IPendingFolderUpdate>> GetPendingFolderUpdatesAsync(long limit, IConnectionFactory factory, CancellationToken token)
    {
      if (null == factory)
      {
        throw new ArgumentNullException(nameof(factory), "You have to be within a tansaction when calling this function.");
      }

      // the pending updates
      var pendingUpdates = new List<IPendingFolderUpdate>();
      try
      {
        // we want to get the latest updated folders.
        var sql = $"SELECT fu.folderid as folderid, fu.type as type, f.path as path FROM {Tables.FolderUpdates} fu, {Tables.Folders} f WHERE f.id=fu.folderid "+
                  $"ORDER BY fu.ticks DESC LIMIT { limit}";
        using (var cmd = factory.CreateCommand(sql))
        using (var reader = await factory.ExecuteReadAsync(cmd, token).ConfigureAwait(false))
        {
          var folderIdPos = reader.GetOrdinal("folderid");
          var pathPos = reader.GetOrdinal("path");
          var typePos = reader.GetOrdinal("type");
          while (reader.Read())
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // the folder id
            var folderId = (long)reader[folderIdPos];

            // the directory for that folder
            var directory = new DirectoryInfo((string)reader[pathPos]);

            // the update type
            var type = (UpdateType)(long)reader[typePos];

            // Get the files currently on record this can be null if we have nothing.
            // if the folder was just created we are not going to bother getting more data.
            var filesOnRecord = type == UpdateType.Created ? new List<FileInfo>() : await _files.GetFilesAsync(folderId, token).ConfigureAwait(false);

            // add this update
            pendingUpdates.Add(new PendingFolderUpdate(
              folderId,
              directory,
              filesOnRecord,
              type
            ));
          }
        }
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Get pending folder updates");
        throw;
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        return null;
      }

      // return whatever we found
      return pendingUpdates;
    }
  }
}
