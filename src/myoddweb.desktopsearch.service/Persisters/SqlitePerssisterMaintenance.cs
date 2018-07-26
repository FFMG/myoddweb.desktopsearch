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
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal partial class SqlitePersister
  {
    /// <summary>
    /// Create the database to the latest version
    /// </summary>
    protected async Task<bool> CreateDatabase(IDbTransaction transaction)
    {
      // create the config table.
      if (!await CreateConfigAsync(transaction).ConfigureAwait(false))
      {
        return false;
      }

      // the files tables
      if (!await CreateFilesAsync(transaction).ConfigureAwait(false))
      {
        return false;
      }

      // the folders table.
      if (!await CreateFoldersAsync(transaction).ConfigureAwait(false))
      {
        return false;
      }

      // the folders update table.
      if (!await CreateFoldersUpdateAsync(transaction).ConfigureAwait(false))
      {
        return false;
      }

      if (!await CreateFilesUpdateAsync(transaction).ConfigureAwait(false))
      {
        return false;
      }
      return true;
    }

    private async Task<bool> CreateFilesAsync(IDbTransaction transaction)
    {
      if (!await
        ExecuteNonQueryAsync($"CREATE TABLE {TableFiles} (id integer PRIMARY KEY, folderid integer, name varchar(260))", transaction)
          .ConfigureAwait(false))
      {
        return false;
      }

      if (
        !await
          ExecuteNonQueryAsync($"CREATE INDEX index_{TableFiles}_folderid_name ON {TableFiles}(folderid, name);", transaction).ConfigureAwait(false))
      {
        return false;
      }

      if (
        !await
          ExecuteNonQueryAsync($"CREATE INDEX index_{TableFiles}_folderid ON {TableFiles}(folderid);", transaction).ConfigureAwait(false))
      {
        return false;
      }
      return true;
    }

    /// <summary>
    /// Create the updates table.
    /// </summary>
    /// <param name="transaction"></param>
    /// <returns></returns>
    private async Task<bool> CreateFoldersUpdateAsync(IDbTransaction transaction)
    {
      if (!await
        ExecuteNonQueryAsync($"CREATE TABLE {TableFolderUpdates} (folderid integer, type integer, ticks integer)", transaction)
          .ConfigureAwait(false))
      {
        return false;
      }

      // index to get the last 'x' updated folders.
      if (
        !await
          ExecuteNonQueryAsync($"CREATE INDEX index_{TableFolderUpdates}_ticks ON {TableFolderUpdates}(ticks); ", transaction).ConfigureAwait(false))
      {
        return false;
      }

      // the folderid index so we can add/remove folders once processed.
      if (
        !await
          ExecuteNonQueryAsync($"CREATE INDEX index_{TableFolderUpdates}_folderid ON {TableFolderUpdates}(folderid); ", transaction).ConfigureAwait(false))
      {
        return false;
      }
      return true;
    }

    /// <summary>
    /// Create the updates table.
    /// </summary>
    /// <param name="transaction"></param>
    /// <returns></returns>
    private async Task<bool> CreateFilesUpdateAsync(IDbTransaction transaction)
    {
      if (!await
        ExecuteNonQueryAsync($"CREATE TABLE {TableFileUpdates} (fileid integer, type integer, ticks integer)", transaction)
          .ConfigureAwait(false))
      {
        return false;
      }

      // index to get the last 'x' updated files.
      if (
        !await
          ExecuteNonQueryAsync($"CREATE INDEX index_{TableFileUpdates}_ticks ON {TableFileUpdates}(ticks); ", transaction).ConfigureAwait(false))
      {
        return false;
      }

      // the files index so we can add/remove files once processed.
      if (
        !await
          ExecuteNonQueryAsync($"CREATE INDEX index_{TableFileUpdates}_fileid ON {TableFileUpdates}(fileid); ", transaction).ConfigureAwait(false))
      {
        return false;
      }
      return true;
    }

    /// <summary>
    /// Create the folders table
    /// </summary>
    /// <returns></returns>
    private async Task<bool> CreateFoldersAsync(IDbTransaction transaction)
    {
      if (!await
        ExecuteNonQueryAsync($"CREATE TABLE {TableFolders} (id integer PRIMARY KEY, path varchar(260))", transaction)
          .ConfigureAwait(false))
      {
        return false;
      }

      if ( 
        !await
          ExecuteNonQueryAsync($"CREATE INDEX index_{TableFolders}_path ON {TableFolders}(path); ", transaction).ConfigureAwait(false))
      {
        return false;
      }

      return true;
    }

    /// <summary>
    /// Create the configuration table
    /// </summary>
    /// <returns></returns>
    private async Task<bool> CreateConfigAsync(IDbTransaction transaction)
    {
      if (!await
        ExecuteNonQueryAsync($"CREATE TABLE {TableConfig} (name varchar(20), value varchar(255))", transaction)
          .ConfigureAwait(false))
      {
        return false;
      }

      if (!await
        ExecuteNonQueryAsync($"CREATE INDEX index_{TableConfig}_name ON {TableConfig}(name); ", transaction).ConfigureAwait(false))
      {
        return false;
      }

      return true;
    }

    /// <summary>
    /// Check if the table exists.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="transaction"></param>
    /// <returns></returns>
    protected bool TableExists(string name, IDbTransaction transaction)
    {
      var sql = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{name}';";
      using (var command = CreateDbCommand(sql, transaction))
      {
        var reader = command.ExecuteReader();
        try
        {
          while (reader.Read())
          {
            return true;
          }
        }
        finally
        {
          reader.Close();
        }
      }
      return false;
    }

    protected async Task Update()
    {
      // if the config table does not exis, then we have to asume it is brand new.
      var transaction = await BeginTransactionAsync().ConfigureAwait(false);
      try
      {
        if (!TableExists(TableConfig, transaction ))
        {
          if (!await CreateDatabase(transaction).ConfigureAwait(false))
          {
            Rollback(transaction);
          }
          else
          {
            Commit(transaction);
          }
        }
        else
        {
          Commit(transaction);
        }
      }
      catch (Exception e)
      {
        Rollback(transaction);
        _logger.Exception(e);
      }
    }
  }
}