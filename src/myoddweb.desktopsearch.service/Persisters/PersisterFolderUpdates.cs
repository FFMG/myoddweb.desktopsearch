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
    public async Task<bool> TouchDirectoryAsync(DirectoryInfo directory, FolderUpdateType type, DbTransaction transaction, CancellationToken token)
    {
      var folderId = await GetDirectoryIdAsync(directory, transaction, token, false).ConfigureAwait(false);
      if (-1 == folderId)
      {
        return !token.IsCancellationRequested;
      }

      // we can then use the transaction id to touch the folder.
      return await TouchDirectoryAsync(folderId, type, transaction, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> TouchDirectoryAsync(long folderId, FolderUpdateType type, DbTransaction transaction, CancellationToken token)
    {
      // if it is not a valid id then there is nothing for us to do.
      if (folderId < 0)
      {
        return true;
      }

      // first mark that folder id as procesed.
      if (!await MarkDirectoryProcessedAsync(folderId, transaction, token))
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
          cmd.Parameters["@type"].Value = (long)type;
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
    public async Task<bool> MarkDirectoryProcessedAsync(DirectoryInfo directory, DbTransaction transaction, CancellationToken token)
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
    public async Task<bool> MarkDirectoryProcessedAsync(long folderId, DbTransaction transaction, CancellationToken token)
    {
      // if it is not a valid id then there is nothing for us to do.
      if (folderId < 0)
      {
        return true;
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
          // set the folder id.
          cmd.Parameters["@id"].Value = folderId;

          // this could return 0 if the row has already been processed
          await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
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
  }
}
