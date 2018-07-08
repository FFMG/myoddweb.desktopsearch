using System.Threading.Tasks;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal partial class Persister
  {
    /// <summary>
    /// Create the database to the latest version
    /// </summary>
    protected async Task CreateDatabase()
    {
      await CreateConfigAsync().ConfigureAwait( false );
    }

    private async Task CreateConfigAsync()
    {
      // first we create the tables.
      await
        ExecuteNonQueryAsync($"CREATE TABLE {TableConfig} (name varchar(20), value varchar(255))")
          .ConfigureAwait(false);
      await
        ExecuteNonQueryAsync($"CREATE INDEX index_{TableConfig}_name ON {TableConfig}(name); ").ConfigureAwait(false);
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