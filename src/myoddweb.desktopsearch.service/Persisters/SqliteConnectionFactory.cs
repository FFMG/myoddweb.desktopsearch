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
    /// Derived classes need to pass an active connection.
    /// </summary>
    /// <param name="connection"></param>
    protected SqliteConnectionFactory(SQLiteConnection connection )
    {
      SqLiteConnection = connection ?? throw new ArgumentNullException(nameof(connection));
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
    private void OpenIfNeeded( CancellationToken token )
    {
      if (SqLiteConnection.State == ConnectionState.Open)
      {
        return;
      }
      // open it
      SqLiteConnection.Open();

      // it is now open.
      OnOpened();
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
      OpenIfNeeded(cancellationToken);
      PepareForWrite();
      return await ExecuteNonQueryAsync(command, cancellationToken).ConfigureAwait( false );
    }

    /// <inheritdoc />
    public async Task<IDataReader> ExecuteReadAsync(IDbCommand command, CancellationToken cancellationToken)
    {
      ThrowIfNotValid();
      OpenIfNeeded(cancellationToken);
      PepareForRead();
      return await ExecuteReaderAsync(command, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<object> ExecuteReadOneAsync(IDbCommand command, CancellationToken cancellationToken)
    {
      ThrowIfNotValid();
      OpenIfNeeded( cancellationToken );
      PepareForRead();
      return await ExecuteScalarAsync(command, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual void Commit()
    {
      ThrowIfNotValid();
      OnCommit();
    }

    /// <inheritdoc />
    public virtual void Rollback()
    {
      ThrowIfNotValid();
      OnRollback();
    }

    #region Abstract Functions
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
    /// We just opened the database.
    /// </summary>
    protected abstract void OnOpened();

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
