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
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.IO;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal partial class SqlitePersister 
  {
    /// <inheritdoc />
    public async Task<bool> AddOrUpdateWordAsync(IWord word, IDbTransaction transaction, CancellationToken token)
    {
      return await AddOrUpdateWordsAsync( new Words(word), transaction, token ).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> AddOrUpdateWordsAsync(Words words, IDbTransaction transaction, CancellationToken token)
    {
      if (null == transaction)
      {
        throw new ArgumentNullException(nameof(transaction), "You have to be within a tansaction when calling this function.");
      }

      // rebuild the list of directory with only those that need to be inserted.
      return await InsertWordsAsync(
        await RebuildWordsListAsync(words, transaction, token).ConfigureAwait(false),
        transaction, token).ConfigureAwait(false);
    }

    #region Private word functions

    /// <summary>
    /// Get the ID for a given work, add it if we are don't find it and are asked to do.
    /// </summary>
    /// <param name="word"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <param name="createIfNotFound"></param>
    /// <returns></returns>
    private async Task<long> GetWordIdAsync(IWord word, IDbTransaction transaction, CancellationToken token, bool createIfNotFound)
    {
      var ids = await GetWordIdsAsync( new Words(word), transaction, token, createIfNotFound).ConfigureAwait(false);
      return ids.Any() ? ids[0] : -1;
    }

    /// <summary>
    /// Get the id of a list of words we match the words to ids
    ///   [word1] => [id1]
    ///   [word2] => [id2]
    /// </summary>
    /// <param name="words"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <param name="createIfNotFound"></param>
    /// <returns></returns>
    private async Task<List<long>> GetWordIdsAsync(Words words, IDbTransaction transaction, CancellationToken token, bool createIfNotFound)
    {
      try
      {
        // we do not check for the token as the underlying functions will throw if needed. 
        // look for the word, add it if needed.
        var sql = $"SELECT id FROM {TableWords} WHERE word=@word";
        using (var cmd = CreateDbCommand(sql, transaction))
        {
          var pWord = cmd.CreateParameter();
          pWord.DbType = DbType.String;
          pWord.ParameterName = "@word";
          cmd.Parameters.Add(pWord);

          var ids = new List<long>(words.Count);
          foreach (var word in words)
          {
            cmd.Parameters["@word"].Value = word.Word;
            var value = await ExecuteScalarAsync(cmd, token).ConfigureAwait(false);
            if (null == value || value == DBNull.Value)
            {
              if (!createIfNotFound)
              {
                // we could not find it and we do not wish to go further.
                ids.Add(-1);
                continue;
              }

              // try and add the word, if that does not work, then we cannot go further.
              if (!await InsertWordsAsync( new Words(word), transaction, token).ConfigureAwait(false))
              {
                ids.Add(-1);
                continue;
              }

              // try one more time to look for it .. and if we do not find it, then just return -1
              ids.Add(await GetWordIdAsync(word, transaction, token, false).ConfigureAwait(false));
              continue;
            }

            // get the path id.
            ids.Add((long) value);
          }

          return ids;
        }
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Get multiple word ids.");
        throw;
      }
      catch (Exception ex)
      {
        _logger.Exception(ex);
        return Enumerable.Repeat( (long)-1, words.Count ).ToList();
      }
    }

    /// <summary>
    /// Given a list of words, re-create the ones that we need to insert.
    /// </summary>
    /// <param name="words"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> InsertWordsAsync(Words words, IDbTransaction transaction, CancellationToken token)
    {
      // if we have nothing to do... we are done.
      if (!words.Any())
      {
        return true;
      }

      // get the next id.
      var nextId = await GetNextWordIdAsync(transaction, token).ConfigureAwait(false);

      var sqlInsert = $"INSERT INTO {TableWords} (id, word) VALUES (@id, @word)";
      using (var cmd = CreateDbCommand(sqlInsert, transaction))
      {
        var pId = cmd.CreateParameter();
        pId.DbType = DbType.Int64;
        pId.ParameterName = "@id";
        cmd.Parameters.Add(pId);

        var pWord = cmd.CreateParameter();
        pWord.DbType = DbType.String;
        pWord.ParameterName = "@word";
        cmd.Parameters.Add(pWord);

        foreach (var word in words)
        {
          try
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            cmd.Parameters["@id"].Value = nextId;
            cmd.Parameters["@word"].Value = word.Word;
            if (0 == await ExecuteNonQueryAsync(cmd, token).ConfigureAwait(false))
            {
              _logger.Error($"There was an issue adding word: {word} to persister");
              continue;
            }

            // we can now move on to the next folder id.
            ++nextId;
          }
          catch (OperationCanceledException)
          {
            _logger.Warning("Received cancellation request - Insert multiple words");
            throw;
          }
          catch (Exception ex)
          {
            _logger.Exception(ex);
          }
        }
      }
      return true;
    }

    /// <summary>
    /// Given a list of directories, re-create the ones that we need to insert.
    /// </summary>
    /// <param name="words"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<Words> RebuildWordsListAsync(Words words, IDbTransaction transaction, CancellationToken token)
    {
      try
      {
        // make that list unique
        var uniqueList = words.Distinct();

        // The list of words we will be adding to the list.
        var actualWords = new Words();

        // we first look for it, and, if we find it then there is nothing to do.
        var sqlGetRowId = $"SELECT id FROM {TableWords} WHERE word=@word";
        using (var cmd = CreateDbCommand(sqlGetRowId, transaction))
        {
          var pWord = cmd.CreateParameter();
          pWord.DbType = DbType.String;
          pWord.ParameterName = "@word";
          cmd.Parameters.Add(pWord);
          foreach (var word in uniqueList)
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // the words are case sensitive
            cmd.Parameters["@word"].Value = word.Word;
            if (null != await ExecuteScalarAsync(cmd, token).ConfigureAwait(false))
            {
              continue;
            }

            // we could not find this word
            // so we will just add it to our list.
            actualWords.UnionWith(word);
          }
        }

        //  we know that the list is unique thanks to the uniqueness above.
        return actualWords;
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Building word list");
        throw;
      }
    }

    /// <summary>
    /// Get the next row ID we can use.
    /// </summary>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<long> GetNextWordIdAsync(IDbTransaction transaction, CancellationToken token )
    {
      try
      {
        // we first look for it, and, if we find it then there is nothing to do.
        var sqlNextRowId = $"SELECT max(id) from {TableWords};";
        using (var cmd = CreateDbCommand(sqlNextRowId, transaction))
        {
          var value = await ExecuteScalarAsync(cmd, token).ConfigureAwait(false);

          // get out if needed.
          token.ThrowIfCancellationRequested();

          // does not exist ...
          if (null == value || value == DBNull.Value)
          {
            return 0;
          }

          // this is the next counter.
          return ((long) value) + 1;
        }
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Get Next valid Word id");
        throw;
      }
    }
    #endregion
  }
}
