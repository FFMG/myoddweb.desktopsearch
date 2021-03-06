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
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Configs;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Persisters;
using myoddweb.desktopsearch.service.Configs;
using IConfig = myoddweb.desktopsearch.interfaces.Persisters.IConfig;
using ILogger = myoddweb.desktopsearch.interfaces.Logging.ILogger;
using IParts = myoddweb.desktopsearch.interfaces.Persisters.IParts;
using IWords = myoddweb.desktopsearch.interfaces.Persisters.IWords;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal partial class SqlitePersister : IPersister
  {
    #region Private Member variables
    /// <summary>
    /// The SQlite connection for reading
    /// </summary>
    private SQLiteConnection _connectionReadOnly;

    /// <summary>
    /// The transactions manager.
    /// </summary>
    private TransactionsManager _transactionSpinner;

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The performance configuration
    /// </summary>
    private readonly IPerformance _performance;

    /// <summary>
    /// The database source.
    /// </summary>
    private readonly ConfigSqliteDatabase _config;
    #endregion

    #region Public properties
    /// <inheritdoc />
    public IQuery Query { get; }

    /// <inheritdoc />
    public IConfig Config { get; }

    /// <inheritdoc />
    public ICounts Counts { get; }

    /// <inheritdoc />
    public IWords Words { get; }

    /// <inheritdoc />
    public IWordsParts WordsParts { get; }

    /// <inheritdoc />
    public IParts Parts { get; }

    /// <inheritdoc />
    public IFilesWords FilesWords { get; }
    
    /// <inheritdoc />
    public IFolders Folders { get; }
    #endregion

    public SqlitePersister(IPerformance performance, IList<IFileParser> parsers, ILogger logger, 
      ConfigSqliteDatabase config, 
      int maxNumCharactersPerWords, 
      int maxNumCharactersPerParts
      )
    {
      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      // performance
      _performance = performance ?? throw new ArgumentNullException(nameof(performance));

      // the configuration
      _config = config ?? throw new ArgumentNullException(nameof(config));

      // create the configuration table.
      Config = new SqlitePersisterConfig();

      // create the counters
      Counts = new SqlitePersisterCounts(logger);

      // word parts
      WordsParts = new SqlitePersisterWordsParts(logger);

      // create the words
      Words = new SqlitePersisterWords( performance, WordsParts, maxNumCharactersPerWords, logger);

      // file words.
      FilesWords = new SqlitePersisterFilesWords( Words, logger );

      // create the files / Folders.
      Folders = new SqlitePersisterFolders( Counts, parsers, logger );

      // the parts
      Parts = new SqlitePersisterParts( maxNumCharactersPerParts );

      // the query
      Query = new SqlitePersisterQuery(maxNumCharactersPerParts, logger);
    }

    /// <inheritdoc />
    public void Start(CancellationToken token)
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
      _transactionSpinner = new TransactionsManager( _performance, _logger);

      // initiialise everything
      Initialise(token).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public void Stop()
    {
      // create the connection spinner and pass the function to create transactions.
      _transactionSpinner?.Dispose();
    }

    private async Task Initialise(CancellationToken token)
    {
      var connectionFactory = await BeginWrite(token).ConfigureAwait(false);
      try
      {
        // update the db if need be.
        await Update(connectionFactory, token).ConfigureAwait(false);

        // init the counters.
        await Counts.Initialise( token).ConfigureAwait(false);
        Commit( connectionFactory );
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        Rollback(connectionFactory);
        throw;
      }
    }

    #region Private
    /// <summary>
    /// Prepare the transactions.
    /// </summary>
    /// <param name="factory"></param>
    private void PrepareTransaction(IConnectionFactory factory)
    {
      Counts.Prepare(this, factory );
      Words.Prepare(this, factory);
      WordsParts.Prepare( this, factory );
      FilesWords.Prepare( this, factory );
      Folders.Prepare( this, factory );
      Query.Prepare(this, factory);
    }

    /// <summary>
    /// Complete the transactions.
    /// </summary>
    /// <param name="factory"></param>
    /// <param name="success"></param>
    private void CompleteTransaction(IConnectionFactory factory, bool success )
    {
      Counts.Complete(factory, success);
      Words.Complete(factory, success);
      WordsParts.Complete(factory, success);
      FilesWords.Complete(factory, success);
      Folders.Complete(factory, success);
      Query.Complete( factory, success );
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

        var factory = await _transactionSpinner
          .BeginRead(() => new SqliteReadOnlyConnectionFactory(_connectionReadOnly), token).ConfigureAwait(false);
        PrepareTransaction(factory);
        return factory;
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
    public Task<IConnectionFactory> BeginWrite(CancellationToken token)
    {
      return BeginWrite(true, Timeout.Infinite, token );
    }

    /// <inheritdoc/>
    public Task<IConnectionFactory> BeginWrite(int timeoutMs, CancellationToken token)
    {
      return BeginWrite(true, timeoutMs, token );
    }

    private async Task<IConnectionFactory> BeginWrite(bool createTransaction, int timeoutMs, CancellationToken token)
    {
      // set the value
      try

      {
        if (null == _transactionSpinner)
        {
          throw new InvalidOperationException("You cannot start using the database as it has not started yet.");
        }
        var factory = await _transactionSpinner.BeginWrite( () => new SqliteReadWriteConnectionFactory(createTransaction, _config), timeoutMs, token).ConfigureAwait(false);
        PrepareTransaction( factory );
        return factory;
      }
      catch (TimeoutException)
      {
        throw;
      }
      catch (OperationCanceledException e)
      {
        _logger.Warning("Received cancellation request - Getting count value.");
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
        _transactionSpinner.Rollback(connectionFactory, () => CompleteTransaction( connectionFactory, false) );
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
        _transactionSpinner.Commit(connectionFactory, () => CompleteTransaction(connectionFactory, true));
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
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> ExecuteNonQueryAsync(string sql, IConnectionFactory connectionFactory, CancellationToken token = default(CancellationToken) )
    {
      try
      {
        using (var command = connectionFactory.CreateCommand(sql))
        {
          await connectionFactory.ExecuteWriteAsync(command, token).ConfigureAwait(false);
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
