﻿//This file is part of Myoddweb.DesktopSearch.
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
using myoddweb.desktopsearch.helper.IO;
using myoddweb.desktopsearch.helper.Persisters;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal class SqlitePersisterParserWords : IParserWords
  {
    #region Member variables
    /// <summary>
    /// The maximum number of characters per words parts...
    /// This is not the max word lenght, but the part lenght
    /// The user cannot enter anything longer in a seatch box.
    /// So search queries longer than that... are ignored.
    /// </summary>
    private readonly int _maxNumCharactersPerParts;

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The words persister so we can check for valid sizes.
    /// </summary>
    private readonly IWords _words;
    #endregion

    /// <inheritdoc />
    public string TableName => Tables.ParserWords;

    public SqlitePersisterParserWords(IWords words, int maxNumCharactersPerParts, ILogger logger )
    {
      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      // the words persiser.
      _words = words ?? throw new ArgumentNullException(nameof(words));

      // the number of characters per parts.
      _maxNumCharactersPerParts = maxNumCharactersPerParts;
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

          if (!_words.IsValidWord( new Word(word, _maxNumCharactersPerParts)))
          {
            _logger.Verbose( $"Did not insert word {word} as it is not valid.");
            continue;
          }
          
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
      using (var parserWordHelper = new ParserWordsHelper(connectionFactory, TableName))
      using (var parserFilesWordsHelper = new ParserFilesWordsHelper(connectionFactory, Tables.ParserFilesWords))
      {
        try
        {
          foreach (var wordid in wordids)
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // delete the word itself.
            if (!await parserWordHelper.DeleteWordAsync(wordid, token).ConfigureAwait(false))
            {
              _logger.Warning($"Could not delete parser words, word id: {wordid}, does it still exist?");
              continue;
            }

            // then delete the words/files ids.
            if (0 == await parserFilesWordsHelper.DeleteWordAsync(wordid, token).ConfigureAwait(false))
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
    public async Task<IList<IPendingParserWordsUpdate>> GetPendingParserWordsForFileIdUpdatesAsync
    (
      long limit,
      long fileId, 
      IParserWordsHelper parserWordsHelper,
      IParserFilesWordsHelper parserFilesWordsHelper,
      CancellationToken token)
    {
      var parserWord = new List<IPendingParserWordsUpdate>((int)limit);

      // get all the word ids.
      var ids = await parserFilesWordsHelper.GetWordIdsAsync(fileId, token).ConfigureAwait(false);
      foreach (var id in ids)
      {
        // get the file ids, there is an outside chance that this 
        // file id does not exist anymore
        // in case another parser takes it over.
        var fileIds = await parserFilesWordsHelper.GetFileIdsAsync(id, token).ConfigureAwait(false);
        if (!fileIds.Any())
        {
          continue;
        }

        // look for that word and add it to the list.
        var word = await parserWordsHelper.GetWordAsync(id, token).ConfigureAwait(false);
        if (word == null)
        {
          continue;
        }

        // add the word to the list.
        parserWord.Add(new PendingParserWordsUpdate(id, new Word(word, _maxNumCharactersPerParts), fileIds ));
        if (parserWord.Count >= limit)
        {
          break;
        }
      }
      return parserWord;
    }

    /// <inheritdoc />
    public async Task<IList<IPendingParserWordsUpdate>> GetPendingParserWordsUpdatesAsync(long limit, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      using (var cmdSelectWord = CreateSelectWordsCommand( connectionFactory ))
      using (var parserFilesWordsHelper = new ParserFilesWordsHelper(connectionFactory, Tables.ParserFilesWords))
      using (var cmdDeleteFileId = CreateDeleteFileIdCommand(connectionFactory))
      {
        var pLimit = cmdSelectWord.CreateParameter();
        pLimit.DbType = DbType.Int64;
        pLimit.ParameterName = "@limit";
        cmdSelectWord.Parameters.Add(pLimit);

        var pDeleteWordId = cmdDeleteFileId.CreateParameter();
        pDeleteWordId.DbType = DbType.Int64;
        pDeleteWordId.ParameterName = "@id";
        cmdDeleteFileId.Parameters.Add(pDeleteWordId);

        var parserWord = new List<IPendingParserWordsUpdate>((int)limit);
        try
        {
          var numberOfUpdatesToGet = limit;
          while (numberOfUpdatesToGet > 0)
          {
            // set the limit
            pLimit.Value = numberOfUpdatesToGet;

            // and assume we have nothing else to get.
            numberOfUpdatesToGet = 0;
            using (var readerWord = await connectionFactory.ExecuteReadAsync(cmdSelectWord, token).ConfigureAwait(false))
            {
              var idPos = readerWord.GetOrdinal("id");
              var wordPos = readerWord.GetOrdinal("word"); 
              while (readerWord.Read())
              {
                // get out if needed.
                token.ThrowIfCancellationRequested();

                // the word
                var id = (long)readerWord[idPos];

                // look for some file ids
                var fileIds = await parserFilesWordsHelper.GetFileIdsAsync(id, token ).ConfigureAwait(false);
                if (!fileIds.Any())
                {
                  // this word does not have any 'attached' files anymore
                  // so we want to delete it so it does not get picked up again.
                  pDeleteWordId.Value = id;
                  await connectionFactory.ExecuteWriteAsync(cmdDeleteFileId, token).ConfigureAwait(false);
                  ++numberOfUpdatesToGet;
                  continue;
                }

                // get the word value.
                var word = (string)readerWord[wordPos];

                // add the word to the list.
                parserWord.Add(new PendingParserWordsUpdate(id, new Word(word, _maxNumCharactersPerParts), fileIds));
              }
            }

            // go around again if we need to get more data.
            // if we do not need to get any more we will
            // break out of the while loop.
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
    /// <param name="connectionFactory"></param>
    /// <returns></returns>
    private static IDbCommand CreateDeleteFileIdCommand(IConnectionFactory connectionFactory)
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
    /// <param name="connectionFactory"></param>
    /// <returns></returns>
    private IDbCommand CreateSelectWordsCommand(IConnectionFactory connectionFactory)
    {
      // get the older words.
      var sqlSelectWord = $"SELECT id, word FROM {TableName} ORDER BY len DESC LIMIT @limit";

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
