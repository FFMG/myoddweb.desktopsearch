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
using System.Threading;
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal partial class SqlitePersister
  {
    /// <inheritdoc />
    public async Task<T> GetConfigValueAsync<T>(string name, T defaultValue, IDbTransaction transaction, CancellationToken token)
    {
      var sql = $"SELECT value FROM {TableConfig} WHERE name='{name}';";
      using (var command = CreateDbCommand(sql, transaction ))
      {
        var value = await ExecuteScalarAsync(command, token).ConfigureAwait(false);
        if (null == value || value == DBNull.Value)
        {
          return defaultValue;
        }

        // the value is saved as a string so we need to convert it to whatever.
        // otherwise we will just throw...
        return ConfigStringToType<T>((string) value);
      }
    }

    /// <inheritdoc />
    public async Task<bool> SetConfigValueAsync<T>(string name, T value, IDbTransaction transaction, CancellationToken token)
    {
      // we will need the string
      var stringValue = ConfigTypeToString(value);
      var current = await GetConfigValueAsync<object>(name, null, transaction, token ).ConfigureAwait(false);
      var sql = null == current ? 
        $"INSERT INTO {TableConfig} (name, value) values (@name, @value)" : 
        $"UPDATE {TableConfig} SET value=@value WHERE name=@name";

      // do it now.
      using (var cmd = CreateDbCommand(sql, transaction))
      {
        var pName = cmd.CreateParameter();
        pName.DbType = DbType.String;
        pName.ParameterName = "@name";
        cmd.Parameters.Add(pName);

        var pValue = cmd.CreateParameter();
        pValue.DbType = DbType.String;
        pValue.ParameterName = "@value";
        cmd.Parameters.Add(pValue);

        pName.Value = name;
        pValue.Value = stringValue;

        return 0 != await ExecuteNonQueryAsync(cmd, token).ConfigureAwait(false);
      }
    }

    private static string ConfigTypeToString<T>(T value)
    {
      // object
      if (typeof(T) == typeof(object))
      {
        return (string)Convert.ChangeType(value, typeof(string));
      }
      // Int64
      if (typeof(T) == typeof(long))
      {
        return $"{Convert.ChangeType(value, typeof(long))}";
      }
      if (typeof(T) == typeof(ulong))
      {
        return $"{Convert.ChangeType(value, typeof(ulong))}";
      }
      // Int32
      if (typeof(T) == typeof(int))
      {
        return $"{Convert.ChangeType(value, typeof(int))}";
      }
      if (typeof(T) == typeof(uint))
      {
        return $"{Convert.ChangeType(value, typeof(uint))}";
      }
      if (typeof(T) == typeof(DateTime))
      {
        var dt = (DateTime)Convert.ChangeType(value, typeof(DateTime));
        return $"{dt.Ticks}";
      }
      throw new InvalidCastException($"Cannot convert value to {typeof(T)}");
    }

    /// <summary>
    /// Convert a given string to a type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value"></param>
    /// <returns></returns>
    private static T ConfigStringToType<T>(string value)
    {
      // object
      if (typeof(T) == typeof(object))
      {
        return (T)Convert.ChangeType(value, typeof(T));
      }
      // string
      if (typeof(T) == typeof(string))
      {
        return (T)Convert.ChangeType(value, typeof(T));
      }
      // Int64
      if (typeof(T) == typeof(long))
      {
        return (T)Convert.ChangeType(Convert.ToInt64(value), typeof(T));
      }
      if (typeof(T) == typeof(ulong))
      {
        return (T)Convert.ChangeType(Convert.ToUInt64(value), typeof(T));
      }
      // Int32
      if (typeof(T) == typeof(int))
      {
        return (T)Convert.ChangeType(Convert.ToInt32(value), typeof(T));
      }
      if (typeof(T) == typeof(uint))
      {
        return (T)Convert.ChangeType(Convert.ToUInt32(value), typeof(T));
      }
      if (typeof(T) == typeof(DateTime))
      {
        return (T)Convert.ChangeType( new DateTime(Convert.ToInt64(value)), typeof(T));
      }
      throw new InvalidCastException($"Cannot convert value to {typeof(T)}");
    }
  }
}
