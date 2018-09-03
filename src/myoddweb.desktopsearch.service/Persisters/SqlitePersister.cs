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
    private const string TableParts = "Parts";
    private const string TableWordsParts = "WordsParts";
    #endregion

    #region Member variables
    /// <summary>
    /// The maximum number of characters per words...
    /// Characters after that are ignored.
    /// </summary>
    private readonly int _maxNumCharacters;

    /// <summary>
    /// The transactions manager.
    /// </summary>
    private TransactionsManager _transactionSpinner;

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The database source.
    /// </summary>
    private readonly string _source;
    #endregion

    public SqlitePersister(ILogger logger, string source, int maxNumCharacters)
    {
      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      // the source
      _source = source ?? throw new ArgumentNullException(nameof(source));

      // the number of characters.
      _maxNumCharacters = maxNumCharacters;
    }

    /// <inheritdoc />
    public void Start( CancellationToken token )
    {
      // check that the file does exists.
      if (!File.Exists(_source))
      {
        try
        {
          SQLiteConnection.CreateFile(_source);
        }
        catch (Exception ex)
        {
          _logger.Exception(ex);
          throw new FileNotFoundException();
        }
      }

      // create the connection spinner and pass the function to create transactions.
      _transactionSpinner = new TransactionsManager(ConnectionFactory);

      // update the db if need be.
      Update(token).Wait( token );
    }

    #region TransactionSpiner functions
    /// <summary>
    /// Create the connection factory
    /// </summary>
    /// <param name="isReadOnly"></param>
    /// <returns></returns>
    private IConnectionFactory ConnectionFactory( bool isReadOnly )
    {
      if (isReadOnly)
      {
        return new SqliteReadOnlyConnectionFactory(_source, _logger);
      } 
      return new SqliteReadWriteConnectionFactory( _source, _logger);
    }
    #endregion

    #region IPersister functions
    /// <inheritdoc/>
    public async Task<IConnectionFactory> BeginRead(CancellationToken token)
    {
      // set the value
      try
      {
        if (null == _transactionSpinner)
        {
          throw new InvalidOperationException("You cannot start using the database as it has not started yet.");
        }
        return await _transactionSpinner.BeginRead(token).ConfigureAwait(false);
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
    public async Task<IConnectionFactory> BeginWrite(CancellationToken token)
    {
      // set the value
      try
      {
        if (null == _transactionSpinner)
        {
          throw new InvalidOperationException("You cannot start using the database as it has not started yet.");
        }
        return await _transactionSpinner.BeginWrite(token).ConfigureAwait(false);
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
    public bool Rollback(IConnectionFactory connectionFactory)
    {
      try
      {
        if (null == connectionFactory)
        {
          throw new ArgumentNullException(nameof(connectionFactory));
        }
        if (null == _transactionSpinner)
        {
          throw new InvalidOperationException("You cannot start using the database as it has not started yet.");
        }
        _transactionSpinner.Rollback(connectionFactory);
        return true;
      }
      catch (Exception rollbackException)
      {
        _logger.Exception(rollbackException);
        return false;
      }
    }

    /// <inheritdoc/>
    public bool Commit(IConnectionFactory connectionFactory)
    {
      try
      {
        if (null == connectionFactory)
        {
          throw new ArgumentNullException( nameof(connectionFactory));
        }
        if (null == _transactionSpinner)
        {
          throw new InvalidOperationException("You cannot start using the database as it has not started yet.");
        }
        _transactionSpinner.Commit(connectionFactory);
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
    /// <summary>
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="connectionFactory"></param>
    /// <returns></returns>
    private async Task<bool> ExecuteNonQueryAsync(string sql, IConnectionFactory connectionFactory )
    {
      try
      {
        using (var command = connectionFactory.CreateCommand(sql))
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
