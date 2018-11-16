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
using System.Data.Common;
using System.Data.SQLite;
using System.Threading;
using myoddweb.desktopsearch.service.Configs;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal class SqliteReadWriteConnectionFactory : SqliteConnectionFactory
  {
    #region Member variables
    /// <summary>
    /// The current SQLite transaction
    /// </summary>
    private SQLiteTransaction _sqLiteTransaction;

    /// <summary>
    /// The cache size we want to use.
    /// </summary>
    private readonly long _cacheSize;

    /// <inheritdoc />
    public override bool IsReadOnly => false;
    #endregion

    public SqliteReadWriteConnectionFactory(ConfigSqliteDatabase config) :
      base( CreateConnection( config ) )
    {
      // save the config value
      _cacheSize = config?.CacheSize ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Create a connection using the configuration values.
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    protected static SQLiteConnection CreateConnection( ConfigSqliteDatabase config )
    {
      // the connection string
      var connectionString = $"Data Source={config.Source};Version=3;Pooling=True;Max Pool Size=100;";

      // the readonly.
      return new SQLiteConnection(connectionString);
    }

    /// <summary>
    /// Dispose the transaction if it is not null
    /// Set the transaction to null.
    /// </summary>
    private void Close()
    {
      // call the derived classes a chance to perform some work.
      PrepareForClose();

      // the database is closed, all we can do now is dispose of the transaction.
      _sqLiteTransaction?.Dispose();
      _sqLiteTransaction = null;

      SqLiteConnection.Close();
      SqLiteConnection.Dispose();
    }

    /// <inheritdoc />
    protected override void OnOpened()
    {
      // we now have a connection, if we want to support Write-Ahead Logging then we do it now.
      // @see https://www.sqlite.org/wal.html
      // we call a read function ... so no transactions are created... yet.
      var sqls = new List<string>
      {
        "PRAGMA journal_mode=WAL;",
        // https://wiki.mozilla.org/Performance/Avoid_SQLite_In_Your_Next_Firefox_Feature
        "PRAGMA wal_autocheckpoint = 16;",
        "PRAGMA journal_size_limit = 1536;",
        // other little tricks to speed things up...
        // https://www.sqlite.org/pragma.html#pragma_cache_size
        $"PRAGMA cache_size = {_cacheSize};",
        "PRAGMA temp_store = MEMORY;"
      };
      foreach (var sql in sqls)
      {
        using (var cmd = new SQLiteCommand(sql, SqLiteConnection))
        {
          cmd.ExecuteNonQuery();
        }
      }
    }

    /// <inheritdoc />
    protected override void OnCommit()
    {
      _sqLiteTransaction?.Commit();
      Close();
    }

    /// <inheritdoc />
    protected override void OnRollback()
    {
      _sqLiteTransaction?.Rollback();
      Close();
    }

    /// <inheritdoc />
    protected override void PrepareForRead()
    {
      //  nothing to do.
    }

    /// <inheritdoc />
    protected override void PrepareForWrite()
    {
      if (_sqLiteTransaction == null)
      {
        _sqLiteTransaction = SqLiteConnection.BeginTransaction();
      }
    }

    /// <inheritdoc />
    protected override void PrepareForClose()
    {
      //  https://www.sqlite.org/pragma.html#pragma_optimize
      using (var cmd = new SQLiteCommand("PRAGMA optimize;", SqLiteConnection))
      {
        cmd.ExecuteNonQuery();
      }
    }

    /// <inheritdoc />
    protected override DbCommand OnCreateCommand(string sql)
    {
      PrepareForWrite();
      return new SQLiteCommand(sql, SqLiteConnection, _sqLiteTransaction);
    }
  }
}
