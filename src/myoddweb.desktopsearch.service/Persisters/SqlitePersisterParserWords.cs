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

    /// <summary>
    /// Insert a parsed word.
    /// </summary>
    internal class InsertParserWordCommand : Command
    {
      public IDataParameter Word { get; }

      public InsertParserWordCommand(IConnectionFactory factory)
      {
        // try and insert the word into the table.
        // we will ignore duplicates and rather return the id.
        var sql = $"INSERT OR IGNORE INTO {Tables.ParserWords} (word) VALUES (@word)";
        Cmd = factory.CreateCommand(sql);

        Word = Cmd.CreateParameter();
        Word.DbType = DbType.String;
        Word.ParameterName = "@word";
        Cmd.Parameters.Add(Word);
      }
    }

    /// <summary>
    /// Look for a parsed word id.
    /// </summary>
    internal class SelectParserWordCommand : Command
    {
      public IDataParameter Word { get; }

      public SelectParserWordCommand(IConnectionFactory factory)
      {
        // look for the word id
        var sql = $"SELECT id FROM {Tables.ParserWords} where word=@word";
        Cmd = factory.CreateCommand(sql);

        Word = Cmd.CreateParameter();
        Word.DbType = DbType.String;
        Word.ParameterName = "@word";
        Cmd.Parameters.Add(Word);
      }
    }

    /// <summary>
    /// Insert a parsed file word id/fileid.
    /// </summary>
    internal class InsertParserFilesWordsCommand : Command
    {
      public IDataParameter WordId { get; }

      public IDataParameter FileId { get; }

      public InsertParserFilesWordsCommand(IConnectionFactory factory)
      {
        // try and insert the word into the table.
        // we will ignore duplicates and rather return the id.
        var sql = $"INSERT OR IGNORE INTO {Tables.ParserFilesWords} (wordid, fileid) VALUES (@wordid, @fileid)";
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

    /// <summary>
    /// Look for a parsed word id.
    /// </summary>
    internal class SelectParserFilesWordsCommand : Command
    {
      public IDataParameter WordId { get; }

      public IDataParameter FileId { get; }

      public SelectParserFilesWordsCommand(IConnectionFactory factory)
      {
        // look for the word id
        var sql = $"SELECT wordid FROM {Tables.ParserFilesWords} where wordid=@wordid and fileid=@fileid";
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

    /// <summary>
    /// Lookfor a file/word id.
    /// </summary>
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

    /// <summary>
    /// Insert data into the FilesWords table
    /// </summary>
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

    /// <summary>
    /// Look for an existing word id in the words id
    /// </summary>
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
    public async Task<long> AddWordsAsync(long fileid, IReadOnlyList<string> words, IConnectionFactory connectionFactory, CancellationToken token)
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
      using (var cmdInsertParserFilesWords = new InsertParserFilesWordsCommand(connectionFactory))  //  insert a wordid/fileid
      using (var cmdSelectParserFilesWords = new SelectParserFilesWordsCommand(connectionFactory))  //  look for theinserted wordid/fileid
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

            // try and add the word to the 
            var wordid = await InsertOrGetWord(word, connectionFactory, cmdInsertParserWord, cmdSelectParserWord, token).ConfigureAwait(false);
            if (-1 == wordid)
            {
              // the logging was in the Validate function.
              continue;
            }

            // we can now add that wordid/fileid 
            if( !await TryInsertWordAndFileId(wordid, fileid, connectionFactory, cmdInsertParserFilesWords, cmdSelectParserFilesWords, token).ConfigureAwait(false))
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
    public async Task<bool> DeleteFileId(long fileId, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      var sqlDeleteWordAndFileIds = $"DELETE FROM {Tables.ParserFilesWords} WHERE fileid=@fileid";
      using (var cmdDeleteWordAndFileIds = connectionFactory.CreateCommand(sqlDeleteWordAndFileIds))
      {
        var pDeleteFileId = cmdDeleteWordAndFileIds.CreateParameter();
        pDeleteFileId.DbType = DbType.Int64;
        pDeleteFileId.ParameterName = "@fileid";
        cmdDeleteWordAndFileIds.Parameters.Add(pDeleteFileId);

        try
        {
          // get out if needed.
          token.ThrowIfCancellationRequested();
          
          // then do the actual delete.
          pDeleteFileId.Value = fileId;
          if (0 == await connectionFactory.ExecuteWriteAsync(cmdDeleteWordAndFileIds, token).ConfigureAwait(false))
          {
            _logger.Verbose($"Could not delete parser words for file id: {fileId}, do they still exist? was there any?");
          }

          // then we can delete whatever word ids no longer have a file id attached to it.
          await DeleteWordIdsIfNoFileIds(connectionFactory, token).ConfigureAwait(false);

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
    public async Task<bool> DeleteFileIds(long wordId, IList<long> fileIds, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      var sqlDeleteWordAndFileIds = $"DELETE FROM {Tables.ParserFilesWords} WHERE wordid=@wordid AND fileid=@fileid";
      using (var cmdDeleteWordAndFileIds = connectionFactory.CreateCommand(sqlDeleteWordAndFileIds))
      {
        var pWordId = cmdDeleteWordAndFileIds.CreateParameter();
        pWordId.DbType = DbType.Int64;
        pWordId.ParameterName = "@wordid";
        cmdDeleteWordAndFileIds.Parameters.Add(pWordId);

        var pFileId = cmdDeleteWordAndFileIds.CreateParameter();
        pFileId.DbType = DbType.Int64;
        pFileId.ParameterName = "@fileid";
        cmdDeleteWordAndFileIds.Parameters.Add(pFileId);

        try
        {
          foreach (var fileId in fileIds)
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // then do the actual delete.
            pWordId.Value = wordId;
            pFileId.Value = fileId;
            if (0 == await connectionFactory.ExecuteWriteAsync(cmdDeleteWordAndFileIds, token).ConfigureAwait(false))
            {
              _logger.Verbose($"Could not delete parser words for file id: {fileId}, do they still exist? was there any?");
            }
          }

          // then we can delete whatever words are now complete.
          await DeleteWordIdsIfNoFileIds( wordId, connectionFactory, token).ConfigureAwait(false);

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

      // first we delete all the 
      var sqlDelete = $"DELETE FROM {Tables.ParserWords} WHERE id=@id";
      var sqlDeleteWordAndFileId = $"DELETE FROM {Tables.ParserFilesWords} WHERE wordid=@wordid";
      using (var cmdDeleteWord = connectionFactory.CreateCommand(sqlDelete))
      using (var cmdDeleteWordAndFile = connectionFactory.CreateCommand(sqlDeleteWordAndFileId))
      {
        var pIdWord = cmdDeleteWord.CreateParameter();
        pIdWord.DbType = DbType.Int64;
        pIdWord.ParameterName = "@id";
        cmdDeleteWord.Parameters.Add(pIdWord);

        var pIdWordAndFile = cmdDeleteWordAndFile.CreateParameter();
        pIdWordAndFile.DbType = DbType.Int64;
        pIdWordAndFile.ParameterName = "@wordid";
        cmdDeleteWordAndFile.Parameters.Add(pIdWordAndFile);
        
        try
        {
          foreach (var wordid in wordids)
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // delete the word itself.
            pIdWord.Value = wordid;
            if (0 == await connectionFactory.ExecuteWriteAsync(cmdDeleteWord, token).ConfigureAwait(false))
            {
              _logger.Warning($"Could not delete parser words, word id: {wordid}, does it still exist?");
              continue;
            }

            // then delete the words/files ids.
            pIdWordAndFile.Value = wordid;
            if (0 == await connectionFactory.ExecuteWriteAsync(cmdDeleteWordAndFile, token).ConfigureAwait(false))
            {
              _logger.Warning($"Could not delete parser files and words id for word id: {wordid}.");
            }
          }

          // remove any words that no longer have any file ids attached to it.
          await DeleteWordIdsIfNoFileIds(connectionFactory, token).ConfigureAwait(false);

          // we are done
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

      // get the files ids we can flag as proccessed.
      // we get a large number of them
      // but we do not let the user set the number so they don't set a crazy number.
      // we don't want to kill the process doing one simple/common word.
      const int fileIdsLimit = 10000;

      // get the older words.
      var sqlSelectWord = $"SELECT id, word FROM {Tables.ParserWords} pw ORDER BY id asc LIMIT {limit}";

      // then we can look for the file ids.
      var sqlSelectWordId = $"SELECT fileid FROM {Tables.ParserFilesWords} where wordid=@wordid LIMIT {fileIdsLimit}";  //  make sure we don't get to many,.
      using (var cmdSelectWord = connectionFactory.CreateCommand(sqlSelectWord))
      using (var cmdSelectWordId = connectionFactory.CreateCommand(sqlSelectWordId))
      {
        var pWordId = cmdSelectWordId.CreateParameter();
        pWordId.DbType = DbType.Int64;
        pWordId.ParameterName = "@wordid";
        cmdSelectWordId.Parameters.Add(pWordId);
        
        var parserWord = new List<IPendingParserWordsUpdate>((int)limit);
        try
        {
          var readerWord = await connectionFactory.ExecuteReadAsync(cmdSelectWord, token).ConfigureAwait(false);
          while (readerWord.Read())
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // the word
            var id = (long)readerWord["id"];
            var word = (string)readerWord["word"];

            // then look for some file ids.
            pWordId.Value = id;
            using (var readerFileIds = await connectionFactory.ExecuteReadAsync(cmdSelectWordId, token).ConfigureAwait(false))
            {
              var fileIds = new List<long>(fileIdsLimit);
              while (readerFileIds.Read())
              {
                // get out if needed.
                token.ThrowIfCancellationRequested();

                // add this item to the list of file ids.
                fileIds.Add( (long)readerFileIds["fileid"]);
              }

              if (!fileIds.Any())
              {
                continue;
              }
              // add the word to the list.
              parserWord.Add(new PendingParserWordsUpdate(id, word, fileIds));
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
    /// Delete all the words that do not have any files attached to it.
    /// </summary>
    /// <param name="wordid"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> DeleteWordIdsIfNoFileIds(long wordid, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      var sql = $@"DELETE FROM {Tables.ParserWords}
                   WHERE
                     id IN
                   (
                     SELECT ID 
                     FROM   {Tables.ParserWords} 
                     WHERE 
                     id = @id AND
                     ID NOT IN (SELECT wordid FROM {Tables.ParserFilesWords} pfw)
                   )";

      using (var cmd = connectionFactory.CreateCommand(sql))
      {
        try
        {
          var pId = cmd.CreateParameter();
          pId.DbType = DbType.Int64;
          pId.ParameterName = "@id";
          cmd.Parameters.Add(pId);

          // get out if needed.
          token.ThrowIfCancellationRequested();

          pId.Value = wordid;
          await connectionFactory.ExecuteWriteAsync(cmd, token).ConfigureAwait(false);

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

    /// <summary>
    /// Delete all the words that do not have any files attached to it.
    /// </summary>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> DeleteWordIdsIfNoFileIds(IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      var sql = $@"DELETE FROM {Tables.ParserWords}
                   WHERE
                     ID IN
                   (
                     SELECT ID 
                     FROM   {Tables.ParserWords} 
                     WHERE ID NOT IN (SELECT wordid FROM {Tables.ParserFilesWords} pfw)
                   )";

      using (var cmd = connectionFactory.CreateCommand(sql))
      {
        try
        {
          // get out if needed.
          token.ThrowIfCancellationRequested();

          await connectionFactory.ExecuteWriteAsync(cmd, token).ConfigureAwait(false);

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

    /// <summary>
    /// Try and insert a linked wordid/fileid
    /// </summary>
    /// <param name="wordId"></param>
    /// <param name="fileId"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="cmdInsertParserFilesWords"></param>
    /// <param name="cmdSelectParserFilesWords"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> TryInsertWordAndFileId(
      long wordId, 
      long fileId, 
      IConnectionFactory connectionFactory, 
      InsertParserFilesWordsCommand cmdInsertParserFilesWords, 
      SelectParserFilesWordsCommand cmdSelectParserFilesWords, 
      CancellationToken token
    )
    {
      // try ans insert that id directly.
      // now that we have a fileid/wordid we can then try and insert the value 
      cmdInsertParserFilesWords.FileId.Value = fileId;
      cmdInsertParserFilesWords.WordId.Value = wordId;
      if (1 == await connectionFactory.ExecuteWriteAsync(cmdInsertParserFilesWords.Cmd, token).ConfigureAwait(false))
      {
        // the word was added, we are done.
        return true;
      }

      // is it really a duplicate?
      // then lets look for it.
      cmdSelectParserFilesWords.FileId.Value = fileId;
      cmdSelectParserFilesWords.WordId.Value = wordId;
      var valueFileWord = await connectionFactory.ExecuteReadOneAsync(cmdSelectParserFilesWords.Cmd, token).ConfigureAwait(false);
      if (null != valueFileWord && valueFileWord != DBNull.Value)
      {
        return true;
      }

      // something did not work.
      _logger.Error($"There was an issue inserting word {wordId} for file : {fileId}");
      return false;
    }

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
    /// <param name="word"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="cmdInsert"></param>
    /// <param name="cmdSelect">If the insert fails we will use this command to make sure it was because the word already exists</param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<long> InsertOrGetWord(
      string word,
      IConnectionFactory connectionFactory,
      InsertParserWordCommand cmdInsert,
      SelectParserWordCommand cmdSelect,
      CancellationToken token)
    {
      // we will be selecting the word if.
      cmdSelect.Word.Value = word;

      // try and insert the word.
      // it does not matter if we get an error or not
      // we need to get the id for it.
      cmdInsert.Word.Value = word;
      await connectionFactory.ExecuteWriteAsync(cmdInsert.Cmd, token).ConfigureAwait(false);

      var value = await connectionFactory.ExecuteReadOneAsync(cmdSelect.Cmd, token).ConfigureAwait(false);
      if (null != value && value != DBNull.Value)
      {
        // we found the word...
        return (long)value;
      }

      // we could not insert it ... or look for it.
      _logger.Error($"There was an issue adding word: {word} to persister");
      return -1;
    }
    #endregion
  }
}
