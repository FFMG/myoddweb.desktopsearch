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
using myoddweb.desktopsearch.interfaces.Configs;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Persisters;
using ILogger = myoddweb.desktopsearch.interfaces.Logging.ILogger;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal class SqlPerformanceCounter : helper.Performance.Counter
  {
    public SqlPerformanceCounter(IPerformance performance, string counterName, ILogger logger) :
      base(performance.CategoryName, performance.CategoryHelp, counterName, helper.Performance.Type.Timed, logger)
    {
    }
  }

  internal class SqlitePersisterWords : interfaces.Persisters.IWords, IDisposable
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

    internal class SelectWordCommand : Command
    {
      public IDataParameter Word { get; }

      public SelectWordCommand(IConnectionFactory factory)
      {
        var sqlSelect = $"SELECT id FROM {Tables.Words} WHERE word = @word";
        Cmd = factory.CreateCommand(sqlSelect);

        Word = Cmd.CreateParameter();
        Word.DbType = DbType.String;
        Word.ParameterName = "@word";
        Cmd.Parameters.Add(Word);
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
      public IDataParameter Word { get; }

      public InsertWordCommand(IConnectionFactory factory)
      {
        var sqlInsertWord = $"INSERT OR IGNORE INTO {Tables.Words} (word) VALUES (@word)";
        Cmd = factory.CreateCommand(sqlInsertWord);

        Word = Cmd.CreateParameter();
        Word.DbType = DbType.String;
        Word.ParameterName = "@word";
        Cmd.Parameters.Add(Word);
      }
    }

    internal class InsertPartCommand : Command
    {
      public IDataParameter Part { get; }

      public InsertPartCommand(IConnectionFactory factory )
      {
        var sqlInsertPart = $"INSERT OR IGNORE INTO {Tables.Parts} (part) VALUES (@part)";
        Cmd = factory.CreateCommand(sqlInsertPart);

        Part = Cmd.CreateParameter();
        Part.DbType = DbType.String;
        Part.ParameterName = "@part";
        Cmd.Parameters.Add(Part);
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

    private static SelectWordCommand CreateSelectWordIdCommand(IConnectionFactory connectionFactory)
    {
      return new SelectWordCommand( connectionFactory );
    }

    /// <summary>
    /// Create the command to insert a word in the database.
    /// </summary>
    /// <param name="connectionFactory"></param>
    /// <returns></returns>
    private static InsertPartCommand CreateInsertPartCommand(IConnectionFactory connectionFactory)
    {
      return new InsertPartCommand(connectionFactory );
    }
    #endregion

    #region Member variable
    /// <summary>
    ///  Our proc counter.
    /// </summary>
    private readonly SqlPerformanceCounter _counter;

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

    public SqlitePersisterWords(IPerformance performance, IWordsParts wordsParts, int maxNumCharactersPerWords, int maxNumCharactersPerParts, ILogger logger)
    {
      // the number of characters per parts.
      _maxNumCharactersPerParts = maxNumCharactersPerParts;

      // the maximum word len
      _maxNumCharactersPerWords = maxNumCharactersPerWords;

      // the words parts interfaces
      _wordsParts = wordsParts ?? throw new ArgumentNullException(nameof(wordsParts));

      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      // create the counter,
      _counter = new SqlPerformanceCounter(performance, "Database: IWords Add Or Update", _logger);
    }

    public void Dispose()
    {
      _counter?.Dispose();
    }

    /// <inheritdoc />
    public async Task<long> AddOrUpdateWordAsync(IWord word, IConnectionFactory connectionFactory, CancellationToken token)
    {
      var ids = await AddOrUpdateWordsAsync( new helper.IO.Words(word), connectionFactory, token ).ConfigureAwait(false);
      return ids.Any() ? ids.First() : -1;
    }

    /// <inheritdoc />
    public async Task<IList<long>> AddOrUpdateWordsAsync(interfaces.IO.IWords words, IConnectionFactory connectionFactory, CancellationToken token)
    {
      using (_counter.Start())
      {
        if (null == connectionFactory)
        {
          throw new ArgumentNullException(nameof(connectionFactory),
            "You have to be within a tansaction when calling this function.");
        }

        // rebuild the list of directory with only those that need to be inserted.
        return await InsertWordsAsync(
          await RebuildWordsListAsync(words, connectionFactory, token).ConfigureAwait(false),
          connectionFactory, token).ConfigureAwait(false);
      }
    }

    /// <inheritdoc />
    public async Task<IList<long>> GetWordIdsAsync(interfaces.IO.IWords words, IConnectionFactory connectionFactory, CancellationToken token, bool createIfNotFound)
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
          var wordsToAdd = new List<IWord>();
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
          ids.AddRange(await AddOrUpdateWordsAsync(new helper.IO.Words(wordsToAdd.ToArray()), connectionFactory, token).ConfigureAwait(false));

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
    private async Task<IList<long>> InsertWordsAsync(interfaces.IO.IWords words, IConnectionFactory connectionFactory, CancellationToken token)
    {
      // if we have nothing to do... we are done.
      if (!words.Any())
      {
        return new List<long>();
      }

      // the ids of the words we just added
      var ids = new List<long>( words.Count );

      using (var cmdInsertWord = CreateInsertWordCommand(connectionFactory))
      using (var cmdSelectWord = CreateSelectWordIdCommand(connectionFactory))
      using (var cmdSelectPart = CreateSelectPartIdCommand(connectionFactory))
      using (var cmdInsertPart = CreateInsertPartCommand(connectionFactory))
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
            var wordId = await InsertWordAsync(word, cmdSelectWord, cmdInsertWord, cmdSelectPart, cmdInsertPart, connectionFactory, token)                .ConfigureAwait(false);
            if (-1 == wordId)
            {
              // we log errors in the insert function
              continue;
            }

            // we added this id.
            ids.Add(wordId);
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
      }// using insert word command
    }

    /// <summary>
    /// Insert a word in the database and then insert all the parts
    /// </summary>
    /// <param name="word"></param>
    /// <param name="cmdSelectWord"></param>
    /// <param name="cmdInsertWord"></param>
    /// <param name="cmdSelectPart"></param>
    /// <param name="cmdInsertPart"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<long> InsertWordAsync(
      IWord word,
      SelectWordCommand cmdSelectWord,
      InsertWordCommand cmdInsertWord, 
      SelectPartCommand cmdSelectPart,
      InsertPartCommand cmdInsertPart,
      IConnectionFactory connectionFactory,
      CancellationToken token)
    {
      long wordId;
      cmdInsertWord.Word.Value = word.Value;
      if (0 == await connectionFactory.ExecuteWriteAsync(cmdInsertWord.Cmd, token).ConfigureAwait(false))
      {
        // maybe the word exists already?
        wordId = await GetWordId(word, cmdSelectWord, connectionFactory, token).ConfigureAwait(false);
        if (wordId == -1)
        {
          _logger.Error($"There was an issue adding word: {word.Value} to persister");
          return -1;
        }
      }
      else
      {
        // get the id of the word we just added.
        wordId = await GetWordId(word, cmdSelectWord, connectionFactory, token).ConfigureAwait(false);
        if( wordId == -1 )
        {
          _logger.Error($"There was an issue getting the word id: {word.Value} from the persister");
          return -1;
        }
      }

      // we now can add/find the parts for that word.
      var partIds = await GetOrInsertParts( word, cmdSelectPart, cmdInsertPart, connectionFactory, token).ConfigureAwait(false);

      // marry the word id, (that we just added).
      // with the partIds, (that we just added).
      await _wordsParts.AddOrUpdateWordParts(wordId, partIds, connectionFactory, token).ConfigureAwait(false);

      // all done return the id of the word.
      return wordId;
    }

    private async Task<HashSet<long>> GetOrInsertParts(
      IWord word,
      SelectPartCommand cmdSelectPart,
      InsertPartCommand cmdInsertPart,
      IConnectionFactory connectionFactory, 
      CancellationToken token)
    {
      // get the parts.
      var parts = word.Parts(_maxNumCharactersPerParts);

      // if we have not words... then move on.
      if (!parts.Any())
      {
        return new HashSet<long>();
      }

      // the ids of all the parts, (added or otherwise).
      var partIds = new List<long>(parts.Count);

      // the parts we actually need to add.
      var partsToAdd = new List<string>(parts.Count);

      foreach (var part in parts)
      {
        // get out if needed.
        token.ThrowIfCancellationRequested();

        // look for that part, if it exists, add it to the list
        // otherwird we will need to add it.
        var partId = await GetPartId(part, cmdSelectPart, connectionFactory, token).ConfigureAwait( false );
        if (partId != -1)
        {
          partIds.Add(partId);
          continue;
        }

        // we could not locate this part
        // so it will need to be added.
        partsToAdd.Add(part);
      }

      // then add the ids of the remaining parts.
      partIds.AddRange(await InsertPartsAsync(partsToAdd.ToArray(), cmdSelectPart, cmdInsertPart, connectionFactory, token).ConfigureAwait(false));

      // return everything we found.
      return new HashSet<long>(partIds);
    }

    private async Task<long> GetWordId(
      IWord word,
      SelectWordCommand cmdSelectWord,
      IConnectionFactory connectionFactory,
      CancellationToken token)
    {
      cmdSelectWord.Word.Value = word.Value;
      var value = await connectionFactory.ExecuteReadOneAsync(cmdSelectWord.Cmd, token).ConfigureAwait(false);
      if (null == value || value == DBNull.Value)
      {
        _logger.Error($"There was an issue getting the word id: {word.Value} from the persister");
        return -1;
      }

      return (long)value;
    }

    private async Task<long> GetPartId(
      string part,
      SelectPartCommand cmdSelectPart,
      IConnectionFactory connectionFactory,
      CancellationToken token)
    {
      // the part we are adding
      cmdSelectPart.Part.Value = part;

      // look for that part, if it exists, add it to the list
      // otherwird we will need to add it.
      var value = await connectionFactory.ExecuteReadOneAsync(cmdSelectPart.Cmd, token).ConfigureAwait(false);
      if (null != value && value != DBNull.Value)
      {
        return (long) value;
      }
      return -1;
    }

/// <summary>
/// Insert parts and return the id of the added parts.
/// </summary>
/// <param name="parts"></param>
/// <param name="cmdSelectPart"></param>
/// <param name="cmdInsertPart"></param>
/// <param name="connectionFactory"></param>
/// <param name="token"></param>
/// <returns></returns>
private async Task<IList<long>> InsertPartsAsync(
      ICollection<string> parts, 
      SelectPartCommand cmdSelectPart,
      InsertPartCommand cmdInsertPart, 
      IConnectionFactory connectionFactory, 
      CancellationToken token)
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
        long partId;
        cmdInsertPart.Part.Value = part;
        if (0 == await connectionFactory.ExecuteWriteAsync(cmdInsert, token).ConfigureAwait(false))
        {
          partId = await GetPartId(part, cmdSelectPart, connectionFactory, token).ConfigureAwait(false);
          if (partId != -1)
          {
            // this part already exists, so we could not add it again.
            // but the id is still value.
            partIds.Add(partId);
            continue;
          }

          _logger.Error($"There was an issue adding part: {part} to persister");
          continue;
        }

        // look for the id we just added.
        partId = await GetPartId(part, cmdSelectPart, connectionFactory, token).ConfigureAwait(false);
        if (partId == -1)
        {
          _logger.Error( $"There was an error finding the id of the part: {part}, we just added" );
          continue;
        }

        // we added it, so we can add it to our list
        partIds.Add(partId);
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
    private async Task<interfaces.IO.IWords> RebuildWordsListAsync(interfaces.IO.IWords words, IConnectionFactory connectionFactory, CancellationToken token)
    {
      try
      {
        if (0 == words.Count)
        {
          return new helper.IO.Words();
        }

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
        return new helper.IO.Words( actualWords );
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Building word list");
        throw;
      }
    }
    #endregion
  }
}
