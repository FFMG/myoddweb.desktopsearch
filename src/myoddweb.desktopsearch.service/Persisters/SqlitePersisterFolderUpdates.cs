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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Enums;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal class SqlitePersisterFolderUpdates : IFolderUpdates
  {
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
    public async Task<bool> TouchDirectoriesAsync(IReadOnlyCollection<DirectoryInfo> directories, UpdateType type, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      // if it is not a valid id then there is nothing for us to do.
      if (!directories.Any())
      {
        return true;
      }

      var folderIds = await _folders.GetDirectoriesIdAsync(directories, connectionFactory, token, false).ConfigureAwait(false);

      // we can now process all the folders.
      return await TouchDirectoriesAsync(folderIds, type, connectionFactory, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> TouchDirectoriesAsync(IReadOnlyCollection<long> folderIds, UpdateType type, IConnectionFactory connectionFactory, CancellationToken token)
    {
      // first mark that folder id as procesed.
      if (!await MarkDirectoriesProcessedAsync( folderIds, connectionFactory, token).ConfigureAwait(false))
      {
        return false;
      }

      var sqlInsert = $"INSERT INTO {Tables.FolderUpdates} (folderid, type, ticks) VALUES (@id, @type, @ticks)";
      using (var cmd = connectionFactory.CreateCommand(sqlInsert))
      {
        var pId = cmd.CreateParameter();
        pId.DbType = DbType.Int64;
        pId.ParameterName = "@id";
        cmd.Parameters.Add(pId);

        var pType = cmd.CreateParameter();
        pType.DbType = DbType.Int64;
        pType.ParameterName = "@type";
        cmd.Parameters.Add(pType);

        var pTicks = cmd.CreateParameter();
        pTicks.DbType = DbType.Int64;
        pTicks.ParameterName = "@ticks";
        cmd.Parameters.Add(pTicks);
        try
        {
          foreach (var folderId in folderIds)
          {
            token.ThrowIfCancellationRequested();

            pId.Value = folderId;
            pType.Value = (long) type;
            pTicks.Value = DateTime.UtcNow.Ticks;
            if (0 == await connectionFactory.ExecuteWriteAsync(cmd, token).ConfigureAwait(false))
            {
              // not sure why we did not throw here...
              _logger.Error($"There was an issue adding folder the folder update: {folderId} to persister");
            }
          }
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
      }

      // return if this cancelled or not
      return true;
    }

    /// <inheritdoc />
    public async Task<bool> MarkDirectoriesProcessedAsync( IReadOnlyCollection<DirectoryInfo> directories, IConnectionFactory connectionFactory, CancellationToken token)
    {
      var folderIds = await _folders.GetDirectoriesIdAsync(directories, connectionFactory, token, false).ConfigureAwait(false);

      // we can now use the folder id to flag this as done.
      return await MarkDirectoriesProcessedAsync(folderIds, connectionFactory, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> MarkDirectoryProcessedAsync(long folderId, IConnectionFactory connectionFactory,CancellationToken token)
    {
      // if it is not a valid id then there is nothing for us to do.
      if (folderId < 0)
      {
        return true;
      }
      return await MarkDirectoriesProcessedAsync(new List<long> {folderId}, connectionFactory, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> MarkDirectoriesProcessedAsync(IEnumerable<long> folderIds, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      var sqlDelete = $"DELETE FROM {Tables.FolderUpdates} WHERE folderid = @id";
      using (var cmd = connectionFactory.CreateCommand(sqlDelete))
      {
        try
        {
          var pId = cmd.CreateParameter();
          pId.DbType = DbType.Int64;
          pId.ParameterName = "@id";
          cmd.Parameters.Add(pId);

          foreach (var folderId in folderIds)
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // set the folder id.
            pId.Value = folderId;

            // this could return 0 if the row has already been processed
            await connectionFactory.ExecuteWriteAsync(cmd, token).ConfigureAwait(false);
          }
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
      }

      // return if this cancelled or not
      return true;
    }

    /// <inheritdoc />
    public async Task<List<PendingFolderUpdate>> GetPendingFolderUpdatesAsync(long limit, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      // the pending updates
      var pendingUpdates = new List<PendingFolderUpdate>();
      try
      {
        // we want to get the latest updated folders.
        var sql = $"SELECT fu.folderid as folderid, fu.type as type, f.path as path FROM {Tables.FolderUpdates} fu, {Tables.Folders} f WHERE f.id=fu.folderid "+
                  $"ORDER BY fu.ticks DESC LIMIT { limit}";
        using (var cmd = connectionFactory.CreateCommand(sql))
        {
          var reader = await connectionFactory.ExecuteReadAsync(cmd, token).ConfigureAwait(false);
          while (reader.Read())
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // the folder id
            var folderId = (long) reader["folderid"];

            // the directory for that folder
            var directory = new DirectoryInfo((string) reader["path"]);

            // the update type
            var type = (UpdateType) (long) reader["type"];

            // Get the files currently on record this can be null if we have nothing.
            // if the folder was just created we are not going to bother getting more data.
            var filesOnRecord = type == UpdateType.Created ? new List<FileInfo>() : await _files.GetFilesAsync(folderId, connectionFactory, token).ConfigureAwait(false);

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
