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
      return await AddOrUpdateFilesAsync(new [] { file }, transaction, token ).ConfigureAwait(false);
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
    public async Task<long> RenameOrAddFileAsync(FileInfo file, FileInfo oldFile, DbTransaction transaction, CancellationToken token)
    {
      if (transaction != null)
      {
        // this is the new folder, we might as well create it if it does not exit.
        var folderId = await GetFolderIdAsync(file.Directory, transaction, token, true);
        if (-1 == folderId)
        {
          // we cannot create the parent folder id
          // so there is nothing more to do really.
          return -1;
        }

        // get the old folder.
        var oldFolderId = await GetFolderIdAsync(oldFile.Directory, transaction, token, true);
        if (-1 == oldFolderId)
        {
          // this cannot be a renaming, as the parent dirctory does not exist.
          // so we will just try and add it.
          // by calling the 'GetFile' function and creating it if needed we will insert the file.
          return await GetFileIdAsync( file, transaction, token, true );
        }

        // so we have an old folder id and a new folder id
        var sql = $"UPDATE {TableFiles} SET name=@name1, folderid=@folderid1 WHERE name=@name2 and folderid=@folderid2";
        using (var cmd = CreateDbCommand(sql, transaction))
        {
          var pName1 = cmd.CreateParameter();
          pName1.DbType = DbType.String;
          pName1.ParameterName = "@name1";
          cmd.Parameters.Add( pName1);

          var pName2 = cmd.CreateParameter();
          pName2.DbType = DbType.String;
          pName2.ParameterName = "@name2";
          cmd.Parameters.Add(pName2);

          var pFolderId1 = cmd.CreateParameter();
          pFolderId1.DbType = DbType.Int64;
          pFolderId1.ParameterName = "@folderid1";
          cmd.Parameters.Add(pFolderId1);

          var pFolderId2 = cmd.CreateParameter();
          pFolderId2.DbType = DbType.Int64;
          pFolderId2.ParameterName = "@folderid2";
          cmd.Parameters.Add(pFolderId2);
          try
          {
            // are we cancelling?
            if (token.IsCancellationRequested)
            {
              return -1;
            }

            // try and replace path1 with path2
            cmd.Parameters["@name1"].Value = file.Name;
            cmd.Parameters["@name2"].Value = oldFile.Name;
            cmd.Parameters["@folderid1"].Value = folderId;
            cmd.Parameters["@folderid2"].Value = oldFolderId;
            if (0 == await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false))
            {
              // we could not rename it, this could be because of an error
              // or because the old path simply does not exist.
              // in that case we can try and simply add the new path.
              if (!await AddOrUpdateFileAsync(file, transaction, token).ConfigureAwait(false))
              {
                _logger.Error($"There was an issue renaming file: {file.FullName} to persister");
                return -1;
              }
            }

            // if we are here, we either renamed the file
            // or we managed to add add it
            // in either cases, we can now return the id of this newly added file.
            // we won't ask the function to insert a new file as we _just_ renamed it.
            // so it has to exist...
            return await GetFileIdAsync(file, transaction, token, false).ConfigureAwait(false);
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
        var id = await RenameOrAddFileAsync(file, oldFile, transaction, token).ConfigureAwait(false);
        if (id != -1)
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
    public async Task<bool> DeleteFileAsync(FileInfo file, DbTransaction transaction, CancellationToken token)
    {
      return await DeleteFilesAsync(new[] { file }, transaction, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteFilesAsync(IReadOnlyList<FileInfo> files, DbTransaction transaction, CancellationToken token)
    {
      // if we have nothing to do... we are done.
      if (!files.Any())
      {
        return true;
      }

      if (null != transaction)
      {
        var sqlDelete = $"DELETE FROM {TableFiles} WHERE folderid=@folderid and name=@name";
        using (var cmd = CreateDbCommand(sqlDelete, transaction))
        {
          var pFolderId = cmd.CreateParameter();
          pFolderId.DbType = DbType.Int64;
          pFolderId.ParameterName = "@folderid";
          cmd.Parameters.Add(pFolderId);

          var pName = cmd.CreateParameter();
          pName.DbType = DbType.String;
          pName.ParameterName = "@name";
          cmd.Parameters.Add(pName);
          foreach (var file in files)
          {
            try
            {
              // are we cancelling?
              if (token.IsCancellationRequested)
              {
                return false;
              }

              // get the folder id, no need to create it.
              var folderid = await GetFolderIdAsync(file.Directory, transaction, token, false).ConfigureAwait( false );
              if (-1 == folderid)
              {
                _logger.Warning($"Could not delete file: {file.FullName}, could not locate the parent folder?");
                continue;
              }

              cmd.Parameters["@folderid"].Value = folderid;
              cmd.Parameters["@name"].Value = file.Name;
              if (0 == await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false))
              {
                _logger.Information($"Could not delete file: {file.FullName}, does it still exist?");
              }
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

      transaction = BeginTransaction();
      try
      {
        if (await DeleteFilesAsync(files, transaction, token).ConfigureAwait(false))
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
      using (var cmd = CreateDbCommand(sqlInsert, transaction))
      {
        var pId = cmd.CreateParameter();
        pId.DbType = DbType.Int64;
        pId.ParameterName = "@id";
        cmd.Parameters.Add(pId);

        var pFolderId = cmd.CreateParameter();
        pFolderId.DbType = DbType.Int64;
        pFolderId.ParameterName = "@folderid";
        cmd.Parameters.Add(pFolderId);

        var pName = cmd.CreateParameter();
        pName.DbType = DbType.String;
        pName.ParameterName = "@name";
        cmd.Parameters.Add(pName);
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
      using (var cmd = CreateDbCommand(sqlGetRowId, transaction))
      {
        var pFolderId = cmd.CreateParameter();
        pFolderId.DbType = DbType.Int64;
        pFolderId.ParameterName = "@folderid";
        cmd.Parameters.Add(pFolderId);

        var pName = cmd.CreateParameter();
        pName.DbType = DbType.String;
        pName.ParameterName = "@name";
        cmd.Parameters.Add(pName);
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
    private async Task<long> GetNextFileIdAsync(DbTransaction transaction, CancellationToken token )
    {
      // we first look for it, and, if we find it then there is nothing to do.
      var sqlNextRowId = $"SELECT max(id) from {TableFiles};";
      using (var cmd = CreateDbCommand(sqlNextRowId, transaction))
      {
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
    /// Get the id of a file or -1.
    /// </summary>
    /// <param name="file"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <param name="createIfNotFound"></param>
    /// <returns></returns>
    private async Task<long> GetFileIdAsync(FileInfo file, DbTransaction transaction, CancellationToken token, bool createIfNotFound)
    {
      // get the folder id
      var folderid = await GetFolderIdAsync(file.Directory, transaction, token, false ).ConfigureAwait(false);
      if (-1 == folderid)
      {
        if (!createIfNotFound)
        {
          return -1;
        }

        // add the file, if we get an error now, there is nothing we can do about it.
        if (!await AddOrUpdateFileAsync(file, transaction, token).ConfigureAwait(false))
        {
          return -1;
        }

        // try and look for it
        folderid = await GetFileIdAsync(file, transaction, token, false).ConfigureAwait(false);
        if (-1 == folderid)
        {
          // we cannot get the folder that we just add!
          _logger.Error( $"I could not find the folder that I _just_ added! (path: {file.FullName}).");
          return -1;
        }
      }

      // we first look for it, and, if we find it then there is nothing to do.
      var sql = $"SELECT id FROM {TableFiles} WHERE folderid=@folderid and name=@name";
      using (var cmd = CreateDbCommand(sql, transaction ))
      {
        var pFolderId = cmd.CreateParameter();
        pFolderId.DbType = DbType.Int64;
        pFolderId.ParameterName = "@folderid";
        cmd.Parameters.Add(pFolderId);

        var pName = cmd.CreateParameter();
        pName.DbType = DbType.String;
        pName.ParameterName = "@name";
        cmd.Parameters.Add(pName);

        // are we cancelling?
        if (token.IsCancellationRequested)
        {
          return -1;
        }

        cmd.Parameters["@folderid"].Value = folderid;
        cmd.Parameters["@name"].Value = file.Name;
        var value = await cmd.ExecuteScalarAsync(token).ConfigureAwait(false);
        if (null == value || value == DBNull.Value)
        {
          if (!createIfNotFound)
          {
            // we could not find it and we do not wish to go further.
            return -1;
          }

          // try and add the folder, if that does not work, then we cannot go further.
          if (!await AddOrUpdateFileAsync(file, transaction, token).ConfigureAwait(false))
          {
            return -1;
          }

          // try one more time to look for it .. and if we do not find it, then just return
          return await GetFileIdAsync(file, transaction, token, false).ConfigureAwait(false);
        }

        // get the path id.
        return (long)value;
      }
    }

  }
}
