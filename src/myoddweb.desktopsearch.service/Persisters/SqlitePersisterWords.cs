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
    public async Task<bool> AddOrUpdateWordAsync(Word word, IDbTransaction transaction, CancellationToken token)
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
      await InsertWordsAsync(
        await RebuildWordsListAsync(words, transaction, token).ConfigureAwait(false),
        transaction, token).ConfigureAwait(false);
      return true;
    }

    #region Private word functions
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
      // do we have anything to even look for?
      if (words.Count == 0)
      {
        return new List<long>();
      }

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
          var wordsToAdd = new Words();
          foreach (var word in words)
          {
            pWord.Value = word.Value;
            var value = await ExecuteScalarAsync(cmd, token).ConfigureAwait(false);
            if (null != value && value != DBNull.Value)
            {
              // get the path id.
              ids.Add((long) value);
              continue;
            }

            if (!createIfNotFound)
            {
              // we could not find it and we do not wish to go further.
              ids.Add(-1);
              continue;
            }

            // add this word to our list
            wordsToAdd.Add(word);
          }
          
          // finally we need to add all the words that were not found.
          ids.AddRange( await InsertWordsAsync(wordsToAdd, transaction, token).ConfigureAwait(false));

          // return all the ids we added, (or not).
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
        throw;
      }
    }

    /// <summary>
    /// Given a list of words, re-create the ones that we need to insert.
    /// </summary>
    /// <param name="words"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<List<long>> InsertWordsAsync(Words words, IDbTransaction transaction, CancellationToken token)
    {
      // if we have nothing to do... we are done.
      if (!words.Any())
      {
        return new List<long>();
      }

      // the ids of the words we just added
      var ids = new List<long>( words.Count );

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

        try
        {
          foreach (var word in words)
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            pId.Value = nextId;
            pWord.Value = word.Value;
            if (0 == await ExecuteNonQueryAsync(cmd, token).ConfigureAwait(false))
            {
              _logger.Error($"There was an issue adding word: {word.Value} to persister");
              continue;
            }

            // we now can add the parts
            var partIds = await GetPartIdsAsync(word.Parts( _maxNumCharacters ), transaction, token, true ).ConfigureAwait(false);

            // marry the word id, (that we just added).
            // with the partIds, (that we just added).
            await InsertWordParts(nextId, partIds, transaction, token).ConfigureAwait(false);

            // we added this id.
            ids.Add( nextId );

            // we can now move on to the next folder id.
            ++nextId;
          }

          // all done, return whatever we did.
          return ids;
        }
        catch (OperationCanceledException)
        {
          _logger.Warning("Received cancellation request - Insert multiple words");
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
            pWord.Value = word.Value;
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
          // get out if needed.
          token.ThrowIfCancellationRequested();

          var value = await ExecuteScalarAsync(cmd, token).ConfigureAwait(false);

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
