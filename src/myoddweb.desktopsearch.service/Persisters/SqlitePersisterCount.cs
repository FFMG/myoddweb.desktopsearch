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
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal partial class SqlitePersister
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

    /// <inheritdoc />
    public async Task<long> GetPendingUpdatesCountAsync(IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == _pendingUpdatesCount && connectionFactory.IsReadOnly )
      {
        return 0;
      }
      await InitialiserCountersAsync(connectionFactory, token).ConfigureAwait(false);
      return (long)_pendingUpdatesCount;
    }

    /// <inheritdoc />
    public async Task<long> GetFilesCountAsync(IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == _filesCount && connectionFactory.IsReadOnly)
      {
        return 0;
      }
      await InitialiserCountersAsync(connectionFactory, token).ConfigureAwait(false);
      return (long)_filesCount;
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
      // do we ahve anything to do?
      if (null != _pendingUpdatesCount)
      {
        return;
      }

      var sql = $"SELECT count FROM {TableCounts} WHERE type=@type";
      using (var cmd = connectionFactory.CreateCommand(sql))
      {
        var pType = cmd.CreateParameter();
        pType.DbType = DbType.Int64;
        pType.ParameterName = "@type";
        cmd.Parameters.Add(pType);

        // get the pending updates
        _pendingUpdatesCount = await InitialiserCountersAsync(Type.PendingUpdate, cmd, pType, connectionFactory, token).ConfigureAwait(false);

        // and get the files count.
        _filesCount = await InitialiserCountersAsync(Type.File, cmd, pType, connectionFactory, token).ConfigureAwait(false);
      }
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
      // make sure that the values exist
      await InitialiserCountersAsync(connectionFactory, token).ConfigureAwait(false);

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
    /// Query the db to get a count value
    /// </summary>
    /// <param name="type"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<long> GetCountDirectAsync(Type type, IConnectionFactory connectionFactory,CancellationToken token)
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
    /// <param name="cmd"></param>
    /// <param name="param"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<long> InitialiserCountersAsync(Type type, IDbCommand cmd, IDbDataParameter param, IConnectionFactory connectionFactory, CancellationToken token)
    {
      // run the select query
      param.Value = (long)type;
      var value = await connectionFactory.ExecuteReadOneAsync(cmd, token).ConfigureAwait(false);
      if (null != value && value != DBNull.Value)
      {
        // the values does not exist
        // so we will add it here.
        return (long) value;
      }

      var count = await GetCountDirectAsync(type, connectionFactory, token).ConfigureAwait(false);

      // the value does not exist, we have to get it.
      var sql = $"INSERT INTO {TableCounts} (type, count) VALUES (@type, @count)";

      using (var cmdInsert = connectionFactory.CreateCommand(sql))
      {
        var pType = cmdInsert.CreateParameter();
        pType.DbType = DbType.Int64;
        pType.ParameterName = "@type";
        cmdInsert.Parameters.Add(pType);

        var pCount = cmdInsert.CreateParameter();
        pCount.DbType = DbType.Int64;
        pCount.ParameterName = "@count";
        cmdInsert.Parameters.Add(pCount);

        try
        {
          // get out if needed.
          token.ThrowIfCancellationRequested();

          pType.Value = (long)type;
          pCount.Value = count;
          if (0 == await connectionFactory.ExecuteWriteAsync(cmd, token).ConfigureAwait(false))
          {
            _logger.Error($"There was an issue adding count {type.ToString()} to persister");
            return 0;
          }

          // all done, return whatever we did.
          return count;
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
