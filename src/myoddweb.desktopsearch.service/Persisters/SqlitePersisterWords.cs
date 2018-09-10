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
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal class SqlitePersisterWords : IWords
  {
    /// <summary>
    /// The maximum number of characters per words...
    /// Characters after that are ignored.
    /// </summary>
    private readonly int _maxNumCharacters;

    /// <summary>
    /// The counts table name
    /// </summary>
    private string TableWords { get; }

    /// <summary>
    /// The parts interface
    /// </summary>
    private readonly IParts _parts;

    /// <summary>
    /// The words parts interface
    /// </summary>
    private readonly IWordsParts _wordsParts;

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;

    public SqlitePersisterWords( IParts parts, IWordsParts wordsParts, int maxNumCharacters, string tableName, ILogger logger)
    {
      // the number of characters.
      _maxNumCharacters = maxNumCharacters;

      // save the table name
      TableWords = tableName;

      // the words parts interfaces
      _parts = parts ?? throw new ArgumentNullException(nameof(parts));
      _wordsParts = wordsParts ?? throw new ArgumentNullException(nameof(wordsParts));

      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<long> AddOrUpdateWordAsync(Word word, IConnectionFactory connectionFactory, CancellationToken token)
    {
      var ids = await AddOrUpdateWordsAsync( new Words(word), connectionFactory, token ).ConfigureAwait(false);
      return ids.Any() ? ids.First() : -1;
    }

    /// <inheritdoc />
    public async Task<IList<long>> AddOrUpdateWordsAsync(Words words, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      // rebuild the list of directory with only those that need to be inserted.
      return await InsertWordsAsync(
        await RebuildWordsListAsync(words, connectionFactory, token).ConfigureAwait(false),
        connectionFactory, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<List<long>> GetWordIdsAsync(Words words, IConnectionFactory connectionFactory, CancellationToken token, bool createIfNotFound)
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
        using (var cmd = connectionFactory.CreateCommand(sql))
        {
          var pWord = cmd.CreateParameter();
          pWord.DbType = DbType.String;
          pWord.ParameterName = "@word";
          cmd.Parameters.Add(pWord);

          var ids = new List<long>(words.Count);

          // we use a list to allow duplicates.
          var wordsToAdd = new List<Word>();
          foreach (var word in words)
          {
            pWord.Value = word.Value;
            var value = await connectionFactory.ExecuteReadOneAsync(cmd, token).ConfigureAwait(false);
            if (null != value && value != DBNull.Value)
            {
              // get the path id.
              ids.Add((long)value);
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
          ids.AddRange(await AddOrUpdateWordsAsync(new Words(wordsToAdd.ToArray()), connectionFactory, token).ConfigureAwait(false));

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
    #region Private word functions
    /// <summary>
    /// Given a list of words, re-create the ones that we need to insert.
    /// </summary>
    /// <param name="words"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<List<long>> InsertWordsAsync(Words words, IConnectionFactory connectionFactory, CancellationToken token)
    {
      // if we have nothing to do... we are done.
      if (!words.Any())
      {
        return new List<long>();
      }

      // the ids of the words we just added
      var ids = new List<long>( words.Count );

      // get the next id.
      var nextId = await GetNextWordIdAsync(connectionFactory, token).ConfigureAwait(false);

      var sqlInsert = $"INSERT INTO {TableWords} (id, word) VALUES (@id, @word)";
      using (var cmd = connectionFactory.CreateCommand(sqlInsert))
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
            if (0 == await connectionFactory.ExecuteWriteAsync(cmd, token).ConfigureAwait(false))
            {
              _logger.Error($"There was an issue adding word: {word.Value} to persister");
              continue;
            }

            // we now can add the parts
            var partIds = await _parts.GetPartIdsAsync(word.Parts( _maxNumCharacters ), connectionFactory, token, true ).ConfigureAwait(false);

            // marry the word id, (that we just added).
            // with the partIds, (that we just added).
            await _wordsParts.AddOrUpdateWordParts(nextId, partIds, connectionFactory, token).ConfigureAwait(false);

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
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<Words> RebuildWordsListAsync(Words words, IConnectionFactory connectionFactory, CancellationToken token)
    {
      try
      {
        // The list of words we will be adding to the list.
        var actualWords = new List<string>(words.Count);

        // we first look for it, and, if we find it then there is nothing to do.
        var sql = $"SELECT id FROM {TableWords} WHERE word=@word";
        using (var cmd = connectionFactory.CreateCommand(sql))
        {
          var pWord = cmd.CreateParameter();
          pWord.DbType = DbType.String;
          pWord.ParameterName = "@word";
          cmd.Parameters.Add(pWord);
          foreach (var word in words)
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // the words are case sensitive
            pWord.Value = word.Value;
            if (null != await connectionFactory.ExecuteReadOneAsync(cmd, token).ConfigureAwait(false))
            {
              continue;
            }

            // we could not find this word
            // so we will just add it to our list.
            actualWords.Add(word.Value);
          }
        }

        //  we know that the list is unique thanks to the uniqueness above.
        return new Words( actualWords );
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
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<long> GetNextWordIdAsync(IConnectionFactory connectionFactory, CancellationToken token )
    {
      try
      {
        // we first look for it, and, if we find it then there is nothing to do.
        var sql = $"SELECT max(id) from {TableWords};";
        using (var cmd = connectionFactory.CreateCommand(sql))
        {
          // get out if needed.
          token.ThrowIfCancellationRequested();

          var value = await connectionFactory.ExecuteReadOneAsync(cmd, token).ConfigureAwait(false);

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
