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
using myoddweb.desktopsearch.helper.IO;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal partial class SqlitePersister 
  {
    /// <inheritdoc />
    public async Task<bool> AddOrUpdateDirectoryAsync(DirectoryInfo directory, IDbTransaction transaction, CancellationToken token)
    {
      return await AddOrUpdateDirectoriesAsync(new [] {directory}, transaction, token ).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> AddOrUpdateDirectoriesAsync(IReadOnlyList<DirectoryInfo> directories, IDbTransaction transaction, CancellationToken token )
    {
      if (null == transaction)
      {
        throw new ArgumentNullException(nameof(transaction), "You have to be within a tansaction when calling this function.");
      }

      // rebuild the list of directory with only those that need to be inserted.
      return await InsertDirectoriesAsync(
        await RebuildDirectoriesListAsync(directories, transaction, token).ConfigureAwait(false),
        transaction, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<long> RenameOrAddDirectoryAsync(DirectoryInfo directory, DirectoryInfo oldDirectory, IDbTransaction transaction, CancellationToken token)
    {
      if (null == transaction)
      {
        throw new ArgumentNullException(nameof(transaction), "You have to be within a tansaction when calling this function.");
      }

      try
      {
        var sql = $"UPDATE {TableFolders} SET path=@path1 WHERE path=@path2";
        using (var cmd = CreateDbCommand(sql, transaction))
        {
          var pPath1 = cmd.CreateParameter();
          pPath1.DbType = DbType.String;
          pPath1.ParameterName = "@path1";
          cmd.Parameters.Add(pPath1);

          var pPath2 = cmd.CreateParameter();
          pPath2.DbType = DbType.String;
          pPath2.ParameterName = "@path2";
          cmd.Parameters.Add(pPath2);

          // try and replace path1 with path2
          cmd.Parameters["@path1"].Value = directory.FullName.ToLowerInvariant();
          cmd.Parameters["@path2"].Value = oldDirectory.FullName.ToLowerInvariant();
          if (0 == await ExecuteNonQueryAsync(cmd, token).ConfigureAwait(false))
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
          var folderId = await GetDirectoryIdAsync(directory, transaction, token, false).ConfigureAwait(false);

          // touch that folder as changed
          await TouchDirectoryAsync(folderId, UpdateType.Changed, transaction, token).ConfigureAwait(false);

          // get out if needed.
          token.ThrowIfCancellationRequested();

          // we are done
          return folderId;
        }
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Update directory");
        throw;
      }
      catch (Exception ex)
      {
        _logger.Exception(ex);
      }
      return -1;
    }
 
    /// <inheritdoc />
    public async Task<bool> DeleteDirectoryAsync(DirectoryInfo directory, IDbTransaction transaction, CancellationToken token)
    {
      return await DeleteDirectoriesAsync(new[] { directory }, transaction, token).ConfigureAwait( false );
    }

    /// <inheritdoc />
    public async Task<bool> DeleteDirectoriesAsync(IReadOnlyList<DirectoryInfo> directories, IDbTransaction transaction, CancellationToken token)
    {
      // if we have nothing to do... we are done.
      if (!directories.Any())
      {
        return true;
      }

      if (null == transaction)
      {
        throw new ArgumentNullException(nameof(transaction), "You have to be within a tansaction when calling this function.");
      }

      var sqlDelete = $"DELETE FROM {TableFolders} WHERE path=@path";
      using (var cmd = CreateDbCommand(sqlDelete, transaction))
      {
        var pPath = cmd.CreateParameter();
        pPath.DbType = DbType.String;
        pPath.ParameterName = "@path";
        cmd.Parameters.Add(pPath);

        foreach (var directory in directories)
        {
          try
          {
            // try and delete files given directory info.
            await DeleteFilesAsync(directory, transaction, token).ConfigureAwait(false);

            // touch that folder as deleted
            await TouchDirectoryAsync(directory, UpdateType.Deleted, transaction, token).ConfigureAwait(false);

            // then do the actual delete.
            cmd.Parameters["@path"].Value = directory.FullName.ToLowerInvariant();
            if (0 == await ExecuteNonQueryAsync(cmd, token).ConfigureAwait(false))
            {
              _logger.Warning($"Could not delete folder: {directory.FullName}, does it still exist?");
            }

            // get out if needed.
            token.ThrowIfCancellationRequested();
          }
          catch (OperationCanceledException)
          {
            _logger.Warning("Received cancellation request - Delete directories");
            throw;
          }
          catch (Exception ex)
          {
            _logger.Exception(ex);
          }
        }
      }

      // we are done.
      return true;
    }

    /// <inheritdoc />
    public async Task<bool> DirectoryExistsAsync(DirectoryInfo directory, IDbTransaction transaction, CancellationToken token)
    {
      return await GetDirectoryIdAsync(directory, transaction, token, false).ConfigureAwait(false) != -1;
    }

    /// <inheritdoc />
    public async Task<DirectoryInfo> GetDirectoryAsync(long directoryId, IDbTransaction transaction, CancellationToken token)
    {
      if (null == transaction)
      {
        throw new ArgumentNullException(nameof(transaction), "You have to be within a tansaction when calling this function.");
      }

      try
      {
        // we want to get the latest updated folders.
        var sql = $"SELECT path FROM {TableFolders} WHERE id = @id";
        using (var cmd = CreateDbCommand(sql, transaction))
        {
          var pId = cmd.CreateParameter();
          pId.DbType = DbType.Int64;
          pId.ParameterName = "@id";
          cmd.Parameters.Add(pId);

          // set the folder id.
          cmd.Parameters["@id"].Value = directoryId;

          // get the path
          var path = await ExecuteScalarAsync(cmd, token).ConfigureAwait(false);
          if (null == path || path == DBNull.Value)
          {
            return null;
          }

          // return the valid paths.
          return helper.File.DirectoryInfo( (string)path, _logger );
        }
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        return null;
      }
    }

    /// <summary>
    /// Get the next row ID we can use.
    /// </summary>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<long> GetNextDirectoryIdAsync(IDbTransaction transaction, CancellationToken token)
    {
      // we first look for it, and, if we find it then there is nothing to do.
      var sqlNextRowId = $"SELECT max(id) from {TableFolders};";
      using (var cmd = CreateDbCommand(sqlNextRowId, transaction))
      {
        var value = await ExecuteScalarAsync(cmd, token).ConfigureAwait(false);
        if (null == value || value == DBNull.Value)
        {
          return 0;
        }

        // we could not find this path ... so just add it.
        return ((long) value) + 1;
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
    private async Task<long> GetDirectoryIdAsync(DirectoryInfo directory, IDbTransaction transaction, CancellationToken token, bool createIfNotFound)
    {
      if (null == directory)
      {
        throw new ArgumentNullException(nameof(directory), "The given directory is null");
      }

      // we first look for it, and, if we find it then there is nothing to do.
      var sql = $"SELECT id FROM {TableFolders} WHERE path=@path";
      using (var cmd = CreateDbCommand(sql, transaction))
      {
        var pPath = cmd.CreateParameter();
        pPath.DbType = DbType.String;
        pPath.ParameterName = "@path";
        cmd.Parameters.Add(pPath);

        cmd.Parameters["@path"].Value = directory.FullName.ToLowerInvariant();
        var value = await ExecuteScalarAsync(cmd, token).ConfigureAwait(false);
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
          return await GetDirectoryIdAsync(directory, transaction, token, false).ConfigureAwait(false);
        }

        // get the path id.
        return (long) value;
      }
    }

    /// <summary>
    /// Given a list of directories, re-create the ones that we need to insert.
    /// </summary>
    /// <param name="directories"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> InsertDirectoriesAsync(IReadOnlyList<DirectoryInfo> directories, IDbTransaction transaction, CancellationToken token)
    {
      // if we have nothing to do... we are done.
      if (!directories.Any())
      {
        return true;
      }

      // get the next id.
      var nextId = await GetNextDirectoryIdAsync(transaction, token).ConfigureAwait(false);

      var sqlInsert = $"INSERT INTO {TableFolders} (id, path) VALUES (@id, @path)";
      using (var cmd = CreateDbCommand(sqlInsert, transaction))
      {
        var pId = cmd.CreateParameter();
        pId.DbType = DbType.Int64;
        pId.ParameterName = "@id";
        cmd.Parameters.Add(pId);

        var pPath = cmd.CreateParameter();
        pPath.DbType = DbType.String;
        pPath.ParameterName = "@path";
        cmd.Parameters.Add(pPath);
        foreach (var directory in directories)
        {
          try
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            cmd.Parameters["@id"].Value = nextId;
            cmd.Parameters["@path"].Value = directory.FullName.ToLowerInvariant();
            if (0 == await ExecuteNonQueryAsync(cmd, token).ConfigureAwait(false))
            {
              _logger.Error($"There was an issue adding folder: {directory.FullName} to persister");
              continue;
            }

            // touch that folder as created
            await TouchDirectoryAsync(nextId, UpdateType.Created, transaction, token).ConfigureAwait(false);

            // we can now move on to the next folder id.
            ++nextId;
          }
          catch (OperationCanceledException)
          {
            _logger.Warning("Received cancellation request - Insert directories");
            throw;
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
    private async Task<List<DirectoryInfo>> RebuildDirectoriesListAsync(IEnumerable<DirectoryInfo> directories, IDbTransaction transaction, CancellationToken token )
    {
      try
      {
        // make that list unique
        var uniqueList = directories.Distinct(new DirectoryInfoComparer());

        // The list of directories we will be adding to the list.
        var actualDirectories = new List<DirectoryInfo>();

        // we first look for it, and, if we find it then there is nothing to do.
        var sqlGetRowId = $"SELECT id FROM {TableFolders} WHERE path=@path";
        using (var cmd = CreateDbCommand(sqlGetRowId, transaction))
        {
          var pPath = cmd.CreateParameter();
          pPath.DbType = DbType.String;
          pPath.ParameterName = "@path";
          cmd.Parameters.Add(pPath);
          foreach (var directory in uniqueList)
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // only valid paths are added.
            if (!directory.Exists)
            {
              continue;
            }

            cmd.Parameters["@path"].Value = directory.FullName.ToLowerInvariant();
            if (null != await ExecuteScalarAsync(cmd, token).ConfigureAwait(false))
            {
              continue;
            }

            // we could not find this path ... so just add it.
            actualDirectories.Add(directory);
          }
        }
        return actualDirectories;
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Rebuild directories list");
        throw;
      }
    }
  }
}
