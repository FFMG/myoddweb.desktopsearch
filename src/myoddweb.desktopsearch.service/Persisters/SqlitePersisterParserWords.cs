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
      var sqlSelect = $"SELECT id FROM {Tables.ParserWords} where fileid=@fileid AND word=@word";
      var sqlInsert = $"INSERT OR IGNORE INTO {Tables.ParserWords} (fileid, word) VALUES (@fileid, @word)";
      using (var cmdInsert = connectionFactory.CreateCommand(sqlInsert))
      using (var cmdSelect = connectionFactory.CreateCommand(sqlSelect))
      {
        var pSFileId = cmdSelect.CreateParameter();
        pSFileId.DbType = DbType.Int64;
        pSFileId.ParameterName = "@fileid";
        cmdSelect.Parameters.Add(pSFileId);

        var pSWord = cmdSelect.CreateParameter();
        pSWord.DbType = DbType.String;
        pSWord.ParameterName = "@word";
        cmdSelect.Parameters.Add(pSWord);

        var pIFileId = cmdInsert.CreateParameter();
        pIFileId.DbType = DbType.Int64;
        pIFileId.ParameterName = "@fileid";
        cmdInsert.Parameters.Add(pIFileId);

        var pIWord = cmdInsert.CreateParameter();
        pIWord.DbType = DbType.String;
        pIWord.ParameterName = "@word";
        cmdInsert.Parameters.Add(pIWord);

        foreach (var word in distinctWords)
        {
          try
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            pIFileId.Value = fileid;
            pIWord.Value = word;
            if (0 == await connectionFactory.ExecuteWriteAsync(cmdInsert, token).ConfigureAwait(false))
            {
              // maybe this word exist already
              pSFileId.Value = fileid;
              pSWord.Value = word;
              var value = await connectionFactory.ExecuteReadOneAsync(cmdSelect, token).ConfigureAwait(false);
              if (null == value || value == DBNull.Value)
              {
                _logger.Error($"There was an issue adding word: {word} for fileid: {fileid} to persister");
              }
              continue;
            }

            // word was added
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

      var sqlSelectWordId = $"SELECT id, fileid FROM {Tables.ParserWords} where word=@word";
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
  }
}
