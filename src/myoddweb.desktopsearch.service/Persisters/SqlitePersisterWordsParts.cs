using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal partial class SqlitePersister
  {
    /// <inheritdoc />
    public async Task AddOrUpdateWordParts(long wordId, HashSet<long> partIds, IDbTransaction transaction, CancellationToken token)
    {
      if (null == transaction)
      {
        throw new ArgumentNullException(nameof(transaction), "You have to be within a tansaction when calling this function.");
      }

      // first we need to get the part ids for this word.
      var currentIds = await GetWordParts(wordId, transaction, token).ConfigureAwait(false);

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
        await InsertWordParts( wordId, idsToAdd, transaction, token).ConfigureAwait( false );

        // and try and remove the ids directly
        await DeleteWordParts( wordId, idsToRemove, transaction, token ).ConfigureAwait(false);
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Inserting parts in word");
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
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task DeleteWordParts(long wordId, HashSet<long> partIds, IDbTransaction transaction, CancellationToken token)
    {
      // we might not have anything to do
      if (!partIds.Any())
      {
        return;
      }

      // the query to insert a new word
      var sqlInsert = $"DELETE FROM {TableWordsParts} WHERE wordid=@wordid AND partid=@partid";
      using (var cmd = CreateDbCommand(sqlInsert, transaction))
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

          if (0 == await ExecuteNonQueryAsync(cmd, token).ConfigureAwait(false))
          {
            _logger.Error($"There was an issue deleting part from word: {part}/{wordId} to persister");
          }
        }
      }
    }

    /// <summary>
    /// Add a list of ids/words to the table if there are any to add.
    /// </summary>
    /// <param name="wordId"></param>
    /// <param name="partIds"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task InsertWordParts(long wordId, IReadOnlyCollection<long> partIds, IDbTransaction transaction, CancellationToken token)
    {
      // we might not have anything to do
      if (!partIds.Any())
      {
        return;
      }

      // the query to insert a new word
      var sqlInsert = $"INSERT INTO {TableWordsParts} (wordid, partid) VALUES (@wordid, @partid)";
      using (var cmd = CreateDbCommand(sqlInsert, transaction))
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

          if (0 == await ExecuteNonQueryAsync(cmd, token).ConfigureAwait(false))
          {
            _logger.Error($"There was an issue adding part to word: {part}/{wordId} to persister");
          }
        }
      }
    }

    /// <summary>
    /// Get all the part ids for the given word.
    /// </summary>
    /// <param name="wordId"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    private async Task<HashSet<long>> GetWordParts(long wordId, IDbTransaction transaction, CancellationToken token)
    {
      try
      {
        // the query to insert a new word
        var sql = $"SELECT partid FROM {TableWordsParts} WHERE wordid = @wordid";
        using (var cmd = CreateDbCommand(sql, transaction))
        {
          var pId = cmd.CreateParameter();
          pId.DbType = DbType.Int64;
          pId.ParameterName = "@wordid";
          cmd.Parameters.Add(pId);

          var partIds = new HashSet<long>();

          // set the folder id.
          pId.Value = wordId;
          var reader = await ExecuteReaderAsync(cmd, token).ConfigureAwait(false);
          while (reader.Read())
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // add this part
            partIds.Add((long) reader["partid"]);
          }

          // ad we are done.
          return partIds;
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
