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
  internal class TransactionSpiner
  {
    private readonly object _lock = new object();
    private readonly IDbConnection _connection;
    private IDbTransaction _transaction;
    private readonly CancellationToken _token;

    public TransactionSpiner(IDbConnection connection, CancellationToken token )
    {
      _token = token;
      _connection = connection ?? throw new ArgumentNullException( nameof(connection));
    }

    public IDbTransaction Begin()
    {
      for(;;)
      {
        // wait for the transaction to no longer be null
        // outside of the lock, (so it can be freed.
        if (null != _transaction)
        {
          SpinWait.SpinUntil(() => _transaction == null || _token.IsCancellationRequested );
        }

        if (_token.IsCancellationRequested)
        {
          return null;
        }

        // now trans and create the transaction
        lock (_lock)
        {
          // oops... we didn't get it.
          if (_transaction != null)
          {
            continue;
          }

          // we were able to get a null transaction
          // ... and we are inside the lock
          _transaction = _connection.BeginTransaction();
          return _transaction;
        }
      }
    }

    public void Rollback( IDbTransaction transaction )
    {
      lock (_lock)
      {
        if (transaction != _transaction)
        {
          throw new ArgumentException("The given transaction was not created by this class");
        }
      }

      if (null == _transaction)
      {
        return;
      }

      lock (_lock)
      {
        if (null == _transaction)
        {
          return;
        }
        _transaction.Rollback();
        _transaction = null;
      }
    }

    public void Commit( IDbTransaction transaction )
    {
      lock (_lock)
      {
        if (transaction != _transaction)
        {
          throw new ArgumentException( "The given transaction was not created by this class");
        }
      }

      if (null == _transaction)
      {
        return;
      }

      lock (_lock)
      {
        if (null == _transaction)
        {
          return;
        }

        _transaction.Commit();
        _transaction = null;
      }
    }
  }

  internal partial class Persister : IPersister
  {
    #region Table names
    private const string TableConfig = "Config";
    private const string TableFolders = "Folders";
    private const string TableFolderUpdates = "FolderUpdates";
    private const string TableFiles = "Files";
    private const string TableFileUpdates = "FileUpdates";
    #endregion

    #region Member variables

    private readonly TransactionSpiner _transaction;

    /// <summary>
    /// The sqlite connection.
    /// </summary>
    private readonly SQLiteConnection _dbConnection;

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;
    #endregion

    public Persister(ILogger logger, CancellationToken token )
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

      // try and open the database.
      _dbConnection = new SQLiteConnection($"Data Source={source};Version=3;");
      _dbConnection.Open();

      _transaction = new TransactionSpiner(_dbConnection, token );

      // update the db if need be.
      Update().Wait();
    }

    #region Transactions
    /// <inheritdoc/>
    public Task<IDbTransaction> BeginTransactionAsync()
    {
      // set the value
      try
      {
        return Task.FromResult( _transaction.Begin() );
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
      return CreateDbCommand(_dbConnection, sql, transaction );
    }

    private static SQLiteCommand CreateDbCommand(SQLiteConnection connection, string sql, IDbTransaction transaction)
    {
      if (null == connection)
      {
        throw new Exception("The database is not open!");
      }
      return new SQLiteCommand(sql, connection, transaction as SQLiteTransaction );
    }

    private async Task<bool> ExecuteNonQueryAsync(string sql, IDbTransaction transaction )
    {
      return await ExecuteNonQueryAsync(_dbConnection, sql, transaction ).ConfigureAwait(false);
    }

    private async Task<bool> ExecuteNonQueryAsync(SQLiteConnection destination, string sql, IDbTransaction transaction )
    {
      try
      {
        using (var command = CreateDbCommand(destination, sql, transaction ))
        {
          await command.ExecuteNonQueryAsync().ConfigureAwait(false);
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
  }
}
