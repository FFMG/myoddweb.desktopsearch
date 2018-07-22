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
using System.Data.Common;
using System.Data.SQLite;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
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
    /// <summary>
    /// The current transaction
    /// </summary>
    private DbTransaction _transaction;

    /// <summary>
    /// Our lock
    /// </summary>
    private readonly object _lock = new object();
    
    /// <summary>
    /// The sqlite connection.
    /// </summary>
    private readonly SQLiteConnection _dbConnection;

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;
    #endregion

    public Persister(ILogger logger)
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

      // update the db if need be.
      Update().Wait();
    }

    #region Transactions
    /// <inheritdoc/>
    public async Task<DbTransaction> BeginTransactionAsync()
    {
      var lockWasTaken = false;
      var temp = _lock;
      try
      {
        while (true)
        {
          Monitor.TryEnter(temp, 5, ref lockWasTaken);
          {
            // did we get the lock?
            if (!lockWasTaken)
            {
              await Task.Yield();
              continue;
            }

            // we have the lock, can we get the transaction?
            if (_transaction != null)
            {
              // we do not have the transaction
              // release the lock
              // and continue waiting.
              lockWasTaken = false;
              Monitor.Exit(temp);
              await Task.Yield();
              continue;
            }

            // set the value
            _transaction = _dbConnection.BeginTransaction();

            // release the lock
            lockWasTaken = false;
            Monitor.Exit(temp);

            // all good
            return _transaction;
          }
        }
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        return null;
      }
      finally
      {
        if (lockWasTaken)
        {
          Monitor.Exit(temp);
        }
      }
    }

    /// <inheritdoc/>
    public bool Rollback()
    {
      try
      {
        if (null == _transaction)
        {
          throw new ArgumentNullException(nameof(_transaction));
        }
        _transaction.Rollback();
        return true;
      }
      catch (Exception rollbackException)
      {
        _logger.Exception(rollbackException);
        return false;
      }
      finally
      {
        _transaction = null;
      }
    }

    /// <inheritdoc/>
    public bool Commit()
    {
      try
      {
        if (null == _transaction)
        {
          throw new ArgumentNullException( nameof(_transaction));
        }
        _transaction.Commit();
        return true;
      }
      catch (Exception commitException)
      {
        _logger.Exception(commitException);
        return false;
      }
      finally
      {
        _transaction = null;
      }
    }
    #endregion

    #region Commands
    /// <inheritdoc/>
    public DbCommand CreateDbCommand(string sql, DbTransaction transaction )
    {
      return CreateDbCommand(_dbConnection, sql, transaction );
    }

    private static SQLiteCommand CreateDbCommand(SQLiteConnection connection, string sql, DbTransaction transaction)
    {
      if (null == connection)
      {
        throw new Exception("The database is not open!");
      }
      return new SQLiteCommand(sql, connection, transaction as SQLiteTransaction );
    }

    private async Task<bool> ExecuteNonQueryAsync(string sql, DbTransaction transaction )
    {
      return await ExecuteNonQueryAsync(_dbConnection, sql, transaction ).ConfigureAwait(false);
    }

    private async Task<bool> ExecuteNonQueryAsync(SQLiteConnection destination, string sql, DbTransaction transaction )
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
