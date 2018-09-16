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
    #region Command classes
    internal class Command : IDisposable
    {
      public IDbCommand Cmd { get; protected set; }

      protected Command()
      {
      }

      public void Dispose()
      {
        Cmd?.Dispose();
      }
    }

    internal class SelectPartCommand : Command
    {
      public IDataParameter Part { get; }

      public SelectPartCommand(IConnectionFactory factory)
      {
        var sqlSelect = $"SELECT id FROM {Tables.Parts} WHERE part = @part";
        Cmd = factory.CreateCommand(sqlSelect);

        Part = Cmd.CreateParameter();
        Part.DbType = DbType.String;
        Part.ParameterName = "@part";
        Cmd.Parameters.Add(Part);
      }
    }
    
    internal class InsertWordCommand : Command
    {
      public IDataParameter Id { get; }

      public IDataParameter Word { get; }

      public InsertWordCommand(IConnectionFactory factory)
      {
        var sqlInsertWord = $"INSERT INTO {Tables.Words} (id, word) VALUES (@id, @word)";
        Cmd = factory.CreateCommand(sqlInsertWord);

        Id = Cmd.CreateParameter();
        Id.DbType = DbType.Int64;
        Id.ParameterName = "@id";
        Cmd.Parameters.Add(Id);

        Word = Cmd.CreateParameter();
        Word.DbType = DbType.String;
        Word.ParameterName = "@word";
        Cmd.Parameters.Add(Word);
      }
    }

    internal class InsertPartCommand : Command
    {
      public IDataParameter Id { get; }

      public IDataParameter Part { get; }

      public long NextId { get; set; }

      public InsertPartCommand(IConnectionFactory factory, long nextId )
      {
        var sqlInsertPart = $"INSERT INTO {Tables.Parts} (id, part) VALUES (@id, @part)";
        Cmd = factory.CreateCommand(sqlInsertPart);

        Id = Cmd.CreateParameter();
        Id.DbType = DbType.Int64;
        Id.ParameterName = "@id";
        Cmd.Parameters.Add(Id);

        Part = Cmd.CreateParameter();
        Part.DbType = DbType.String;
        Part.ParameterName = "@part";
        Cmd.Parameters.Add(Part);

        NextId = nextId;
      }
    }
    #endregion 

    #region Commands creator
    /// <summary>
    /// Create the command to insert a word in the database.
    /// </summary>
    /// <param name="connectionFactory"></param>
    /// <returns></returns>
    private static InsertWordCommand CreateInsertWordCommand(IConnectionFactory connectionFactory)
    {
      return new InsertWordCommand(connectionFactory);
    }

    /// <summary>
    /// Create the command to insert a word in the database.
    /// </summary>
    /// <param name="connectionFactory"></param>
    /// <returns></returns>
    private static SelectPartCommand CreateSelectPartIdCommand(IConnectionFactory connectionFactory)
    {
      return new SelectPartCommand(connectionFactory);
    }

    /// <summary>
    /// Create the command to insert a word in the database.
    /// </summary>
    /// <param name="connectionFactory"></param>
    /// <param name="nextId"></param>
    /// <returns></returns>
    private static InsertPartCommand CreateInsertPartCommand(IConnectionFactory connectionFactory, long nextId)
    {
      return new InsertPartCommand(connectionFactory, nextId );
    }
    #endregion

    #region Member variable
    /// <summary>
    /// The maximum number of characters per words parts...
    /// This is not the max word lenght, but the part lenght
    /// The user cannot enter anything longer in a seatch box.
    /// So search queries longer than that... are ignored.
    /// </summary>
    private readonly int _maxNumCharactersPerParts;

    /// <summary>
    /// The maximum word size, words that are longer ... are ignored.
    /// </summary>
    private readonly int _maxNumCharactersPerWords;

    /// <summary>
    /// The words parts interface
    /// </summary>
    private readonly IWordsParts _wordsParts;

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;
    #endregion

    public SqlitePersisterWords( IWordsParts wordsParts, int maxNumCharactersPerWords, int maxNumCharactersPerParts, ILogger logger)
    {
      // the number of characters per parts.
      _maxNumCharactersPerParts = maxNumCharactersPerParts;

      // the maximum word len
      _maxNumCharactersPerWords = maxNumCharactersPerWords;

      // the words parts interfaces
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
        var sql = $"SELECT id FROM {Tables.Words} WHERE word=@word";
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

      using (var cmdInsertWord = CreateInsertWordCommand(connectionFactory))
      {
        using (var cmdSelectPart = CreateSelectPartIdCommand(connectionFactory))
        {
          // get the next valid id.
          var nextPartId = await GetNextPartIdAsync(connectionFactory, token).ConfigureAwait(false);
          using (var cmdInsertPart = CreateInsertPartCommand(connectionFactory, nextPartId))
          {
            try
            {
              foreach (var word in words)
              {
                // get out if needed.
                token.ThrowIfCancellationRequested();

                // the word is crazy long, so we are ignoring it...
                if (word.Value.Length > _maxNumCharactersPerWords)
                {
                  continue;
                }

                // the word we want to insert.
                if (!await InsertWordAsync(word, nextId, cmdInsertWord, cmdSelectPart, cmdInsertPart, connectionFactory,
                  token).ConfigureAwait(false))
                {
                  continue;
                }

                // we added this id.
                ids.Add(nextId);

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
          }// insert part
        }// using select parts command
      }// using insert word command
    }

    /// <summary>
    /// Insert a word in the database and then insert all the parts
    /// </summary>
    /// <param name="word"></param>
    /// <param name="id"></param>
    /// <param name="cmdInsertWord"></param>
    /// <param name="cmdSelectPart"></param>
    /// <param name="cmdInsertPart"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> InsertWordAsync(
      Word word, 
      long id, 
      InsertWordCommand cmdInsertWord, 
      SelectPartCommand cmdSelectPart,
      InsertPartCommand cmdInsertPart,
      IConnectionFactory connectionFactory,
      CancellationToken token)
    {
      cmdInsertWord.Id.Value = id;
      cmdInsertWord.Word.Value = word.Value;
      if (0 == await connectionFactory.ExecuteWriteAsync(cmdInsertWord.Cmd, token).ConfigureAwait(false))
      {
        _logger.Error($"There was an issue adding word: {word.Value} to persister");
        return false;
      }

      // we now can add/find the parts
      var partIds = await GetOrInsertParts( word, cmdSelectPart, cmdInsertPart, connectionFactory, token).ConfigureAwait(false);

      // marry the word id, (that we just added).
      // with the partIds, (that we just added).
      await _wordsParts.AddOrUpdateWordParts(id, partIds, connectionFactory, token).ConfigureAwait(false);

      // all done.
      return true;
    }

    public async Task<HashSet<long>> GetOrInsertParts(
      Word word,
      SelectPartCommand cmdSelectPart,
      InsertPartCommand cmdInsertPart,
      IConnectionFactory connectionFactory, 
      CancellationToken token)
    {
      // get the parts.
      var parts = word.Parts(_maxNumCharactersPerParts);

      // the ids of all the parts, (added or otherwise).
      var partIds = new List<long>(parts.Count);

      // if we have not words... then move on.
      if (!parts.Any())
      {
        return new HashSet<long>(partIds);
      }

      var cmdSelect = cmdSelectPart.Cmd;

      // the parts we actually need to add.
      var partsToAdd = new List<string>(parts.Count);

      foreach (var part in parts)
      {
        // get out if needed.
        token.ThrowIfCancellationRequested();

        // the part we are adding
        cmdSelectPart.Part.Value = part;

        // look for that part, if it exists, add it to the list
        // otherwird we will need to add it.
        var value = await connectionFactory.ExecuteReadOneAsync(cmdSelect, token).ConfigureAwait(false);
        if (null != value && value != DBNull.Value)
        {
          partIds.Add((long)value);
          continue;
        }

        // we could not locate this part
        // so it will need to be added.
        partsToAdd.Add(part);
      }

      // then add the ids of the remaining parts.
      partIds.AddRange(await InsertPartsAsync(partsToAdd.ToArray(), cmdInsertPart, connectionFactory, token).ConfigureAwait(false));

      // return everything we found.
      return new HashSet<long>(partIds);
    }

    /// <summary>
    /// Insert parts and return the id of the added parts.
    /// </summary>
    /// <param name="parts"></param>
    /// <param name="cmdInsertPart"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<List<long>> InsertPartsAsync(IReadOnlyCollection<string> parts, InsertPartCommand cmdInsertPart, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (!parts.Any())
      {
        return new List<long>();
      }

      // the ids of all the parts inserted.
      var partIds = new List<long>(parts.Count);

      var cmdInsert = cmdInsertPart.Cmd;
      foreach (var part in parts.Distinct())
      {
        cmdInsertPart.Id.Value = cmdInsertPart.NextId;
        cmdInsertPart.Part.Value = part;

        if (0 == await connectionFactory.ExecuteWriteAsync(cmdInsert, token).ConfigureAwait(false))
        {
          _logger.Error($"There was an issue adding part: {part} to persister");
          continue;
        }

        // we added it, so we can add it to our list
        partIds.Add(cmdInsertPart.NextId);

        // and move on to the next id.
        ++cmdInsertPart.NextId;
      }
      // return all the ids we added.
      return partIds;
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
        var sql = $"SELECT id FROM {Tables.Words} WHERE word=@word";
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
    private async Task<long> GetNextPartIdAsync(IConnectionFactory connectionFactory, CancellationToken token)
    {
      try
      {
        // we first look for it, and, if we find it then there is nothing to do.
        var sql = $"SELECT max(id) from {Tables.Parts};";
        using (var cmd = connectionFactory.CreateCommand(sql))
        {
          var value = await connectionFactory.ExecuteReadOneAsync(cmd, token).ConfigureAwait(false);

          // get out if needed.
          token.ThrowIfCancellationRequested();

          // does not exist ...
          if (null == value || value == DBNull.Value)
          {
            return 0;
          }

          // this is the next counter.
          return ((long)value) + 1;
        }
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Get Next valid Part id");
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
        var sql = $"SELECT max(id) from {Tables.Words};";
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
