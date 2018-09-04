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
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;
using myoddweb.desktopsearch.service.Configs;
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
    protected SQLiteConnection SqLiteConnection
    {
      get
      {
        if (_connection != null)
        {
          return _connection;
        }
        var connectionString  = $"Data Source={_config.Source};Version=3;Pooling=True;Max Pool Size=100;";
        if (IsReadOnly)
        {
          connectionString += "Read Only=True;";
        }
        _connection = new SQLiteConnection( connectionString);

        // we now have a connection, if we want to support Write-Ahead Logging then we do it now.
        // @see https://www.sqlite.org/wal.html
        // we call a read function ... so no transactions are created... yet.
        ExecuteReadOneAsync(CreateCommand("PRAGMA journal_mode=WAL;"), default(CancellationToken)).Wait();
        return _connection;
      }
    }

    /// <summary>
    /// The created connection, null until created.
    /// </summary>
    private SQLiteConnection _connection;
    
    /// <summary>
    /// The sqlite database
    /// </summary>
    private readonly ConfigSqliteDatabase _config;

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
    /// <param name="config"></param>
    /// <param name="logger"></param>
    protected SqliteConnectionFactory(ConfigSqliteDatabase config, ILogger logger )
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _config = config ?? throw new ArgumentNullException(nameof(config));
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

    #region Open/Close
    /// <summary>
    /// Open the database if needed.
    /// </summary>
    private void OpenIfNeeded()
    {
      if (SqLiteConnection.State == ConnectionState.Open)
      {
        return;
      }
      SqLiteConnection.Open();
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
        OnClose();
      }
      catch (Exception e)
      {
        _logger.Exception(e);
      }

      // close the connection.
      _connection?.Close();
      _connection?.Dispose();
      _connection = null;

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
    #endregion 

    #region Fix DBCommand Cancellation issues
    /// <summary>
    /// Create a cancelled task
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    private static Task<T> CreatedTaskWithCancellation<T>()
    {
      var completion = new TaskCompletionSource<T>();
      completion.SetCanceled();
      return completion.Task;
    }

    private static Task<T> CreatedTaskWithException<T>(Exception ex)
    {
      var completion = new TaskCompletionSource<T>();
      completion.SetException(ex);
      return completion.Task;
    }

    /// <summary>
    /// Try and cancel the current command.
    /// </summary>
    /// <param name="command"></param>
    private static void CancelIgnoreFailure(IDbCommand command)
    {
      try
      {
        command.Cancel();
      }
      catch (Exception)
      {
        // ignored
      }
    }

    /// <summary>
    /// @see https://github.com/Microsoft/referencesource/blob/master/System.Data/System/Data/Common/DBCommand.cs
    /// @see https://github.com/dotnet/corefx/commit/297fcc33db4e65287455f6575684f24975688b53
    /// </summary>
    /// <param name="command"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>The return value is the number of rows affected by the command</returns>
    private static Task<int> ExecuteNonQueryAsync(IDbCommand command, CancellationToken cancellationToken)
    {
      if (cancellationToken.IsCancellationRequested)
      {
        return CreatedTaskWithCancellation<int>();
      }

      var register = new CancellationTokenRegistration();
      if (cancellationToken.CanBeCanceled)
      {
        register = cancellationToken.Register(() => CancelIgnoreFailure(command));
      }

      try
      {
        return Task.FromResult(command.ExecuteNonQuery());
      }
      catch (Exception e)
      {
        return CreatedTaskWithException<int>(e);
      }
      finally
      {
        register.Dispose();
      }
    }

    /// <summary>
    /// @see https://github.com/Microsoft/referencesource/blob/master/System.Data/System/Data/Common/DBCommand.cs
    /// @see https://github.com/dotnet/corefx/commit/297fcc33db4e65287455f6575684f24975688b53
    /// </summary>
    /// <param name="command"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private static Task<IDataReader> ExecuteReaderAsync(IDbCommand command, CancellationToken cancellationToken)
    {
      if (cancellationToken.IsCancellationRequested)
      {
        return CreatedTaskWithCancellation<IDataReader>();
      }

      var register = new CancellationTokenRegistration();
      if (cancellationToken.CanBeCanceled)
      {
        register = cancellationToken.Register(() => CancelIgnoreFailure(command));
      }

      try
      {
        return Task.FromResult(command.ExecuteReader());
      }
      catch (Exception e)
      {
        return CreatedTaskWithException<IDataReader>(e);
      }
      finally
      {
        register.Dispose();
      }
    }

    /// <summary>
    /// @see https://github.com/Microsoft/referencesource/blob/master/System.Data/System/Data/Common/DBCommand.cs
    /// @see https://github.com/dotnet/corefx/commit/297fcc33db4e65287455f6575684f24975688b53
    /// </summary>
    /// <param name="command"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected static Task<object> ExecuteScalarAsync(IDbCommand command, CancellationToken cancellationToken)
    {
      if (cancellationToken.IsCancellationRequested)
      {
        return CreatedTaskWithCancellation<object>();
      }

      var register = new CancellationTokenRegistration();
      if (cancellationToken.CanBeCanceled)
      {
        register = cancellationToken.Register(() => CancelIgnoreFailure(command));
      }

      try
      {
        return Task.FromResult(command.ExecuteScalar());
      }
      catch (Exception e)
      {
        return CreatedTaskWithException<object>(e);
      }
      finally
      {
        register.Dispose();
      }
    }
    #endregion

    /// <inheritdoc />
    public DbCommand CreateCommand(string sql)
    {
      ThrowIfNotValid();
      return OnCreateCommand(sql);
    }

    /// <inheritdoc />
    public async Task<int> ExecuteWriteAsync(IDbCommand command, CancellationToken cancellationToken)
    {
      ThrowIfNotValid();
      OpenIfNeeded();
      PepareForWrite();
      return await ExecuteNonQueryAsync(command, cancellationToken).ConfigureAwait( false );
    }

    /// <inheritdoc />
    public async Task<IDataReader> ExecuteReadAsync(IDbCommand command, CancellationToken cancellationToken)
    {
      ThrowIfNotValid();
      OpenIfNeeded();
      PepareForRead();
      return await ExecuteReaderAsync(command, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<object> ExecuteReadOneAsync(IDbCommand command, CancellationToken cancellationToken)
    {
      ThrowIfNotValid();
      OpenIfNeeded();
      PepareForRead();
      return await ExecuteScalarAsync(command, cancellationToken).ConfigureAwait(false);
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

    #region Abstract Functions
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

    /// <summary>
    /// Give derived classes a chance to get ready for a read
    /// </summary>
    protected abstract void PepareForRead();

    /// <summary>
    /// Give derived classes a chance to get ready for a write
    /// </summary>
    protected abstract void PepareForWrite();
    #endregion
  }
}
