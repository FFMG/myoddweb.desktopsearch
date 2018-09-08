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
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal class SqlitePersisterCounts : ICounts
  {
    private enum Type
    {
      PendingUpdate,
      File
    }

    /// <summary>
    /// The current running total of pending updaqtes.
    /// </summary>
    private long? _pendingUpdatesCount;

    /// <summary>
    /// The current running total of files.
    /// </summary>
    private long? _filesCount;

    /// <summary>
    /// The counts table name
    /// </summary>
    private string TableCounts { get; }

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;

    public SqlitePersisterCounts(string tableName, ILogger logger )
    {
      // save the table name
      TableCounts = tableName;

      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task Initialise(IConnectionFactory connectionFactory, CancellationToken token)
    {
      await InitialiserCountersAsync(connectionFactory, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<long> GetPendingUpdatesCountAsync(IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == _pendingUpdatesCount )
      {
        throw new InvalidOperationException( "The counters have not been initialised!");
      }
      return Task.FromResult( (long)_pendingUpdatesCount );
    }

    /// <inheritdoc />
    public Task<long> GetFilesCountAsync(IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == _filesCount)
      {
        throw new InvalidOperationException("The counters have not been initialised!");
      }
      return Task.FromResult((long)_filesCount);
    }

    /// <inheritdoc />
    public async Task<bool> UpdatePendingUpdatesCountAsync(long addOrRemove, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (!await UpdateCountDirectAsync(Type.PendingUpdate, addOrRemove, connectionFactory, token).ConfigureAwait(false))
      {
        return false;
      }

      _pendingUpdatesCount += addOrRemove;
      return true;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateFilesCountAsync(long addOrRemove, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (!await UpdateCountDirectAsync(Type.File, addOrRemove, connectionFactory, token).ConfigureAwait(false))
      {
        return false;
      }

      _filesCount += addOrRemove;
      return true;
    }

    #region Private functions
    /// <summary>
    /// Initialise all the counters.
    /// </summary>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task InitialiserCountersAsync(IConnectionFactory connectionFactory, CancellationToken token)
    {
      // firt initialise
      await InitialiserCountersAsync(Type.PendingUpdate, connectionFactory, token).ConfigureAwait(false);
      await InitialiserCountersAsync(Type.File, connectionFactory, token).ConfigureAwait(false);

      // get the pending updates
      _pendingUpdatesCount = await GetCountValue(Type.PendingUpdate, connectionFactory, token).ConfigureAwait(false);

      // and get the files count.
      _filesCount = await GetCountValue(Type.File, connectionFactory, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Update a single type count.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="addOrRemove"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> UpdateCountDirectAsync(Type type, long addOrRemove, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (0 == addOrRemove)
      {
        //  tada!
        return true;
      }

      var sql = $"UPDATE {TableCounts} SET count=count+@count WHERE type=@type";

      using (var cmd = connectionFactory.CreateCommand(sql))
      {
        var pType = cmd.CreateParameter();
        pType.DbType = DbType.Int64;
        pType.ParameterName = "@type";
        cmd.Parameters.Add(pType);

        var pCount = cmd.CreateParameter();
        pCount.DbType = DbType.Int64;
        pCount.ParameterName = "@count";
        cmd.Parameters.Add(pCount);
        
        pCount.Value = addOrRemove;
        pType.Value = (long)type;

        // get out if needed.
        token.ThrowIfCancellationRequested();

        if (0 == await connectionFactory.ExecuteWriteAsync(cmd, token).ConfigureAwait(false))
        {
          // 
          _logger.Error($"I was unable to update count for {type.ToString()}, how is it posible?");
          return false;
        }

        // all good.
        return true;
      }
    }

    /// <summary>
    /// Get a value from the database.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<long?> GetCountValue(Type type, IConnectionFactory connectionFactory, CancellationToken token)
    {
      try
      {
        var sql = $"SELECT count FROM {TableCounts} WHERE type=@type";
        using (var cmd = connectionFactory.CreateCommand(sql))
        {
          var pType = cmd.CreateParameter();
          pType.DbType = DbType.Int64;
          pType.ParameterName = "@type";
          cmd.Parameters.Add(pType);

          // run the select query
          pType.Value = (long) type;
          var value = await connectionFactory.ExecuteReadOneAsync(cmd, token).ConfigureAwait(false);
          if (null == value || value == DBNull.Value)
          {
            return null;
          }

          return (long) value;
        }
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Getting count value.");
        throw;
      }
      catch (Exception ex)
      {
        _logger.Exception(ex);
        throw;
      }
    }

    /// <summary>
    /// Query the db to get a count value
    /// </summary>
    /// <param name="type"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<long> GetActualCountAsync(Type type, IConnectionFactory connectionFactory,CancellationToken token)
    {
      string sql;
      switch (type)
      {
        case Type.File:
          sql = "SELECT COUNT(*) as count FROM Files";
          break;

        case Type.PendingUpdate:
          sql = "SELECT COUNT(*) as count FROM FileUpdates";
          break;

        default:
          throw new ArgumentException($"Unknown count type {type.ToString()}");
      }

      using (var cmd = connectionFactory.CreateCommand(sql))
      {
        var value = await connectionFactory.ExecuteReadOneAsync(cmd, token).ConfigureAwait(false);

        // get out if needed.
        token.ThrowIfCancellationRequested();

        if (null != value && value != DBNull.Value)
        {
          // the values does not exist
          // so we will add it here.
          return (long)value;
        }

        // hey? could not get the count??
        _logger.Error( $"I was unable to get a simple count(*) for {type.ToString()}, how is it posible?");
        return 0;
      }
    }

    /// <summary>
    /// Initialize a single count.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task InitialiserCountersAsync(Type type, IConnectionFactory connectionFactory, CancellationToken token)
    {
      // do we have a value in the database?
      var current = await GetCountValue(type, connectionFactory, token).ConfigureAwait(false);
      if ( null != current )
      {
        // the value exists already ... so we want to update.
        await UpdateCountersAsync(type, connectionFactory, token).ConfigureAwait(false);
        return;
      }

      // insert the value for that type.
      await InsertCountersAsync(type, connectionFactory, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Update the value for the given type.
    /// We will not check if the value already exists!
    /// </summary>
    /// <param name="type"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task UpdateCountersAsync(Type type, IConnectionFactory connectionFactory, CancellationToken token)
    {
      // get the vale we will be updating
      var count = await GetActualCountAsync(type, connectionFactory, token).ConfigureAwait(false);

      // the value does not exist, we have to get it.
      var sql = $"UPDATE {TableCounts} SET count=@count WHERE type=@type";

      using (var cmd = connectionFactory.CreateCommand(sql))
      {
        var pType = cmd.CreateParameter();
        pType.DbType = DbType.Int64;
        pType.ParameterName = "@type";
        cmd.Parameters.Add(pType);

        var pCount = cmd.CreateParameter();
        pCount.DbType = DbType.Int64;
        pCount.ParameterName = "@count";
        cmd.Parameters.Add(pCount);

        try
        {
          // get out if needed.
          token.ThrowIfCancellationRequested();

          pType.Value = (long)type;
          pCount.Value = count;
          if (0 == await connectionFactory.ExecuteWriteAsync(cmd, token).ConfigureAwait(false))
          {
            _logger.Error($"There was an issue updating the count {type.ToString()} to persister");
          }
        }
        catch (OperationCanceledException)
        {
          _logger.Warning("Received cancellation request - Update counter");
          throw;
        }
        catch (Exception ex)
        {
          _logger.Exception(ex);
          throw;
        }
      }
    }

    /// <summary>
    /// Insert the value for the given type.
    /// We will not check if the value already exists!
    /// </summary>
    /// <param name="type"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task InsertCountersAsync(Type type, IConnectionFactory connectionFactory, CancellationToken token)
    { 
      // get the vale we will be inserting
      var count = await GetActualCountAsync(type, connectionFactory, token).ConfigureAwait(false);

      // the value does not exist, we have to get it.
      var sql = $"INSERT INTO {TableCounts} (type, count) VALUES (@type, @count)";

      using (var cmd = connectionFactory.CreateCommand(sql))
      {
        var pType = cmd.CreateParameter();
        pType.DbType = DbType.Int64;
        pType.ParameterName = "@type";
        cmd.Parameters.Add(pType);

        var pCount = cmd.CreateParameter();
        pCount.DbType = DbType.Int64;
        pCount.ParameterName = "@count";
        cmd.Parameters.Add(pCount);

        try
        {
          // get out if needed.
          token.ThrowIfCancellationRequested();

          pType.Value = (long)type;
          pCount.Value = count;
          if (0 == await connectionFactory.ExecuteWriteAsync(cmd, token).ConfigureAwait(false))
          {
            _logger.Error($"There was an issue adding count {type.ToString()} to persister");
          }
        }
        catch (OperationCanceledException)
        {
          _logger.Warning("Received cancellation request - Insert counter");
          throw;
        }
        catch (Exception ex)
        {
          _logger.Exception(ex);
          throw;
        }
      }
    }
    #endregion
  }
}
