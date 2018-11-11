using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal class SqlitePersisterWordsParts : IWordsParts
  {
    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;

    public SqlitePersisterWordsParts(ILogger logger)
    {
      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string TableName => Tables.WordsParts;

    /// <inheritdoc />
    public async Task AddOrUpdateWordParts(long wordId, HashSet<long> partIds, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      // first we need to get the part ids for this word.
      var currentIds = await GetWordParts(wordId, connectionFactory, token).ConfigureAwait(false);

      // remove the ones that are in the current list but not in the new one
      // and add the words that are in the new list but not in the old one.
      // we want to add all the files that are on disk but not on record.
      var idsToAdd = helper.Collection.RelativeComplement(currentIds, partIds);

      // we want to remove all the files that are on record but not on file.
      var idsToRemove = helper.Collection.RelativeComplement(partIds, currentIds);

      // add the words now
      try
      {
        // try and insert the ids directly
        await InsertWordParts( wordId, idsToAdd, connectionFactory, token).ConfigureAwait( false );

        // and try and remove the ids directly
        await DeleteWordParts( wordId, idsToRemove, connectionFactory, token ).ConfigureAwait(false);
      }
      catch (OperationCanceledException e)
      {
        _logger.Warning("Received cancellation request - Add or update word parts in word");
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

    #region Private parts function
    /// <summary>
    /// Remove a bunch of word parts ascociated to a word id.
    /// </summary>
    /// <param name="wordId"></param>
    /// <param name="partIds"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task DeleteWordParts(long wordId, HashSet<long> partIds, IConnectionFactory connectionFactory, CancellationToken token)
    {
      // we might not have anything to do
      if (!partIds.Any())
      {
        return;
      }

      try
      {
        // the query to insert a new word
        var sqlInsert = $"DELETE FROM {TableName} WHERE wordid=@wordid AND partid=@partid";
        using (var cmd = connectionFactory.CreateCommand(sqlInsert))
        {
          var ppId = cmd.CreateParameter();
          ppId.DbType = DbType.Int64;
          ppId.ParameterName = "@partid";
          cmd.Parameters.Add(ppId);

          var pwId = cmd.CreateParameter();
          pwId.DbType = DbType.Int64;
          pwId.ParameterName = "@wordid";
          cmd.Parameters.Add(pwId);

          pwId.Value = wordId;
          foreach (var part in partIds)
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            ppId.Value = part;

            if (0 == await connectionFactory.ExecuteWriteAsync(cmd, token).ConfigureAwait(false))
            {
              _logger.Error($"There was an issue deleting part from word: {part}/{wordId} to persister");
            }
          }
        }
      }
      catch (OperationCanceledException e)
      {
        _logger.Warning( $"Received cancellation request - Deletting parts for word id {wordId}");
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

    /// <summary>
    /// Add a list of ids/words to the table if there are any to add.
    /// </summary>
    /// <param name="wordId"></param>
    /// <param name="partIds"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task InsertWordParts(long wordId, IReadOnlyCollection<long> partIds, IConnectionFactory connectionFactory, CancellationToken token)
    {
      // we might not have anything to do
      if (!partIds.Any())
      {
        return;
      }

      try
      {
        // the query to insert a new word
        var sqlSelect = $"SELECT 1 FROM {TableName} WHERE wordid=@wordid and partid=@partid";
        var sqlInsert = $"INSERT OR IGNORE INTO {TableName} (wordid, partid) VALUES (@wordid, @partid)";
        using (var cmdInsert = connectionFactory.CreateCommand(sqlInsert))
        using (var cmdSelect = connectionFactory.CreateCommand(sqlSelect))
        {
          var ppSId = cmdSelect.CreateParameter();
          ppSId.DbType = DbType.Int64;
          ppSId.ParameterName = "@partid";
          cmdSelect.Parameters.Add(ppSId);

          var pwSId = cmdSelect.CreateParameter();
          pwSId.DbType = DbType.Int64;
          pwSId.ParameterName = "@wordid";
          cmdSelect.Parameters.Add(pwSId);

          var ppIId = cmdInsert.CreateParameter();
          ppIId.DbType = DbType.Int64;
          ppIId.ParameterName = "@partid";
          cmdInsert.Parameters.Add(ppIId);

          var pwIId = cmdInsert.CreateParameter();
          pwIId.DbType = DbType.Int64;
          pwIId.ParameterName = "@wordid";
          cmdInsert.Parameters.Add(pwIId);

          pwIId.Value = wordId;
          pwSId.Value = wordId;
          foreach (var part in partIds)
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            ppIId.Value = part;

            if (1 == await connectionFactory.ExecuteWriteAsync(cmdInsert, token).ConfigureAwait(false))
            {
              continue;
            }

            // make sure that it is not just a duplicate
            ppSId.Value = part;
            var value = await connectionFactory.ExecuteReadOneAsync(cmdSelect, token).ConfigureAwait(false);
            if (null == value || value == DBNull.Value)
            {
              _logger.Error($"There was an issue adding part to word: {part}/{wordId} to persister");
            }
          }
        }
      }
      catch (OperationCanceledException e)
      {
        _logger.Warning( $"Received cancellation request - Insert parts of word {wordId}.");
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

    /// <summary>
    /// Get all the part ids for the given word.
    /// </summary>
    /// <param name="wordId"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    private async Task<HashSet<long>> GetWordParts(long wordId, IConnectionFactory connectionFactory, CancellationToken token)
    {
      try
      {
        // the query to insert a new word
        var sql = $"SELECT partid FROM {TableName} WHERE wordid = @wordid";
        using (var cmd = connectionFactory.CreateCommand(sql))
        {
          var pId = cmd.CreateParameter();
          pId.DbType = DbType.Int64;
          pId.ParameterName = "@wordid";
          cmd.Parameters.Add(pId);

          var partIds = new List<long>();

          // set the folder id.
          pId.Value = wordId;
          var reader = await connectionFactory.ExecuteReadAsync(cmd, token).ConfigureAwait(false);
          while (reader.Read())
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // add this part
            partIds.Add(reader.GetInt64(0));
          }

          // ad we are done.
          return new HashSet<long>(partIds);
        }
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Get Parts id for word");
        throw;
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        throw;
      }
    }
    #endregion
  }
}
