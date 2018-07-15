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

using System.Data.Common;
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal partial class Persister
  {
    /// <inheritdoc />
    public async Task<string> GetConfigValueAsync(string name, string defaultValue, DbTransaction transaction )
    {
      var sql = $"SELECT value FROM {TableConfig} WHERE name='{name}';";
      using (var command = CreateDbCommand(sql, transaction ))
      {
        var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        try
        {
          while (reader.Read())
          {
            return (string)reader["value"];
          }
        }
        finally
        {
          reader.Close();
        }
      }
      return defaultValue;
    }

    /// <inheritdoc />
    public async Task<bool> SetConfigValueAsync(string name, string value, DbTransaction transaction)
    {
      var current = await GetConfigValueAsync(name, null, transaction).ConfigureAwait(false);
      if (null == current)
      {
        return
          await
            ExecuteNonQueryAsync($"insert into {TableConfig} (name, value) values ('{name}', '{value}')", transaction)
              .ConfigureAwait(false);
      }
      return
        await
          ExecuteNonQueryAsync($"update {TableConfig} set value='{value}' where name='{name}'", transaction).ConfigureAwait(false);
    }
  }
}
