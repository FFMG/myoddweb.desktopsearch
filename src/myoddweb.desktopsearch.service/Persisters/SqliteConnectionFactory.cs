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
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;
using Exception = System.Exception;

namespace myoddweb.desktopsearch.service.Persisters
{
  /// <summary>
  /// Connection factory to manage the SQLite connections.
  /// </summary>
  internal abstract class SqliteConnectionFactory : IConnectionFactory
  {
    /// <summary>
    /// The current SQLite connection.
    /// </summary>
    protected SQLiteConnection SqLiteConnection;

    /// <inheritdoc />
    public abstract bool IsReadOnly { get; }

    /// <inheritdoc />
    public IDbConnection Connection => SqLiteConnection;

    /// <summary>
    /// Our logger.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Derived classes need to pass an active connection.
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="logger"></param>
    protected SqliteConnectionFactory(SQLiteConnection connection, ILogger logger )
    {
      SqLiteConnection = connection ?? throw new ArgumentNullException( nameof(connection));
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Make sure that all the values are correct.
    /// </summary>
    private void ThrowIfNotValid()
    {
      if (SqLiteConnection == null)
      {
        throw new Exception("The database is not open!");
      }
    }

    /// <summary>
    /// Close the connection and get rid of the transaction
    /// </summary>
    private void CloseAll()
    {
      try
      {
        // the database is about to close allow the derived
        // classes to perform ppre closing
        OnClosed();
      }
      catch (Exception e)
      {
        _logger.Exception(e);
      }

      // close the connection.
      SqLiteConnection.Close();
      SqLiteConnection.Dispose();
      SqLiteConnection = null;

      try
      {
        // the database is closed allow the derived
        // classes to perform post closing
        OnClosed();
      }
      catch (Exception e)
      {
        _logger.Exception(e);
      }
    }

    /// <inheritdoc />
    public DbCommand CreateCommand(string sql)
    {
      ThrowIfNotValid();
      if (SqLiteConnection.State != ConnectionState.Open)
      {
        SqLiteConnection.Open();
      }
      return OnCreateCommand(sql);
    }

    /// <inheritdoc />
    public virtual void Commit()
    {
      ThrowIfNotValid();
      try
      {
        OnCommit();
      }
      finally 
      {
        CloseAll();
      }
    }

    /// <inheritdoc />
    public virtual void Rollback()
    {
      ThrowIfNotValid();
      try
      {
        OnRollback();
      }
      finally
      {
        CloseAll();
      }
    }

    /// <summary>
    /// The database is now closed, the derived classes can do some final cleanup.
    /// </summary>
    protected abstract void OnClosed();

    /// <summary>
    /// The database is about to close.
    /// </summary>
    protected abstract void OnClose();

    /// <summary>
    /// Create a command
    /// </summary>
    /// <param name="sql"></param>
    /// <returns></returns>
    protected abstract DbCommand OnCreateCommand(string sql);

    /// <summary>
    /// Derived classes can now roll back the transaction
    /// We will close the connection afterward.
    /// </summary>
    protected abstract void OnRollback();

    /// <summary>
    /// Derived classes can now commit the transaction
    /// We will close the connection afterward.
    /// </summary>
    protected abstract void OnCommit();
  }
}
