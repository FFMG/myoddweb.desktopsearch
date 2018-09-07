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
using System.Data.SQLite;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;
using myoddweb.desktopsearch.service.Configs;

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
    private const string TableCounts = "Counts";
    #endregion

    #region Member variables
    /// <summary>
    /// The SQlite connection for reading
    /// </summary>
    private SQLiteConnection _connectionReadOnly;

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
    private readonly ConfigSqliteDatabase _config;
    #endregion

    /// <inheritdoc />
    public ICounts Counts { get; }

    public SqlitePersister(ILogger logger, ConfigSqliteDatabase config, int maxNumCharacters)
    {
      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      // the configuration
      _config = config ?? throw new ArgumentNullException(nameof(config));

      // the number of characters.
      _maxNumCharacters = maxNumCharacters;

      // create the counters
      Counts = new SqlitePersisterCounts( TableCounts, _logger );
    }

    /// <inheritdoc />
    public void Start( CancellationToken token )
    {
      // check that the file does exists.
      if (!File.Exists(_config.Source))
      {
        try
        {
          SQLiteConnection.CreateFile(_config.Source);
        }
        catch (Exception ex)
        {
          _logger.Exception(ex);
          throw new FileNotFoundException();
        }
      }

      // the connection string
      var connectionString = $"Data Source={_config.Source};Version=3;Pooling=True;Max Pool Size=100;";

      // the readonly.
      _connectionReadOnly = new SQLiteConnection($"{connectionString}Read Only=True;");
      _connectionReadOnly.Open();

      // create the connection spinner and pass the function to create transactions.
      _transactionSpinner = new TransactionsManager(ConnectionFactory);

      // initiialise everything
      Initialise(token).Wait(token);
    }

    private async Task Initialise(CancellationToken token)
    {
      var connectionFactory = await BeginWrite(token).ConfigureAwait(false);
      try
      {
        // update the db if need be.
        Update(connectionFactory, token).Wait(token);

        // init the counters.
        Counts.Initialise(connectionFactory, token).Wait(token);
        Commit( connectionFactory );
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        Rollback(connectionFactory);
        throw;
      }
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
        return new SqliteReadOnlyConnectionFactory( _connectionReadOnly );
      } 
      return new SqliteReadWriteConnectionFactory( _config );
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
          await connectionFactory.ExecuteWriteAsync(command, CancellationToken.None).ConfigureAwait(false);
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
