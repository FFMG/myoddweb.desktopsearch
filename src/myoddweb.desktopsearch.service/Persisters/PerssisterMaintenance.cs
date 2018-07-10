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
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal partial class Persister
  {
    /// <summary>
    /// Create the database to the latest version
    /// </summary>
    protected async Task<bool> CreateDatabase()
    {
      // create the config table.
      if (!await CreateConfigAsync().ConfigureAwait(false))
      {
        return false;
      }

      // the folders table.
      if (!await CreateFoldersAsync().ConfigureAwait(false))
      {
        return false;
      }

      return true;
    }

    /// <summary>
    /// Create the folders table
    /// </summary>
    /// <returns></returns>
    private async Task<bool> CreateFoldersAsync()
    {
      if (!await
        ExecuteNonQueryAsync($"CREATE TABLE {TableFolders} (id integer PRIMARY KEY, path varchar(260))")
          .ConfigureAwait(false))
      {
        return false;
      }

      if ( 
        !await
          ExecuteNonQueryAsync($"CREATE INDEX index_{TableFolders}_path ON {TableFolders}(path); ").ConfigureAwait(false))
      {
        return false;
      }

      return true;
    }

    /// <summary>
    /// Create the configuration table
    /// </summary>
    /// <returns></returns>
    private async Task<bool> CreateConfigAsync()
    {
      if (!await
        ExecuteNonQueryAsync($"CREATE TABLE {TableConfig} (name varchar(20), value varchar(255))")
          .ConfigureAwait(false))
      {
        return false;
      }

      if (!await
        ExecuteNonQueryAsync($"CREATE INDEX index_{TableConfig}_name ON {TableConfig}(name); ").ConfigureAwait(false))
      {
        return false;
      }

      return true;
    }

    /// <summary>
    /// Check if the table exists.
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    protected bool TableExists(string name)
    {
      var sql = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{name}';";
      using (var command = CreateCommand(sql))
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
      if (!TableExists(TableConfig))
      {
        await CreateDatabase().ConfigureAwait(false);
      }
    }
  }
}