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
    #region Member variables
    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;
    #endregion


    /// <inheritdoc />
    public string TableName => Tables.ParserWords;

    public SqlitePersisterParserWords(ILogger logger )
    {
      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<long> AddWordsAsync(
      long fileid, 
      IReadOnlyList<string> words,
      IWordsHelper wordsHelper,
      IFilesWordsHelper filesWordsHelper,
      IParserWordsHelper parserWordsHelper,
      IParserFilesWordsHelper parserFilesWordsHelper,
      CancellationToken token
    )
    {
      if (!words.Any())
      {
        return 0;
      }

      // make it a distinct list.
      var distinctWords = words.Distinct().ToList();

      long added = 0;
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
            wordsHelper,
            filesWordsHelper,
            token).ConfigureAwait(false))
          {
            // the word was not added to the parser table
            // we bypassed the entire process by adding the word id
            // and the file id to the files table.
            ++added;
            continue;
          }

          // try and add the word to the 
          var wordid = await InsertOrGetWord(word, parserWordsHelper, token).ConfigureAwait(false);
          if (-1 == wordid)
          {
            // the logging was in the Validate function.
            continue;
          }

          // we can now add that wordid/fileid 
          if( !await TryInsertWordAndFileId(wordid, fileid, parserFilesWordsHelper, token).ConfigureAwait(false))
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
    public async Task<bool> DeleteFileIds(long wordId, IList<long> fileIds, IParserFilesWordsHelper parserFilesWordsHelper, CancellationToken token)
    {
      try
      {
        var tasks = fileIds.Select(fileId => parserFilesWordsHelper.DeleteAsync(wordId, fileId, token)).ToList();
        await helper.Wait.WhenAll(tasks, _logger, token).ConfigureAwait(false);
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

    /// <inheritdoc />
    public async Task<bool> DeleteWordIds(IList<long> wordids, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      // first we delete all the 
      var sqlDelete = $"DELETE FROM {TableName} WHERE id=@id";
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

      using (var cmdSelectWord = CreateSelectWordsCommand( limit, connectionFactory ))
      using (var cmdSelectWordId = CreateSelectFileIdCommand(fileIdsLimit, connectionFactory))
      using (var cmdDeleteFileId = CreateDeleteFileIdCommand(fileIdsLimit, connectionFactory))
      {
        var pWordId = cmdSelectWordId.CreateParameter();
        pWordId.DbType = DbType.Int64;
        pWordId.ParameterName = "@wordid";
        cmdSelectWordId.Parameters.Add(pWordId);

        var pDeleteWordId = cmdDeleteFileId.CreateParameter();
        pDeleteWordId.DbType = DbType.Int64;
        pDeleteWordId.ParameterName = "@id";
        cmdDeleteFileId.Parameters.Add(pDeleteWordId);

        var parserWord = new List<IPendingParserWordsUpdate>((int)limit);
        try
        {
          using (var readerWord = await connectionFactory.ExecuteReadAsync(cmdSelectWord, token).ConfigureAwait(false))
          {
            while (readerWord.Read())
            {
              // get out if needed.
              token.ThrowIfCancellationRequested();

              // the word
              var id = (long)readerWord["id"];
              
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
                  fileIds.Add((long)readerFileIds["fileid"]);
                }

                if (!fileIds.Any())
                {
                  pDeleteWordId.Value = id;
                  await connectionFactory.ExecuteWriteAsync(cmdDeleteFileId, token).ConfigureAwait(false);
                  continue;
                }

                // get the word value.
                var word = (string)readerWord["word"];

                // add the word to the list.
                parserWord.Add(new PendingParserWordsUpdate(id, word, fileIds));
              }
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
    /// Create the SelectWordCommand
    /// </summary>
    /// <param name="limit"></param>
    /// <param name="connectionFactory"></param>
    /// <returns></returns>
    private static IDbCommand CreateDeleteFileIdCommand(long limit, IConnectionFactory connectionFactory)
    {
      var sql = $@"DELETE FROM {Tables.ParserWords}
                   WHERE
                     id IN
                   (
                     SELECT ID 
                     FROM   {Tables.ParserWords} 
                     WHERE 
                     id = @id AND
                     ID NOT IN (SELECT wordid FROM {Tables.ParserFilesWords})
                   )";

      // create the comment
      return connectionFactory.CreateCommand(sql);
    }

    /// <summary>
    /// Create the SelectWordCommand
    /// </summary>
    /// <param name="limit"></param>
    /// <param name="connectionFactory"></param>
    /// <returns></returns>
    private static IDbCommand CreateSelectFileIdCommand(long limit, IConnectionFactory connectionFactory)
    {
      // then we can look for the file ids.
      var sqlSelectWordId = $"SELECT fileid FROM {Tables.ParserFilesWords} where wordid=@wordid LIMIT {limit}";  //  make sure we don't get to many,.

      // create the comment
      return connectionFactory.CreateCommand(sqlSelectWordId);
    }

    /// <summary>
    /// Create the SelectWordCommand
    /// </summary>
    /// <param name="limit"></param>
    /// <param name="connectionFactory"></param>
    /// <returns></returns>
    private IDbCommand CreateSelectWordsCommand(long limit, IConnectionFactory connectionFactory)
    {
      // get the older words.
      var sqlSelectWord = $"SELECT id, word FROM {TableName} LIMIT {limit}";

      // create the comment
      return connectionFactory.CreateCommand(sqlSelectWord);
    }
    
    /// <summary>
    /// Try and insert a linked wordid/fileid
    /// </summary>
    /// <param name="wordId"></param>
    /// <param name="fileId"></param>
    /// <param name="parserFilesWordsHelper"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> TryInsertWordAndFileId(
      long wordId, 
      long fileId, 
      IParserFilesWordsHelper parserFilesWordsHelper, 
      CancellationToken token
    )
    {
      // try ans insert that id directly.
      // now that we have a fileid/wordid we can then try and insert the value 
      if (await parserFilesWordsHelper.InsertAsync(wordId, fileId, token).ConfigureAwait(false))
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
    /// <param name="filesWordsHelper"></param>
    /// <param name="wordsHelper"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> TryInsertFilesWords(
      long fileId,
      string word,
      IWordsHelper wordsHelper,
      IFilesWordsHelper filesWordsHelper,
      CancellationToken token)
    {
      // try and get the id, if it does not exist, we cannot add it.
      var wordId = await wordsHelper.GetIdAsync(word, token).ConfigureAwait(false);
      if (-1 == wordId)
      {
        return false;
      }

      // now that we have a fileid/wordid we can then try and insert the value 
      if (await filesWordsHelper.InsertAsync(wordId, fileId, token).ConfigureAwait(false))
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
    /// <param name="parserWordsHelper"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<long> InsertOrGetWord(
      string word,
      IParserWordsHelper parserWordsHelper,
      CancellationToken token)
    {
      var wordId = await parserWordsHelper.InsertAsync(word, token).ConfigureAwait(false);
      if (wordId != -1)
      {
        return wordId;
      }

      // we could not insert it ... or look for it.
      _logger.Error($"There was an issue adding word: {word} to persister");
      return -1;
    }
    #endregion
  }
}
