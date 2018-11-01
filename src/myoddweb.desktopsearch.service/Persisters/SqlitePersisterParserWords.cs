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
using myoddweb.desktopsearch.helper.IO;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;
using IWords = myoddweb.desktopsearch.interfaces.IO.IWords;

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
      var sqlInsertWord = $"INSERT INTO {Tables.ParserWords} (fileid, word) VALUES (@fileid, @word)";
      using (var cmd = connectionFactory.CreateCommand(sqlInsertWord))
      {
        var pFileId = cmd.CreateParameter();
        pFileId.DbType = DbType.Int64;
        pFileId.ParameterName = "@fileid";
        cmd.Parameters.Add(pFileId);

        var pWord = cmd.CreateParameter();
        pWord.DbType = DbType.String;
        pWord.ParameterName = "@word";
        cmd.Parameters.Add(pWord);

        foreach (var word in distinctWords)
        {
          // does it exist already?
          var idNow = await GetWordAsync(fileid, word, connectionFactory, token).ConfigureAwait(false);
          if (idNow != -1)
          {
            continue;
          }

          try
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            pFileId.Value = fileid;
            pWord.Value = word;
            if (0 == await connectionFactory.ExecuteWriteAsync(cmd, token).ConfigureAwait(false))
            {
              _logger.Error($"There was an issue adding word: {word} for fileid: {fileid} to persister");
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
    public async Task<bool> DeleteWordIdFileId(long wordid, IConnectionFactory connectionFactory, CancellationToken token)
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
          // get out if needed.
          token.ThrowIfCancellationRequested();

          // then do the actual delete.
          pId.Value = wordid;
          if (0 == await connectionFactory.ExecuteWriteAsync(cmd, token).ConfigureAwait(false))
          {
            _logger.Warning($"Could not delete parser words, word id: {wordid}, does it still exist?");
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

      var sql = $"SELECT id, fileid, word FROM {Tables.ParserWords} order by fileid desc LIMIT {limit}";
      using (var cmd = connectionFactory.CreateCommand(sql))
      {
        var parserWord = new List<IPendingParserWordsUpdate>( (int)limit);
        try
        {
          var reader = await connectionFactory.ExecuteReadAsync(cmd, token).ConfigureAwait(false);
          while (reader.Read())
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // add the word to the list.
            parserWord.Add( new PendingParserWordsUpdate((long)reader["id"], (long)reader["fileid"], (string)reader["word"] ));
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
    /// Get the word id of a word for a file, if it exists.
    /// </summary>
    /// <param name="fileid"></param>
    /// <param name="word"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<long> GetWordAsync(long fileid, string word, IConnectionFactory connectionFactory, CancellationToken token)
    {
      try
      {
        // we first look for it, and, if we find it then there is nothing to do.
        var sql = $"SELECT id from {Tables.ParserWords} WHERE fileid=@fileid and word=@word;";
        using (var cmd = connectionFactory.CreateCommand(sql))
        {
          // get out if needed.
          token.ThrowIfCancellationRequested();

          var pFileId = cmd.CreateParameter();
          pFileId.DbType = DbType.Int64;
          pFileId.ParameterName = "@fileid";
          cmd.Parameters.Add(pFileId);

          var pWord = cmd.CreateParameter();
          pWord.DbType = DbType.String;
          pWord.ParameterName = "@word";
          cmd.Parameters.Add(pWord);

          pFileId.Value = fileid;
          pWord.Value = word;

          var value = await connectionFactory.ExecuteReadOneAsync(cmd, token).ConfigureAwait(false);
          if (null == value || value == DBNull.Value)
          {
            return -1;
          }

          // return the file id.
          return (long)value;
        }
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Get Word id");
        throw;
      }
    }
    #endregion
  }
}
