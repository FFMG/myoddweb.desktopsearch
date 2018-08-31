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
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal partial class SqlitePersister 
  {
    /// <inheritdoc />
    public async Task<bool> AddOrUpdateWordToFileAsync(Word word, long fileId, IConnectionFactory connectionFactory, CancellationToken token)
    {
      return await AddOrUpdateWordsToFileAsync(new Words( word ), fileId, connectionFactory, token ).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> AddOrUpdateWordsToFileAsync(Words words, long fileId, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      // get all the word ids, insert them into the word table if needed.
      // we will then add those words to the this file id.
      var wordids = await GetWordIdsAsync(words, connectionFactory, token, true).ConfigureAwait(false);

      // get all the current word ids already in that files.
      var currentIds = await GetWordIdsForFile(fileId, connectionFactory, token).ConfigureAwait(false);

      // all the ids returned are the ones in the file id.
      // if that file has any other words then they need to be removed.
      if (!await RemoveWordIdsThatShouldNotBeInFile(fileId, wordids, currentIds, connectionFactory, token).ConfigureAwait(false))
      {
        return false;
      }

      try
      {
        // finally we need to add all the words to the file that are not in the current file.
        var wordIdsToAdd = wordids.Except(currentIds).ToList();
        if (!wordIdsToAdd.Any())
        {
          return true;
        }

        var sql = $"INSERT INTO {TableFilesWords} (wordid, fileid) VALUES (@wordid, @fileid)";
        using (var cmd = connectionFactory.CreateCommand(sql))
        {
          // create the parameters for inserting.
          var pWId = cmd.CreateParameter();
          pWId.DbType = DbType.Int64;
          pWId.ParameterName = "@wordid";
          cmd.Parameters.Add(pWId);

          var pFId = cmd.CreateParameter();
          pFId.DbType = DbType.Int64;
          pFId.ParameterName = "@fileid";
          cmd.Parameters.Add(pFId);

          // it does not eixst ... we have to add it.
          pFId.Value = fileId;

          foreach (var id in wordIdsToAdd)
          {
            pWId.Value = id;
            if (1 != await ExecuteNonQueryAsync(cmd, token).ConfigureAwait(false))
            {
              _logger.Error($"There was an issue inserting word : {id} for file : {fileId}");
            }
          }
        }// Command Insert

        // we are done
        return true;
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Add words to file id");
        throw;
      }
      catch (Exception ex)
      {
        _logger.Exception(ex);
        throw;
      }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteFileFromFilesAndWordsAsync(long fileId, IConnectionFactory connectionFactory, CancellationToken token)
    {
      try
      {
        var sqlDelete = $"DELETE FROM {TableFilesWords} WHERE fileid=@fileid";
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
          await ExecuteNonQueryAsync(cmd, token).ConfigureAwait(false);
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

    #region Private word functions

    /// <summary>
    /// Remove the ids that were in the words list but are not anymore.
    /// </summary>
    /// <param name="fileId"></param>
    /// <param name="expectedWordIds">The ids we want to add</param>
    /// <param name="currentWordIds"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> RemoveWordIdsThatShouldNotBeInFile(long fileId, IReadOnlyCollection<long> expectedWordIds, IEnumerable<long> currentWordIds, IConnectionFactory connectionFactory, CancellationToken token)
    {
      // if we have nothing in the list of ids we want then we want to remove everything.
      if (!expectedWordIds.Any())
      {
        return await DeleteFileFromFilesAndWordsAsync(fileId, connectionFactory, token).ConfigureAwait(false);
      }

      // then remove all the ids that are in our _current_ list of words
      // but that are not in our given list.
      var wordIdsToRemove = currentWordIds.Except(expectedWordIds).ToArray();

      // those are the id that we want to delete.
      if (!wordIdsToRemove.Any())
      {
        // we have nothing to do
        // but it is no an error in itslef.
        return true;
      }

      try
      {
        // whatever word ids are left we need to remove them.
        var sqlDelete = $"DELETE FROM {TableFilesWords} WHERE wordid=@wordid and fileid=@fileid";
        using (var cmd = connectionFactory.CreateCommand(sqlDelete))
        {
          var pWId = cmd.CreateParameter();
          pWId.DbType = DbType.Int64;
          pWId.ParameterName = "@wordid";
          cmd.Parameters.Add(pWId);

          var pFId = cmd.CreateParameter();
          pFId.DbType = DbType.Int64;
          pFId.ParameterName = "@fileid";
          cmd.Parameters.Add(pFId);

          pFId.Value = fileId;
          foreach (var wordId in wordIdsToRemove)
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // set the word id we are getting rid off.
            pWId.Value = wordId;
            if (1 != await ExecuteNonQueryAsync(cmd, token).ConfigureAwait(false))
            {
              _logger.Warning($"Could not delete word : {wordId} for file : {fileId}, does it still exist?");
            }
          }
        }

        // all good.
        return true;
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Deleting extra word ids.");
        throw;
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        throw;
      }
    }

    /// <summary>
    /// Get all the woord ids for a given file.
    /// </summary>
    /// <param name="fileId"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<HashSet<long>> GetWordIdsForFile( long fileId, IConnectionFactory connectionFactory, CancellationToken token)
    {
      // first we get the ids that are in the 
      try
      {
        var sqlSelect = $"SELECT wordid FROM {TableFilesWords} WHERE fileid=@fileid";
        using (var cmd = connectionFactory.CreateCommand(sqlSelect))
        {
          // and the prameters for selecting.
          var pFId = cmd.CreateParameter();
          pFId.DbType = DbType.Int64;
          pFId.ParameterName = "@fileid";
          cmd.Parameters.Add(pFId);

          // set the file id.
          pFId.Value = fileId;

          // the list of wordIds 
          var wordIds = new HashSet<long>();

          //  execute the query itself.
          var reader = await ExecuteReaderAsync(cmd, token).ConfigureAwait(false);
          var ordinal = reader.GetOrdinal("wordid");
          while (reader.Read())
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // add this id to the list.
            wordIds.Add( reader.GetInt64(ordinal));
          }

          // return all the word ids that we found for this files.
          return wordIds;
        }
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Getting all the word ids for a file id.");
        throw;
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        return new HashSet<long>();
      }
    }
    #endregion
  }
}
