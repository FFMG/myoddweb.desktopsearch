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
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal partial class Persister 
  {
    /// <inheritdoc />
    public async Task<bool> AddOrUpdateFileAsync(FileInfo file, DbTransaction transaction, CancellationToken token)
    {
      return await AddOrUpdateFilesAsync(new [] { file }, transaction, token ).ConfigureAwait(false); ;
    }

    /// <inheritdoc />
    public async Task<bool> AddOrUpdateFilesAsync(IReadOnlyList<FileInfo> files, DbTransaction transaction, CancellationToken token)
    {
      if (null != transaction)
      {
        // rebuild the list of directory with only those that need to be inserted.
        return await InsertFilesAsync(
          await RebuildFilesListAsync(files, transaction, token).ConfigureAwait(false),
          transaction, token).ConfigureAwait(false);
      }

      transaction = BeginTransaction();
      try
      {
        if (await AddOrUpdateFilesAsync(files, transaction, token).ConfigureAwait(false))
        {
          Commit( transaction );
          return true;
        }
        Rollback(transaction);
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        Rollback( transaction );
      }
      return false;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteFileAsync(FileInfo file, DbTransaction transaction, CancellationToken token)
    {
      return await DeleteFilesAsync(new[] { file }, transaction, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteFilesAsync(IReadOnlyList<FileInfo> files, DbTransaction transaction, CancellationToken token)
    {
      if (null != transaction)
      {
        // rebuild the list of directory with only those that need to be inserted.
        return await InsertFilesAsync(
          await RebuildFilesListAsync(files, transaction, token).ConfigureAwait(false),
          transaction, token).ConfigureAwait(false);
      }

      transaction = BeginTransaction();
      try
      {
        if (await AddOrUpdateFilesAsync(files, transaction, token).ConfigureAwait(false))
        {
          Commit(transaction);
          return true;
        }
        Rollback(transaction);
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        Rollback(transaction);
      }
      return false;
    }

    /// <summary>
    /// Given a list of files, re-create the ones that we need to insert.
    /// </summary>
    /// <param name="files"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> InsertFilesAsync(IReadOnlyList<FileInfo> files, DbTransaction transaction, CancellationToken token)
    {
      // if we have nothing to do... we are done.
      if (!files.Any())
      {
        return true;
      }

      // get the next id.
      var nextId = await GetNextFileIdAsync(transaction, token).ConfigureAwait(false);

      var sqlInsert = $"INSERT INTO {TableFiles} (id, folderid, name) VALUES (@id, @folderid, @name)";
      using (var cmd = CreateCommand(sqlInsert))
      {
        cmd.Transaction = transaction as SQLiteTransaction;
        cmd.Parameters.Add("@id", DbType.Int64);
        cmd.Parameters.Add("@folderid", DbType.Int64);
        cmd.Parameters.Add("@name", DbType.String);
        foreach (var file in files)
        {
          try
          {
            // are we cancelling?
            if (token.IsCancellationRequested)
            {
              return false;
            }

            // Get the folder for this file and insert it, if need be.
            var folderId = await GetFolderIdAsync(file.Directory, transaction, token, true ).ConfigureAwait(false);
            if (-1 == folderId)
            {
              _logger.Error( $"I was unable to insert {file.FullName} as I could not locate and insert the directory!");
              return false;
            }

            cmd.Parameters["@id"].Value = nextId++;
            cmd.Parameters["@folderid"].Value = folderId;
            cmd.Parameters["@name"].Value = file.Name;
            if (0 == await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false))
            {
              _logger.Error($"There was an issue adding file: {file.FullName} to persister");
            }
          }
          catch (Exception ex)
          {
            _logger.Exception(ex);
          }
        }
      }
      return true;
    }

    /// <summary>
    /// Given a list of directories, re-create the ones that we need to insert.
    /// </summary>
    /// <param name="files"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<List<FileInfo>> RebuildFilesListAsync(IEnumerable<FileInfo> files, DbTransaction transaction, CancellationToken token)
    {
      // The list of directories we will be adding to the list.
      var actualFiles = new List<FileInfo>();

      // we first look for it, and, if we find it then there is nothing to do.
      var sqlGetRowId = $"SELECT id FROM {TableFiles} WHERE folderid=@folderid AND name=@name";
      using (var cmd = CreateCommand(sqlGetRowId))
      {
        cmd.Transaction = transaction as SQLiteTransaction;
        cmd.Parameters.Add("@folderid", DbType.Int64);
        cmd.Parameters.Add("@name", DbType.String);
        foreach (var file in files)
        {
          // are we cancelling?
          if (token.IsCancellationRequested)
          {
            return new List<FileInfo>();
          }

          // only valid paths are added.
          if (!file.Exists)
          {
            continue;
          }

          // Get the folder for this file and insert it, if need be.
          var folderId = await GetFolderIdAsync(file.Directory, transaction, token, true).ConfigureAwait(false);
          if (-1 == folderId)
          {
            _logger.Error($"I was unable to insert {file.FullName} as I could not locate and insert the directory!");
            continue;
          }

          cmd.Parameters["@folderid"].Value = folderId;
          cmd.Parameters["@name"].Value = file.Name;
          if (null != await cmd.ExecuteScalarAsync(token).ConfigureAwait(false))
          {
            continue;
          }

          // we could not find this file
          // so we will just add it to our list.
          actualFiles.Add(file);
        }
      }
      return actualFiles;
    }

    /// <summary>
    /// Get the next row ID we can use.
    /// </summary>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<long> GetNextFileIdAsync(DbTransaction transaction, CancellationToken token)
    {
      // we first look for it, and, if we find it then there is nothing to do.
      var sqlNextRowId = $"SELECT max(id) from {TableFiles};";
      using (var cmd = CreateCommand(sqlNextRowId))
      {
        cmd.Transaction = transaction as SQLiteTransaction;

        // are we cancelling?
        if (token.IsCancellationRequested)
        {
          return 0;
        }

        var value = await cmd.ExecuteScalarAsync(token).ConfigureAwait(false);
        if (null == value || value == DBNull.Value)
        {
          return 0;
        }

        // we could not find this path ... so just add it.
        return ((long)value) + 1;
      }
    }
  }
}
