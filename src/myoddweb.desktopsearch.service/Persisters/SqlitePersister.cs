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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal partial class SqlitePersister : IPersister
  {
    #region Table names
    private const string TableConfig = "Config";
    private const string TableFolders = "Folders";
    private const string TableFolderUpdates = "FolderUpdates";
    private const string TableFiles = "Files";
    private const string TableFileUpdates = "FileUpdates";
    private const string TableWords = "Words";
    private const string TableFilesWords = "FilesWords";
    #endregion

    #region Member variables
    /// <summary>
    /// The transactions manager.
    /// </summary>
    private readonly TransactionSpiner _transaction;

    /// <summary>
    /// The current connection
    /// </summary>
    private SQLiteConnection _connection;

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The connection string to use
    /// </summary>
    private readonly string _connectionString;
    #endregion

    public SqlitePersister(ILogger logger, CancellationToken token)
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      // the file we are looking for.
      const string source = "database.db";

      // check that the file does exists.
      if (!File.Exists(source))
      {
        try
        {
          SQLiteConnection.CreateFile(source);
        }
        catch (Exception ex)
        {
          _logger.Exception(ex);
          throw new FileNotFoundException();
        }
      }

      _connectionString = $"Data Source={source};Version=3;Pooling=True;Max Pool Size=100;";

      _transaction = new TransactionSpiner(CreateConnection, FreeResources );

      // update the db if need be.
      Update(token).Wait();
    }

    #region TransactionSpiner functions
    private void FreeResources()
    {
      if (_connection == null )
      {
        return;
      }
      _connection.Close();
      _connection.Dispose();
      _connection = null;
    }

    private IDbConnection CreateConnection()
    {
      // try and open the database.
      if (_connection != null)
      {
        return _connection;
      }

      _connection = new SQLiteConnection(_connectionString);
      _connection.Open();
      return _connection;
    }
    #endregion

    #region Transactions
    /// <inheritdoc/>
    public async Task<IDbTransaction> Begin(CancellationToken token)
    {
      // set the value
      try
      {
        return await _transaction.Begin(token).ConfigureAwait(false);
      }
      catch (OperationCanceledException e)
      {
        // is it my token?
        if (e.CancellationToken != token)
        {
          _logger.Exception(e);
        }
        throw;
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        throw;
      }
    }

    /// <inheritdoc/>
    public bool Rollback(IDbTransaction transaction)
    {
      try
      {
        if (null == transaction)
        {
          throw new ArgumentNullException(nameof(transaction));
        }
        _transaction.Rollback(transaction);
        return true;
      }
      catch (Exception rollbackException)
      {
        _logger.Exception(rollbackException);
        return false;
      }
    }

    /// <inheritdoc/>
    public bool Commit(IDbTransaction transaction)
    {
      try
      {
        if (null == transaction)
        {
          throw new ArgumentNullException( nameof(transaction));
        }
        _transaction.Commit(transaction);
        return true;
      }
      catch (Exception commitException)
      {
        _logger.Exception(commitException);
        return false;
      }
    }
    #endregion

    #region Commands
    /// <inheritdoc/>
    public DbCommand CreateDbCommand(string sql, IDbTransaction transaction)
    {
      if (_connection == null)
      {
        throw new Exception("The database is not open!");
      }
      return new SQLiteCommand(sql, _connection, transaction as SQLiteTransaction);
    }

    /// <summary>
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="transaction"></param>
    /// <returns></returns>
    private async Task<bool> ExecuteNonQueryAsync(string sql, IDbTransaction transaction )
    {
      try
      {
        using (var command = CreateDbCommand(sql, transaction ))
        {
          await ExecuteNonQueryAsync(command, CancellationToken.None).ConfigureAwait(false);
        }
        return true;
      }
      catch (Exception ex)
      {
        // log this
        _logger.Exception( ex);

        // did not work.
        return false;
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
    /// <returns></returns>
    protected static Task<object> ExecuteScalarAsync(DbCommand command, CancellationToken cancellationToken)
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
      catch (Exception e )
      {
        return CreatedTaskWithException<object>(e);
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
    protected static Task<DbDataReader> ExecuteReaderAsync(DbCommand command, CancellationToken cancellationToken)
    {
      if (cancellationToken.IsCancellationRequested)
      {
        return CreatedTaskWithCancellation<DbDataReader>();
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
        return CreatedTaskWithException<DbDataReader>(e);
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
    /// <returns>The return value is the number of rows affected by the command</returns>
    protected static Task<int> ExecuteNonQueryAsync(DbCommand command, CancellationToken cancellationToken)
    {
      if (cancellationToken.IsCancellationRequested)
      {
        return CreatedTaskWithCancellation<int>();
      }

      var register = new CancellationTokenRegistration();
      if (cancellationToken.CanBeCanceled)
      {
        register = cancellationToken.Register( () => CancelIgnoreFailure(command) );
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
    #endregion
  }
}
