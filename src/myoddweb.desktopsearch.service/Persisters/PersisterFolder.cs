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
    public async Task<bool> AddOrUpdateFolderAsync(DirectoryInfo directory, CancellationToken token)
    {
      return await AddOrUpdateFoldersAsync(new [] {directory}, token );
    }

    /// <inheritdoc />
    public async Task<bool> AddOrUpdateFoldersAsync(IEnumerable<DirectoryInfo> directories, CancellationToken token)
    {
      var transaction = DbConnection.BeginTransaction();
      try
      {
        if (await AddOrUpdateFoldersAsync(directories, transaction, token ))
        {
          transaction.Commit();
          return true;
        }
        transaction.Rollback();
        return false;
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        try
        {
          transaction.Rollback();
        }
        catch (Exception rollbackException)
        {
          _logger.Exception(rollbackException);
        }
        return false;
      }
    }

    /// <summary>
    /// Add multiple folders within a transaction.
    /// </summary>
    /// <param name="directories">The directories we will be adding</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> AddOrUpdateFoldersAsync(IEnumerable<DirectoryInfo> directories, SQLiteTransaction transaction, CancellationToken token )
    {
      // rebuild the list of directory with only those that need to be inserted.
      return await InsertDirectoriesAsync(await RebuildDirectoriesListAsync(directories, transaction, token).ConfigureAwait(false), 
        transaction, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Given a list of directories, re-create the ones that we need to insert.
    /// </summary>
    /// <param name="directories"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> InsertDirectoriesAsync(IReadOnlyList<DirectoryInfo> directories, SQLiteTransaction transaction, CancellationToken token)
    {
      // if we have nothing to do... we are done.
      if (!directories.Any())
      {
        return true;
      }

      // get the next id.
      var nextId = await GetNextRowIdAsync(transaction, token).ConfigureAwait(false);

      var sqlInsert = $"INSERT INTO {TableFolders} (id, path) VALUES (@id, @path)";
      using (var cmd = CreateCommand(sqlInsert))
      {
        cmd.Transaction = transaction;
        cmd.Parameters.Add("@id", DbType.Int64);
        cmd.Parameters.Add("@path", DbType.String);
        foreach (var directory in directories)
        {
          try
          {
            // are we cancelling?
            if (token.IsCancellationRequested)
            {
              return false;
            }

            cmd.Parameters["@id"].Value = nextId++;
            cmd.Parameters["@path"].Value = directory.FullName;
            if (0 == await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false))
            {
              _logger.Error($"There was an issue adding folder: {directory.FullName} to persister");
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
    /// Get the next row ID we can use.
    /// </summary>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<long> GetNextRowIdAsync(SQLiteTransaction transaction, CancellationToken token)
    {
      // we first look for it, and, if we find it then there is nothing to do.
      const string sqlNextRowId = "SELECT max(id) from folders;";
      using (var cmd = CreateCommand(sqlNextRowId))
      {
        cmd.Transaction = transaction;

        // are we cancelling?
        if (token.IsCancellationRequested)
        {
          return 0;
        }

        var value = await cmd.ExecuteScalarAsync(token).ConfigureAwait(false);
        if (null == value || value == DBNull.Value )
        {
          return 0;
        }

        // we could not find this path ... so just add it.
        return ((long)value)+1;
      }
    }

    /// <summary>
    /// Given a list of directories, re-create the ones that we need to insert.
    /// </summary>
    /// <param name="directories"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<List<DirectoryInfo>> RebuildDirectoriesListAsync(IEnumerable<DirectoryInfo> directories, SQLiteTransaction transaction, CancellationToken token )
    {
      // The list of directories we will be adding to the list.
      var actualDirectories = new List<DirectoryInfo>();

      // we first look for it, and, if we find it then there is nothing to do.
      var sqlGetRowId = $"SELECT id FROM {TableFolders} WHERE path=@path";
      using (var cmd = CreateCommand(sqlGetRowId))
      {
        cmd.Transaction = transaction;
        cmd.Parameters.Add("@path", DbType.String);
        foreach (var directory in directories)
        {
          // are we cancelling?
          if (token.IsCancellationRequested)
          {
            return new List<DirectoryInfo>();
          }

          // only valid paths are added.
          if (!directory.Exists)
          {
            continue;
          }
          cmd.Parameters["@path"].Value = directory.FullName;
          if (null != await cmd.ExecuteScalarAsync(token).ConfigureAwait(false))
          {
            continue;
          }

          // we could not find this path ... so just add it.
          actualDirectories.Add(directory);
        }
      }
      return actualDirectories;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteFolderAsync(DirectoryInfo directory, CancellationToken token)
    {
      return await DeleteFoldersAsync(new[] { directory }, token);
    }

    /// <inheritdoc />
    public Task<bool> DeleteFoldersAsync(IEnumerable<DirectoryInfo> directories, CancellationToken token)
    {
      throw new NotImplementedException();
    }
  }
}
