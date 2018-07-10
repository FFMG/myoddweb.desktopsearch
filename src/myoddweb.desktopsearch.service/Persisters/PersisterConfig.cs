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
    /// <inheritdoc />
    public async Task<string> GetConfigValueAsync(string name, string defaultValue)
    {
      var sql = $"SELECT value FROM {TableConfig} WHERE name='{name}';";
      using (var command = CreateCommand(sql))
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

    /// <summary>
    /// Get a configuration value.
    /// @NB this a very simnple function, it is up to the caller to check for nulls and make
    ///     sure that the resulting objects can be properly cast to whatever values.
    /// </summary>
    /// <param name="name">The value we are looking for.</param>
    /// <param name="value">The value in case the data does not exist in the db.</param>
    /// <returns>Either the default value or the actual value.</returns>
    public async Task<bool> SetConfigValueAsync(string name, string value)
    {
      var current = await GetConfigValueAsync(name, null).ConfigureAwait(false);
      if (null == current)
      {
        return
          await
            ExecuteNonQueryAsync($"insert into {TableConfig} (name, value) values ('{name}', '{value}')")
              .ConfigureAwait(false);
      }
      return
        await
          ExecuteNonQueryAsync($"update {TableConfig} set value='{value}' where name='{name}'").ConfigureAwait(false);
    }
  }
}
