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
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.helper.Persisters;
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

    #region Member Variables

    private ICountsHelper _countsHelper;

    /// <inheritdoc />
    public IConnectionFactory Factory { get; set; }

    /// <summary>
    /// The current running total of pending updaqtes.
    /// </summary>
    private long? _pendingUpdatesCount;

    /// <summary>
    /// The current running total of files.
    /// </summary>
    private long? _filesCount;

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Readonly factory.
    /// </summary>
    private IConnectionFactory _readOnlyFactory;

    /// <summary>
    /// Return readonly factory if we have one ... otherwise, return the factory.
    /// </summary>
    private IConnectionFactory ReadonlyFactory => _readOnlyFactory ?? Factory;
    #endregion

    public SqlitePersisterCounts(ILogger logger )
    {
      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public void Prepare(IPersister persister, IConnectionFactory factory)
    {
      // save non readonly factory
      if (Factory == null && !factory.IsReadOnly)
      {
        Factory = factory;

        Contract.Assert(_countsHelper == null);
        _countsHelper = new CountsHelper(factory, Tables.Counts);
      }

      // save the readonly factory
      if (_readOnlyFactory == null && factory.IsReadOnly)
      {
        _readOnlyFactory = factory;
      }
    }

    /// <inheritdoc />
    public void Complete(IConnectionFactory factory, bool success)
    {
      if (factory == _readOnlyFactory)
      {
        _readOnlyFactory = null;
      }

      if (factory == Factory)
      {
        _countsHelper = null;
        Factory = null;
      }
    }

    /// <inheritdoc />
    public Task Initialise( CancellationToken token)
    {
      return InitialiserCountersAsync( token);
    }

    /// <inheritdoc />
    public Task<long> GetPendingUpdatesCountAsync( CancellationToken token)
    {
      if (null == _pendingUpdatesCount )
      {
        throw new InvalidOperationException( "The counters have not been initialised!");
      }
      return Task.FromResult( (long)_pendingUpdatesCount );
    }

    /// <inheritdoc />
    public Task<long> GetFilesCountAsync( CancellationToken token)
    {
      if (null == _filesCount)
      {
        throw new InvalidOperationException("The counters have not been initialised!");
      }
      return Task.FromResult((long)_filesCount);
    }

    /// <inheritdoc />
    public async Task<bool> UpdatePendingUpdatesCountAsync(long addOrRemove, CancellationToken token)
    {
      if (!await UpdateCountDirectAsync(Type.PendingUpdate, addOrRemove, token).ConfigureAwait(false))
      {
        return false;
      }

      _pendingUpdatesCount += addOrRemove;
      return true;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateFilesCountAsync(long addOrRemove, CancellationToken token)
    {
      if (!await UpdateCountDirectAsync(Type.File, addOrRemove, token).ConfigureAwait(false))
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
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task InitialiserCountersAsync(CancellationToken token)
    {
      // firt initialise
      await InitialiserCountersAsync(Type.PendingUpdate, token).ConfigureAwait(false);
      await InitialiserCountersAsync(Type.File, token).ConfigureAwait(false);

      // get the pending updates
      _pendingUpdatesCount = await GetCountValue(Type.PendingUpdate, token).ConfigureAwait(false);

      // and get the files count.
      _filesCount = await GetCountValue(Type.File, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Update a single type count.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="addOrRemove"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private Task<bool> UpdateCountDirectAsync(Type type, long addOrRemove, CancellationToken token)
    {
      if (0 == addOrRemove)
      {
        //  tada!
        return Task.FromResult(true);
      }

      Contract.Assert(_countsHelper != null );
      return _countsHelper.AddAsync((long) type, addOrRemove, token);
    }

    /// <summary>
    /// Get a value from the database.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<long?> GetCountValue(Type type, CancellationToken token)
    {
      try
      {
        var sql = $"SELECT count FROM {Tables.Counts} WHERE type=@type";
        using (var cmd = ReadonlyFactory.CreateCommand(sql))
        {
          var pType = cmd.CreateParameter();
          pType.DbType = DbType.Int64;
          pType.ParameterName = "@type";
          cmd.Parameters.Add(pType);

          // run the select query
          pType.Value = (long) type;
          var value = await ReadonlyFactory.ExecuteReadOneAsync(cmd, token).ConfigureAwait(false);
          if (null == value || value == DBNull.Value)
          {
            return null;
          }

          return (long) value;
        }
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
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<long> GetActualCountAsync(Type type, CancellationToken token)
    {
      string sql;
      switch (type)
      {
        case Type.File:
          sql = $"SELECT COUNT(*) as count FROM {Tables.Files}";
          break;

        case Type.PendingUpdate:
          sql = $"SELECT COUNT(*) as count FROM {Tables.FileUpdates}";
          break;

        default:
          throw new ArgumentException($"Unknown count type {type.ToString()}");
      }

      using (var cmd = ReadonlyFactory.CreateCommand(sql))
      {
        var value = await ReadonlyFactory.ExecuteReadOneAsync(cmd, token).ConfigureAwait(false);

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
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task InitialiserCountersAsync(Type type, CancellationToken token)
    {
      // get the vale we will be setting
      var count = await GetActualCountAsync(type, token).ConfigureAwait(false);

      Contract.Assert(_countsHelper != null);
      try
      {
        if (false == await _countsHelper.SetAsync((long)type, count, token).ConfigureAwait(false))
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
    #endregion
  }
}
