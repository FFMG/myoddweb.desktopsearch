﻿//This file is part of Myoddweb.DesktopSearch.
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
using System.Data.SQLite;
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

    /// <summary>
    /// The checkpoint size
    /// https://www.sqlite.org/wal.html#automatic_checkpoint
    /// https://www.sqlite.org/wal.html#checkpointing
    /// </summary>
    private readonly long _autoCheckpoint;

    /// <inheritdoc />
    public override bool IsReadOnly => false;

    /// <summary>
    /// If false, we will _not_ create a transaction
    /// </summary>
    private readonly bool _createTransaction;
    #endregion

    public SqliteReadWriteConnectionFactory(bool createTransaction, ConfigSqliteDatabase config) :
      base( CreateConnection( config ) )
    {
      // save the config value
      if (null == config)
      {
        throw new ArgumentNullException(nameof(config));
      }
      _cacheSize = config.CacheSize;
      _autoCheckpoint = config.AutoCheckpoint;
      _createTransaction = createTransaction;
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
        "PRAGMA auto_vacuum=NONE;",
        "PRAGMA journal_mode=WAL;",
        "PRAGMA threads=true;",
        // https://wiki.mozilla.org/Performance/Avoid_SQLite_In_Your_Next_Firefox_Feature
        // https://www.sqlite.org/wal.html#automatic_checkpoint
        $"PRAGMA wal_autocheckpoint = {_autoCheckpoint};",
        "PRAGMA synchronous = NORMAL;",
        "PRAGMA journal_size_limit = -1;",
        // other little tricks to speed things up...
        // https://www.sqlite.org/pragma.html#pragma_cache_size
        $"PRAGMA cache_size = {_cacheSize};",
        "PRAGMA temp_store = MEMORY;",
        "PRAGMA automatic_index = false;",
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
        if (_createTransaction)
        {
          _sqLiteTransaction = SqLiteConnection.BeginTransaction();
        }
      }
    }

    /// <inheritdoc />
    protected override void PrepareForClose()
    {
      //  https://www.sqlite.org/pragma.html#pragma_optimize
      if (SqLiteConnection?.State == ConnectionState.Open)
      {
        using (var cmd = new SQLiteCommand("PRAGMA optimize;", SqLiteConnection))
        {
          cmd.ExecuteNonQuery();
        }
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
