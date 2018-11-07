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
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal class SqlitePersisterParserWords : IParserWords
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

    internal class InsertParserWordCommand : Command
    {
      public IDataParameter Word { get; }

      public IDataParameter FileId { get; }

      public InsertParserWordCommand(IConnectionFactory factory)
      {
        var sql = $"INSERT OR IGNORE INTO {Tables.ParserWords} (fileid, word) VALUES (@fileid, @word)";
        Cmd = factory.CreateCommand(sql);

        FileId = Cmd.CreateParameter();
        FileId.DbType = DbType.Int64;
        FileId.ParameterName = "@fileid";
        Cmd.Parameters.Add(FileId);

        Word = Cmd.CreateParameter();
        Word.DbType = DbType.String;
        Word.ParameterName = "@word";
        Cmd.Parameters.Add(Word);
      }
    }

    internal class SelectParserWordCommand : Command
    {
      public IDataParameter Word { get; }

      public IDataParameter FileId { get; }

      public SelectParserWordCommand(IConnectionFactory factory)
      {
        var sql = $"SELECT id FROM {Tables.ParserWords} where fileid=@fileid AND word=@word";
        Cmd = factory.CreateCommand(sql);

        FileId = Cmd.CreateParameter();
        FileId.DbType = DbType.Int64;
        FileId.ParameterName = "@fileid";
        Cmd.Parameters.Add(FileId);

        Word = Cmd.CreateParameter();
        Word.DbType = DbType.String;
        Word.ParameterName = "@word";
        Cmd.Parameters.Add(Word);
      }
    }

    internal class SelectFilesWordsCommand : Command
    {
      public IDataParameter WordId { get; }

      public IDataParameter FileId { get; }

      public SelectFilesWordsCommand(IConnectionFactory factory)
      {
        var sql = $"SELECT wordid FROM {Tables.FilesWords} where wordid=@wordid AND fileid=@fileid";
        Cmd = factory.CreateCommand(sql);
        WordId = Cmd.CreateParameter();
        WordId.DbType = DbType.Int64;
        WordId.ParameterName = "@wordid";
        Cmd.Parameters.Add(WordId);

        FileId = Cmd.CreateParameter();
        FileId.DbType = DbType.Int64;
        FileId.ParameterName = "@fileid";
        Cmd.Parameters.Add(FileId);
      }
    }

    internal class InsertFilesWordsCommand : Command
    {
      public IDataParameter WordId { get; }

      public IDataParameter FileId { get; }

      public InsertFilesWordsCommand(IConnectionFactory factory)
      {
        var sql = $"INSERT OR IGNORE INTO {Tables.FilesWords} (wordid, fileid) VALUES (@wordid, @fileid)";
        Cmd = factory.CreateCommand(sql);
        WordId = Cmd.CreateParameter();
        WordId.DbType = DbType.Int64;
        WordId.ParameterName = "@wordid";
        Cmd.Parameters.Add(WordId);

        FileId = Cmd.CreateParameter();
        FileId.DbType = DbType.Int64;
        FileId.ParameterName = "@fileid";
        Cmd.Parameters.Add(FileId);
      }
    }

    internal class SelectWordCommand : Command
    {
      public IDataParameter Word { get; }

      public SelectWordCommand(IConnectionFactory factory)
      {
        var sql = $"SELECT id FROM {Tables.Words} WHERE word = @word";
        Cmd = factory.CreateCommand(sql);
        Word = Cmd.CreateParameter();
        Word.DbType = DbType.String;
        Word.ParameterName = "@word";
        Cmd.Parameters.Add(Word);
      }
    }
    #endregion 

    #region Member variables
    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;
    #endregion

    public SqlitePersisterParserWords(ILogger logger )
    {
      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<long> AddWordAsync(long fileid, IReadOnlyList<string> words, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (!words.Any())
      {
        return 0;
      }
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      // make it a distinct list.
      var distinctWords = words.Distinct().ToList();

      long added = 0;
      using (var cmdInsertFilesWords = new InsertFilesWordsCommand(connectionFactory))  //  insert the word/fileid if the word has been processed already
      using (var cmdSelectFilesWords = new SelectFilesWordsCommand(connectionFactory))  //  look for the word to make sure it was added properly
      using (var cmdSelectWordWord = new SelectWordCommand(connectionFactory))              //  look for the word
      using (var cmdInsertParserWord = new InsertParserWordCommand(connectionFactory))  //  insert the word in parsed word
      using (var cmdSelectParserWord = new SelectParserWordCommand(connectionFactory))  //  make sure that the parsed word was added properly.
      {
        foreach (var word in distinctWords)
        {
          try
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // if the word already exists in the Words table
            // then we do not need to add it anymore
            // we just need to add it to the FilesWords Table.
            if (await TryInsertFilesWords(
              fileid, 
              word, 
              connectionFactory, 
              cmdInsertFilesWords, 
              cmdSelectFilesWords,
              cmdSelectWordWord, 
              token).ConfigureAwait(false))
            {
              // the word was not added to the parser table
              // we bypassed the entire process by adding the word id
              // and the file id to the files table.
              ++added;
              continue;
            }

            if (!await TryInsertPartWord(fileid, word, connectionFactory, cmdInsertParserWord, cmdSelectParserWord, token).ConfigureAwait(false))
            {
              // the logging was in the Validate function.
              continue;
            }

            // word was added, (or it already existed).
            ++added;
          }
          catch (OperationCanceledException)
          {
            _logger.Warning("Received cancellation request - Insert Parser word.");
            throw;
          }
          catch (Exception ex)
          {
            _logger.Exception(ex);
            return 0;
          }
        }
      }

      // succcess it worked.
      return added;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteFileId(long fileid, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      var sqlDelete = $"DELETE FROM {Tables.ParserWords} WHERE fileid=@fileid";
      using (var cmd = connectionFactory.CreateCommand(sqlDelete))
      {
        var pFileId = cmd.CreateParameter();
        pFileId.DbType = DbType.Int64;
        pFileId.ParameterName = "@fileid";
        cmd.Parameters.Add(pFileId);

        try
        {
          // get out if needed.
          token.ThrowIfCancellationRequested();

          // then do the actual delete.
          pFileId.Value = fileid;
          if (0 == await connectionFactory.ExecuteWriteAsync(cmd, token).ConfigureAwait(false))
          {
            _logger.Verbose($"Could not delete parser words for file id: {fileid}, do they still exist? was there any?");
          }

          // we are done.
          return true;
        }
        catch (OperationCanceledException)
        {
          _logger.Warning("Received cancellation request - Parser words - Delete file");
          throw;
        }
        catch (Exception ex)
        {
          _logger.Exception(ex);
          return false;
        }
      }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteWordIds(IList<long> wordids, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      var sqlDelete = $"DELETE FROM {Tables.ParserWords} WHERE id=@id";
      using (var cmd = connectionFactory.CreateCommand(sqlDelete))
      {
        var pId = cmd.CreateParameter();
        pId.DbType = DbType.Int64;
        pId.ParameterName = "@id";
        cmd.Parameters.Add(pId);

        try
        {
          foreach (var wordid in wordids)
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // then do the actual delete.
            pId.Value = wordid;
            if (0 == await connectionFactory.ExecuteWriteAsync(cmd, token).ConfigureAwait(false))
            {
              _logger.Warning($"Could not delete parser words, word id: {wordid}, does it still exist?");
            }
          }
          // we are done.
          return true;
        }
        catch (OperationCanceledException)
        {
          _logger.Warning("Received cancellation request - Parser words - Delete word");
          throw;
        }
        catch (Exception ex)
        {
          _logger.Exception(ex);
          return false;
        }
      }
    }

    /// <inheritdoc />
    public async Task<IList<IPendingParserWordsUpdate>> GetPendingParserWordsUpdatesAsync(long limit, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      // get all the words that match this id.
      var sqlSelectWordId = $"SELECT id, fileid FROM {Tables.ParserWords} where word=@word LIMIT {20*limit}";
      var sqlSelectWord = $"SELECT DISTINCT word FROM {Tables.ParserWords} order by fileid asc LIMIT {limit}";
      using (var cmdSelectWord = connectionFactory.CreateCommand(sqlSelectWord))
      using (var cmdSelectWordId = connectionFactory.CreateCommand(sqlSelectWordId))
      {
        var pWord = cmdSelectWordId.CreateParameter();
        pWord.DbType = DbType.String;
        pWord.ParameterName = "@word";
        cmdSelectWordId.Parameters.Add(pWord);

        var parserWord = new List<IPendingParserWordsUpdate>((int)limit);
        try
        {
          var reader = await connectionFactory.ExecuteReadAsync(cmdSelectWord, token).ConfigureAwait(false);
          while (reader.Read())
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // the word
            var word = (string) reader["word"];

            var wordIdAndFileId = new Dictionary<long, long>();
            pWord.Value = word;
            using (var reader2 = await connectionFactory.ExecuteReadAsync(cmdSelectWordId, token).ConfigureAwait(false))
            {
              while (reader2.Read())
              {
                // get out if needed.
                token.ThrowIfCancellationRequested();

                wordIdAndFileId.Add((long) reader2["id"], (long) reader2["fileid"]);
              }

              // add the word to the list.
              parserWord.Add(new PendingParserWordsUpdate(word, wordIdAndFileId));
            }
          }
          return parserWord;
        }
        catch (OperationCanceledException)
        {
          _logger.Warning("Received cancellation request - Parser words - Delete word");
          throw;
        }
        catch (Exception ex)
        {
          _logger.Exception(ex);
          return null;
        }
      }
    }

    #region Private functions
    /// <summary>
    /// Try and insert the word inf the FilesWords table if the word has been parsed already
    /// If it has already been parsed, then there is no need to add it again.
    /// </summary>
    /// <param name="fileId"></param>
    /// <param name="word"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="cmdInsertFilesWords"></param>
    /// <param name="cmdSelectFilesWords"></param>
    /// <param name="cmdSelectWordWord"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> TryInsertFilesWords(
      long fileId,
      string word,
      IConnectionFactory connectionFactory,
      InsertFilesWordsCommand cmdInsertFilesWords,
      SelectFilesWordsCommand cmdSelectFilesWords,
      SelectWordCommand cmdSelectWordWord,
      CancellationToken token)
    {
      // we are first going to look for that id
      // if it does not exist, then we cannot update the files table.
      cmdSelectWordWord.Word.Value = word;
      var value = await connectionFactory.ExecuteReadOneAsync(cmdSelectWordWord.Cmd, token).ConfigureAwait(false);
      if (null == value || value == DBNull.Value)
      {
        // the word does not exist
        // so we cannot really go any further here.
        return false;
      }

      // the word that we have already parsed.
      var wordId = (long)value;

      // now that we have a fileid/wordid we can then try and insert the value 
      cmdInsertFilesWords.FileId.Value = fileId;
      cmdInsertFilesWords.WordId.Value = wordId;
      if (1 == await connectionFactory.ExecuteWriteAsync(cmdInsertFilesWords.Cmd, token).ConfigureAwait(false))
      {
        // the word was added
        return true;
      }

      // we were not able to add it, it is posible that it is because the word
      // and file already exists in the table.
      // we just want to double check that all is good.
      // we did not insert it... could it be because it exists already?
      cmdSelectFilesWords.FileId.Value = fileId;
      cmdSelectFilesWords.WordId.Value = wordId;
      var valueFileWord = await connectionFactory.ExecuteReadOneAsync(cmdSelectFilesWords.Cmd, token).ConfigureAwait(false);
      if (null != valueFileWord && valueFileWord != DBNull.Value)
      {
        return true;
      }

      // something did not work.
      _logger.Error($"There was an issue inserting word : {word}({wordId}) for file : {fileId}");
      return false;
    }

    /// <summary>
    /// Try and insert the word to the parser table
    /// If we have an error we will check that the word already exists, (ON IGNORE)
    /// </summary>
    /// <param name="fileId"></param>
    /// <param name="word"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="cmdInsert"></param>
    /// <param name="cmdSelect">If the insert fails we will use this command to make sure it was because the word already exists</param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> TryInsertPartWord(
      long fileId,
      string word,
      IConnectionFactory connectionFactory,
      InsertParserWordCommand cmdInsert,
      SelectParserWordCommand cmdSelect,
      CancellationToken token)
    {
      cmdInsert.FileId.Value = fileId;
      cmdInsert.Word.Value = word;
      if (1 == await connectionFactory.ExecuteWriteAsync(cmdInsert.Cmd, token).ConfigureAwait(false))
      {
        // the word was inserted!
        return true;
      }

      // looks ike the word _might_ already exist.
      // so we will double check.
      cmdSelect.FileId.Value = fileId;
      cmdSelect.Word.Value = word;
      var value = await connectionFactory.ExecuteReadOneAsync(cmdSelect.Cmd, token).ConfigureAwait(false);
      if (null != value && value != DBNull.Value)
      {
        // the word already exist, so it is the same as having added it.
        // this means that another thread was able to add the word.
        return true;
      }

      _logger.Error($"There was an issue adding word: {word} for fileid: {fileId} to persister");
      return false;
    }
    #endregion

  }
}
