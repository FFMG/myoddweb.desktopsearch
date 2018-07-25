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
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal partial class Persister
  {
    /// <inheritdoc />
    public async Task<bool> TouchDirectoryAsync(DirectoryInfo directory, UpdateType type, IDbTransaction transaction, CancellationToken token)
    {
      if (null == transaction)
      {
        throw new ArgumentNullException(nameof(transaction), "You have to be within a tansaction when calling this function.");
      }

      var folderId = await GetDirectoryIdAsync(directory, transaction, token, false).ConfigureAwait(false);
      if (-1 == folderId)
      {
        return !token.IsCancellationRequested;
      }

      // we can then use the transaction id to touch the folder.
      return await TouchDirectoryAsync(folderId, type, transaction, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> TouchDirectoryAsync(long folderId, UpdateType type, IDbTransaction transaction,CancellationToken token)
    {
      // if it is not a valid id then there is nothing for us to do.
      if (folderId < 0)
      {
        return !token.IsCancellationRequested;
      }

      if (null == transaction)
      {
        throw new ArgumentNullException(nameof(transaction), "You have to be within a tansaction when calling this function.");
      }

      // first mark that folder id as procesed.
      if (!await MarkDirectoryProcessedAsync(folderId, transaction, token).ConfigureAwait(false))
      {
        return false;
      }

      var sqlInsert = $"INSERT INTO {TableFolderUpdates} (folderid, type, ticks) VALUES (@id, @type, @ticks)";
      using (var cmd = CreateDbCommand(sqlInsert, transaction))
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
          cmd.Parameters["@id"].Value = folderId;
          cmd.Parameters["@type"].Value = (long) type;
          cmd.Parameters["@ticks"].Value = DateTime.UtcNow.Ticks;
          if (0 == await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false))
          {
            _logger.Error($"There was an issue adding folder the folder update: {folderId} to persister");
            return false;
          }
        }
        catch (Exception ex)
        {
          _logger.Exception(ex);
          return false;
        }
      }

      // return if this cancelled or not
      return !token.IsCancellationRequested;
    }

    /// <inheritdoc />
    public async Task<bool> MarkDirectoryProcessedAsync(DirectoryInfo directory, IDbTransaction transaction, CancellationToken token)
    {
      var folderId = await GetDirectoryIdAsync(directory, transaction, token, false).ConfigureAwait(false);
      if (-1 == folderId)
      {
        return !token.IsCancellationRequested;
      }

      // we can now use the folder id to flag this as done.
      return await MarkDirectoryProcessedAsync(folderId, transaction, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> MarkDirectoryProcessedAsync(long folderId, IDbTransaction transaction,CancellationToken token)
    {
      // if it is not a valid id then there is nothing for us to do.
      if (folderId < 0)
      {
        return true;
      }
      return await MarkDirectoriesProcessedAsync(new List<long> {folderId}, transaction, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> MarkDirectoriesProcessedAsync(IEnumerable<long> folderIds, IDbTransaction transaction, CancellationToken token)
    {
      if (null == transaction)
      {
        throw new ArgumentNullException(nameof(transaction), "You have to be within a tansaction when calling this function.");
      }

      var sqlDelete = $"DELETE FROM {TableFolderUpdates} WHERE folderid = @id";
      using (var cmd = CreateDbCommand(sqlDelete, transaction))
      {
        var pId = cmd.CreateParameter();
        pId.DbType = DbType.Int64;
        pId.ParameterName = "@id";
        cmd.Parameters.Add(pId);
        try
        {
          foreach (var folderId in folderIds )
          {
            // are we cancelling?
            if (token.IsCancellationRequested)
            {
              return false;
            }

            // set the folder id.
            cmd.Parameters["@id"].Value = folderId;

            // this could return 0 if the row has already been processed
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
          }
        }
        catch (Exception ex)
        {
          _logger.Exception(ex);
          return false;
        }
      }

      // return if this cancelled or not
      return !token.IsCancellationRequested;
    }

    /// <inheritdoc />
    public async Task<List<PendingFolderUpdate>> GetPendingFolderUpdatesAsync(long limit, IDbTransaction transaction, CancellationToken token)
    {
      if (null == transaction)
      {
        throw new ArgumentNullException(nameof(transaction), "You have to be within a tansaction when calling this function.");
      }

      // the pending updates
      var pendingUpdates = new List<PendingFolderUpdate>();
      try
      {
        // we want to get the latest updated folders.
        var sql = $"SELECT folderid, type FROM {TableFolderUpdates} ORDER BY ticks DESC LIMIT {limit}";
        using (var cmd = CreateDbCommand(sql, transaction))
        {
          var reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
          while (reader.Read())
          {
            // are we cancelling?
            if (token.IsCancellationRequested)
            {
              return null;
            }

            // add this update
            pendingUpdates.Add(new PendingFolderUpdate(
                (long)reader["folderid"],
                (UpdateType)(long)reader["type"]
              ));
          }
        }
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        return null;
      }

      // return whatever we found
      return token.IsCancellationRequested ? null : pendingUpdates;
    }
  }
}
