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
    public async Task<bool> AddOrUpdateDirectoryAsync(DirectoryInfo directory, DbTransaction transaction, CancellationToken token)
    {
      return await AddOrUpdateDirectoriesAsync(new [] {directory}, transaction, token ).ConfigureAwait(false); ;
    }

    /// <inheritdoc />
    public async Task<bool> AddOrUpdateDirectoriesAsync(IReadOnlyList<DirectoryInfo> directories, DbTransaction transaction, CancellationToken token )
    {
      if (null != transaction)
      {
        // rebuild the list of directory with only those that need to be inserted.
        return await InsertDirectoriesAsync(
          await RebuildDirectoriesListAsync(directories, transaction, token).ConfigureAwait(false),
          transaction, token).ConfigureAwait(false);
      }

      transaction = BeginTransaction();
      try
      {
        if (await AddOrUpdateDirectoriesAsync(directories, transaction, token).ConfigureAwait(false))
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
    public async Task<long> RenameOrAddDirectoryAsync(DirectoryInfo directory, DirectoryInfo oldDirectory, DbTransaction transaction, CancellationToken token)
    {
      if (transaction != null)
      {
        var sqlDelete = $"UPDATE {TableFolders} SET path=@path1 WHERE path=@path2";
        using (var cmd = CreateCommand(sqlDelete))
        {
          cmd.Transaction = transaction as SQLiteTransaction;
          cmd.Parameters.Add("@path1", DbType.String);
          cmd.Parameters.Add("@path2", DbType.String);
          try
          {
            // are we cancelling?
            if (token.IsCancellationRequested)
            {
              return -1;
            }

            // try and replace path1 with path2
            cmd.Parameters["@path1"].Value = directory.FullName;
            cmd.Parameters["@path2"].Value = oldDirectory.FullName;
            if (0 == await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false))
            {
              // we could not rename it, this could be because of an error
              // or because the old path simply does not exist.
              // in that case we can try and simply add the new path.
              _logger.Error($"There was an issue renaming folder: {directory.FullName} to persister");
              if (!await AddOrUpdateDirectoryAsync(oldDirectory, transaction, token).ConfigureAwait(false))
              {
                return -1;
              }
            }

            // if we are here, we either renamed the directory or we managed 
            // to add add a new directory
            // in either cases, we can now return the id of this newly added path.
            // we won't ask the function to insert a new file as we _just_ renamed it.
            // so it has to exist...
            return await GetFolderIdAsync(directory, transaction, token, false ).ConfigureAwait(false);
          }
          catch (Exception ex)
          {
            _logger.Exception(ex);
          }
        }
        return -1;
      }

      // we were not given a transaction to work with
      // so we will create one ourslves and wrap the code around it.
      transaction = BeginTransaction();
      try
      {
        // try and rename, and if it worked then we can return the id.
        var id = await RenameOrAddDirectoryAsync(directory, oldDirectory, transaction, token).ConfigureAwait(false);
        if (id != -1 )
        {
          Commit(transaction);
          return id;
        }
      }
      catch (Exception e)
      {
        _logger.Exception(e);
      }

      // if we are here, it did not work.
      Rollback(transaction);
      return -1;
    }
 
    /// <inheritdoc />
    public async Task<bool> DeleteDirectoryAsync(DirectoryInfo directory, DbTransaction transaction, CancellationToken token)
    {
      return await DeleteDirectoriesAsync(new[] { directory }, transaction, token).ConfigureAwait( false );
    }

    /// <inheritdoc />
    public async Task<bool> DeleteDirectoriesAsync(IReadOnlyList<DirectoryInfo> directories, DbTransaction transaction, CancellationToken token)
    {
      // if we have nothing to do... we are done.
      if (!directories.Any())
      {
        return true;
      }

      if (transaction != null)
      {
        var sqlDelete = $"DELETE FROM {TableFolders} WHERE path=@path";
        using (var cmd = CreateCommand(sqlDelete))
        {
          cmd.Transaction = transaction as SQLiteTransaction;
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

              cmd.Parameters["@path"].Value = directory.FullName;
              if (0 == await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false))
              {
                _logger.Error($"There was an issue deleting folder: {directory.FullName} from persister");
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

      transaction = BeginTransaction();
      try
      {
        if (await DeleteDirectoriesAsync(directories, transaction, token).ConfigureAwait(false))
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
    /// Get the next row ID we can use.
    /// </summary>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<long> GetNextFolderIdAsync(DbTransaction transaction, CancellationToken token)
    {
      // we first look for it, and, if we find it then there is nothing to do.
      var sqlNextRowId = $"SELECT max(id) from {TableFolders};";
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

    /// <summary>
    /// Get the id of a folder or -1.
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <param name="createIfNotFound"></param>
    /// <returns></returns>
    private async Task<long> GetFolderIdAsync(DirectoryInfo directory, DbTransaction transaction, CancellationToken token, bool createIfNotFound)
    {
      // we first look for it, and, if we find it then there is nothing to do.
      var sql = $"SELECT id FROM {TableFolders} WHERE path=@path";
      using (var cmd = CreateCommand(sql))
      {
        cmd.Transaction = transaction as SQLiteTransaction;
        cmd.Parameters.Add("@path", DbType.String);

        // are we cancelling?
        if (token.IsCancellationRequested)
        {
          return -1;
        }

        cmd.Parameters["@path"].Value = directory.FullName;
        var value = await cmd.ExecuteScalarAsync(token).ConfigureAwait(false);
        if (null == value || value == DBNull.Value)
        {
          if (!createIfNotFound)
          {
            // we could not find it and we do not wish to go further.
            return -1;
          }

          // try and add the folder, if that does not work, then we cannot go further.
          if (!await AddOrUpdateDirectoryAsync(directory, transaction, token).ConfigureAwait(false))
          {
            return -1;
          }

          // try one more time to look for it .. and if we do not find it, then just return
          return await GetFolderIdAsync(directory, transaction, token, false).ConfigureAwait(false);
        }

        // get the path id.
        return (long)value;
      }
    }

    /// <summary>
    /// Given a list of directories, re-create the ones that we need to insert.
    /// </summary>
    /// <param name="directories"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> InsertDirectoriesAsync(IReadOnlyList<DirectoryInfo> directories, DbTransaction transaction, CancellationToken token)
    {
      // if we have nothing to do... we are done.
      if (!directories.Any())
      {
        return true;
      }

      // get the next id.
      var nextId = await GetNextFolderIdAsync(transaction, token).ConfigureAwait(false);

      var sqlInsert = $"INSERT INTO {TableFolders} (id, path) VALUES (@id, @path)";
      using (var cmd = CreateCommand(sqlInsert))
      {
        cmd.Transaction = transaction as SQLiteTransaction;
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
    /// Given a list of directories, re-create the ones that we need to insert.
    /// </summary>
    /// <param name="directories"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<List<DirectoryInfo>> RebuildDirectoriesListAsync(IEnumerable<DirectoryInfo> directories, DbTransaction transaction, CancellationToken token )
    {
      // The list of directories we will be adding to the list.
      var actualDirectories = new List<DirectoryInfo>();

      // we first look for it, and, if we find it then there is nothing to do.
      var sqlGetRowId = $"SELECT id FROM {TableFolders} WHERE path=@path";
      using (var cmd = CreateCommand(sqlGetRowId))
      {
        cmd.Transaction = transaction as SQLiteTransaction;
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
  }
}
