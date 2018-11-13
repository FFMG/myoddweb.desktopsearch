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
  internal class SqlitePersisterFilesWords : IFilesWords
  {
    #region Member variables
    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The words interface
    /// </summary>
    private readonly IWords _words;

    /// <summary>
    /// The parser words we are working with.
    /// </summary>
    private readonly IParserWords _parserWords;
    #endregion

    public SqlitePersisterFilesWords(IParserWords parserWords, IWords words, ILogger logger)
    {
      // the parsers word.
      _parserWords = parserWords ?? throw new ArgumentException(nameof(parserWords));

      // the words manager
      _words = words ?? throw new ArgumentNullException(nameof(words));

      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string TableName => Tables.FilesWords;

    /// <inheritdoc />
    public async Task<bool> AddParserWordsAsync(
      IPendingParserWordsUpdate pendingUpdate,
      IWordsHelper wordsHelper, 
      IFilesWordsHelper filesWordsHelper,
      IPartsHelper partsHelper,
      IWordsPartsHelper wordsPartsHelper,
      CancellationToken token)
    {
      // get some words from the parser so we can add them here now.
      if (pendingUpdate == null)
      {
        // there was nothing to add.
        return true;
      }

      try
      {
        // then do an inserts.
        // get the id for this word.
        var wordId = await _words.AddOrUpdateWordAsync(wordsHelper, partsHelper, wordsPartsHelper, pendingUpdate.Word, token ).ConfigureAwait(false);
        if (-1 == wordId)
        {
          _logger.Error($"There was an issue inserting/finding the word : {pendingUpdate.Word.Value}.");
          return false;
        }

        foreach (var fileId in pendingUpdate.FileIds)
        {
          if( !await filesWordsHelper.InsertAsync(wordId, fileId, token).ConfigureAwait(false))
          {
            _logger.Error( $"There was an issue inserting word : {pendingUpdate.Word.Value}({wordId}) for file : {fileId}");
          }
        }

        // we are done
        return true;
      }
      catch (OperationCanceledException)
      {
        _logger.Warning( "Received cancellation request - Add pending parser words");
        throw;
      }
      catch (Exception ex)
      {
        _logger.Exception("There was an exception adding parser words", ex);
        throw;
      }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteWordsAsync(long fileId, IConnectionFactory connectionFactory, CancellationToken token)
    {
      try
      {
        // remove all the words we might still need to parse
        await _parserWords.DeleteFileId(fileId, connectionFactory, token).ConfigureAwait(false);

        // then remove the ones we might have done already.
        var sqlDelete = $"DELETE FROM {TableName} WHERE fileid=@fileid";
        using (var cmd = connectionFactory.CreateCommand(sqlDelete))
        {
          var pFId = cmd.CreateParameter();
          pFId.DbType = DbType.Int64;
          pFId.ParameterName = "@fileid";
          cmd.Parameters.Add(pFId);

          pFId.Value = fileId;

          // get out if needed.
          token.ThrowIfCancellationRequested();

          // it is very posible that this file had no words at all
          // in fact most files are never parsed.
          // so we don't want to log a message for this.
          // if there is an error it will throw and this will be logged that way
          await connectionFactory.ExecuteWriteAsync(cmd, token).ConfigureAwait(false);
        }

        // all good.
        return true;
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Deleting File Id from Files/Word.");
        throw;
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        return false;
      }
    }
  }
}
