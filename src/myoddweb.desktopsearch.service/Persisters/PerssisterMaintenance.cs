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