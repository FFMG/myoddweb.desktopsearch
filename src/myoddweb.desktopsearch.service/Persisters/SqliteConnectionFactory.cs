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
using System.Data.SQLite;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal abstract class SqliteConnectionFactory : IConnectionFactory
  {
    protected SQLiteConnection _sqLiteConnection;

    public abstract bool IsReadOnly { get; }
    
    public IDbConnection Connection => _sqLiteConnection;

    protected SqliteConnectionFactory(SQLiteConnection connection )
    {
      _sqLiteConnection = connection ?? throw new ArgumentNullException( nameof(connection));
    }

    /// <summary>
    /// Make sure that all the values are correct.
    /// </summary>
    protected virtual void ThrowIfNotValid()
    {
      if (_sqLiteConnection == null)
      {
        throw new Exception("The database is not open!");
      }
    }

    /// <summary>
    /// Close the connection and get rid of the transaction
    /// </summary>
    private void CloseAll()
    {
      Connection.Close();
      Connection.Dispose();

      _sqLiteConnection = null;

      OnClose();
    }

    /// <inheritdoc />
    public DbCommand CreateCommand(string sql)
    {
      ThrowIfNotValid();
      return OnCreateCommand(sql);
    }

    /// <inheritdoc />
    public virtual void Commit()
    {
      ThrowIfNotValid();
      CloseAll();
    }

    /// <inheritdoc />
    public virtual void Rollback()
    {
      ThrowIfNotValid();
      CloseAll();
    }

    protected abstract void OnClose();

    protected abstract DbCommand OnCreateCommand(string sql);

    protected abstract void OnRollback();

    protected abstract void OnCommit();
  }
}
