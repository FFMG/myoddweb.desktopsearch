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
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal partial class SqlitePersister
  {
    /// <inheritdoc />
    public async Task<bool> TouchFileAsync(FileInfo file, UpdateType type, IDbTransaction transaction, CancellationToken token)
    {
      if (null == transaction)
      {
        throw new ArgumentNullException(nameof(transaction),
          "You have to be within a tansaction when calling this function.");
      }

      var fileId = await GetFileIdAsync(file, transaction, token, false).ConfigureAwait(false);
      if (-1 == fileId)
      {
        return true;
      }

      // then we can do the update
      return await TouchFilesAsync(new [] {fileId}, type, transaction, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> TouchFileAsync(long fileId, UpdateType type, IDbTransaction transaction, CancellationToken token)
    {
      // just make the files do all the work.
      return await TouchFilesAsync(new List<long> { fileId }, type, transaction, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> TouchFilesAsync(IEnumerable<long> fileIds, UpdateType type, IDbTransaction transaction, CancellationToken token)
    {
      if (null == fileIds)
      {
        throw new ArgumentNullException( nameof(fileIds) );
      }

      // if it is not a valid id then there is nothing for us to do.
      var ids = fileIds as long[] ?? fileIds.ToArray();
      if (!ids.Any())
      {
        return true;
      }

      if (null == transaction)
      {
        throw new ArgumentNullException(nameof(transaction), "You have to be within a tansaction when calling this function.");
      }

      // first mark that folder id as procesed.
      if (!await MarkFilesProcessedAsync( ids, transaction, token).ConfigureAwait(false))
      {
        return false;
      }

      var sqlInsert = $"INSERT INTO {TableFileUpdates} (fileid, type, ticks) VALUES (@id, @type, @ticks)";
      using (var cmd = CreateDbCommand(sqlInsert, transaction))
      {
        try
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

          foreach (var fileId in ids)
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            cmd.Parameters["@id"].Value = fileId;
            cmd.Parameters["@type"].Value = (long) type;
            cmd.Parameters["@ticks"].Value = DateTime.UtcNow.Ticks;
            if (0 == await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false))
            {
              _logger.Error($"There was an issue adding file the file update: {fileId} to persister");
              return false;
            }
          }
        }
        catch (OperationCanceledException)
        {
          return false;
        }
        catch (Exception ex)
        {
          _logger.Exception(ex);
          return false;
        }
      }
      return true;
    }

    /// <inheritdoc />
    public async Task<bool> MarkFileProcessedAsync(FileInfo file, IDbTransaction transaction, CancellationToken token)
    {
      if (null == transaction)
      {
        throw new ArgumentNullException(nameof(transaction), "You have to be within a tansaction when calling this function.");
      }

      // look for the files id
      var fileId = await GetFileIdAsync(file, transaction, token, false).ConfigureAwait(false);

      // did we find it?
      if (fileId == -1)
      {
        return true;
      }

      // then we can do it by id.
      return await MarkFilesProcessedAsync(new []{fileId}, transaction, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> MarkFileProcessedAsync(long fileId, IDbTransaction transaction, CancellationToken token)
    {
      // just make the files do all the work.
      return await MarkFilesProcessedAsync(new [] { fileId }, transaction, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> MarkFilesProcessedAsync(IEnumerable<long> fileIds, IDbTransaction transaction, CancellationToken token)
    {
      if (null == transaction)
      {
        throw new ArgumentNullException(nameof(transaction), "You have to be within a tansaction when calling this function.");
      }

      var sqlDelete = $"DELETE FROM {TableFileUpdates} WHERE fileid = @id";
      using (var cmd = CreateDbCommand(sqlDelete, transaction))
      {
        try
        {
          var pId = cmd.CreateParameter();
          pId.DbType = DbType.Int64;
          pId.ParameterName = "@id";
          cmd.Parameters.Add(pId);

          foreach (var fileId in fileIds)
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // set the folder id.
            cmd.Parameters["@id"].Value = fileId;

            // this could return 0 if the row has already been processed
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
          }
        }
        catch (OperationCanceledException)
        {
          return false;
        }
        catch (Exception ex)
        {
          _logger.Exception(ex);
          return false;
        }
      }

      // return if this cancelled or not
      return true;
    }

    /// <inheritdoc />
    public async Task<List<PendingFileUpdate>> GetPendingFileUpdatesAsync(long limit, IDbTransaction transaction, CancellationToken token)
    {
      if (null == transaction)
      {
        throw new ArgumentNullException(nameof(transaction), "You have to be within a tansaction when calling this function.");
      }

      // the pending updates
      var pendingUpdates = new List<PendingFileUpdate>();
      try
      {
        // we want to get the latest updated folders.
        var sql = $"SELECT fileid, type FROM {TableFileUpdates} ORDER BY ticks DESC LIMIT {limit}";
        using (var cmd = CreateDbCommand(sql, transaction))
        {
          var reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
          while (reader.Read())
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // add this update
            pendingUpdates.Add(new PendingFileUpdate(
              (long) reader["fileid"],
              (UpdateType) (long) reader["type"]
            ));
          }
        }
      }
      catch (OperationCanceledException)
      {
        return null;
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
