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
using System.Data.SQLite;
using System.Threading;
using myoddweb.desktopsearch.service.Configs;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal class SqliteReadWriteConnectionFactory : SqliteConnectionFactory
  {
    private SQLiteTransaction _sqLiteTransaction;
    
    public override bool IsReadOnly => false;

    public SqliteReadWriteConnectionFactory(ConfigSqliteDatabase config) :
      base( CreateConnection( config ) )
    {
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
      ExecuteReadOneAsync(CreateCommand("PRAGMA journal_mode=WAL;"), default(CancellationToken)).Wait();
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
    protected override void PepareForRead()
    {
      //  nothing to do.
    }

    /// <inheritdoc />
    protected override void PepareForWrite()
    {
      if (_sqLiteTransaction == null)
      {
        _sqLiteTransaction = SqLiteConnection.BeginTransaction();
      }
    }

    /// <inheritdoc />
    protected override DbCommand OnCreateCommand(string sql)
    {
      return new SQLiteCommand(sql, SqLiteConnection, _sqLiteTransaction);
    }
  }
}
