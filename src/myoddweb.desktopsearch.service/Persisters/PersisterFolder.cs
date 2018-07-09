using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal partial class Persister 
  {
    /// <inheritdoc />
    public async Task<bool> AddOrUpdateFolderAsync(DirectoryInfo directory )
    {
      return await AddOrUpdateFoldersAsync(new [] {directory});
    }

    /// <inheritdoc />
    public async Task<bool> AddOrUpdateFoldersAsync(IEnumerable<DirectoryInfo> directories)
    {
      var transaction = DbConnection.BeginTransaction();
      try
      {
        if (await AddOrUpdateFoldersAsync(directories, transaction))
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
    /// <returns></returns>
    private async Task<bool> AddOrUpdateFoldersAsync(IEnumerable<DirectoryInfo> directories, SQLiteTransaction transaction )
    {
      // The list of directories we will be adding to the list.
      var actualDirectories = new List<DirectoryInfo>();

      // we first look for it, and, if we find it then there is nothing to do.
      var sqlGetRowId = $"SELECT rowid FROM {TableFolders} WHERE path=@path";
      using (var cmd = CreateCommand(sqlGetRowId))
      {
        cmd.Transaction = transaction;
        cmd.Parameters.Add("@path", DbType.String);
        foreach (var directory in directories)
        {
          // only valid paths are added.
          if (!directory.Exists)
          {
            continue;
          }
          cmd.Parameters["@path"].Value = directory.FullName;
          if (null != await cmd.ExecuteScalarAsync().ConfigureAwait(false))
          {
            continue;
          }

          // we could not find this path ... so just add it.
          actualDirectories.Add(directory);
        }
      }

      // if we have nothing to do... we are done.
      if (!actualDirectories.Any())
      {
        return true;
      }
      
      var sqlInsert = $"INSERT INTO {TableFolders} (path) VALUES (@path)";
      using (var cmd = CreateCommand(sqlInsert))
      {
        cmd.Transaction = transaction;
        cmd.Parameters.Add("@path", DbType.String);
        foreach (var directory in actualDirectories)
        {
          try
          {
            cmd.Parameters["@path"].Value = directory.FullName;
            if (0 == await cmd.ExecuteNonQueryAsync().ConfigureAwait(false))
            {
              _logger.Error( $"There was an issue adding folder: {directory.FullName} to persister");
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
    
    /// <inheritdoc />
    public Task<bool> DeleteFolderAsync(int folderId)
    {
      throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<bool> DeleteFolderAsync(string path)
    {
      throw new NotImplementedException();
    }
  }
}
