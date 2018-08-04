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

namespace myoddweb.desktopsearch.service.Persisters
{
  internal partial class SqlitePersister 
  {
    /// <inheritdoc />
    public async Task<bool> AddOrUpdateWordToFileAsync(IWord word, long fileId, IDbTransaction transaction, CancellationToken token)
    {
      return await AddOrUpdateWordsToFileAsync(new Words( word ), fileId, transaction, token ).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> AddOrUpdateWordsToFileAsync(Words words, long fileId, IDbTransaction transaction, CancellationToken token)
    {
      if (null == transaction)
      {
        throw new ArgumentNullException(nameof(transaction), "You have to be within a tansaction when calling this function.");
      }

      // get all the word ids, insert the words as needed ...
      var ids = await GetWordIdsAsync(words, transaction, token, true).ConfigureAwait(false);

      // before we go anywhere, we want to remove whatever is not in the list
      // of word ids we will be adding.
      if (!await RemoveIdsInvalidWordsInfFile(ids, fileId, transaction, token).ConfigureAwait(false))
      {
        return false;
      }

      try
      {
        // we then go around and look for the word/id, if it already exists then we do not add it again
        // otherwise we will go ahead and add it.
        var sqlInsert = $"INSERT INTO {TableFilesWords} (wordid, fileid) VALUES (@wordid, @fileid)";
        using (var cmdInsert = CreateDbCommand(sqlInsert, transaction))
        {
          // create the parameters for inserting.
          var pWId1 = cmdInsert.CreateParameter();
          pWId1.DbType = DbType.Int64;
          pWId1.ParameterName = "@wordid";
          cmdInsert.Parameters.Add(pWId1);

          var pFId1 = cmdInsert.CreateParameter();
          pFId1.DbType = DbType.Int64;
          pFId1.ParameterName = "@fileid";
          cmdInsert.Parameters.Add(pFId1);

          var sqlSelect = $"SELECT wordid FROM {TableFilesWords} WHERE wordid=@wordid AND fileid=@fileid";
          using (var cmdSelect = CreateDbCommand(sqlSelect, transaction))
          {
            // and the prameters for selecting.
            var pWId2 = cmdSelect.CreateParameter();
            pWId2.DbType = DbType.Int64;
            pWId2.ParameterName = "@wordid";
            cmdSelect.Parameters.Add(pWId2);

            var pFId2 = cmdSelect.CreateParameter();
            pFId2.DbType = DbType.Int64;
            pFId2.ParameterName = "@fileid";
            cmdSelect.Parameters.Add(pFId2);

            // we can now go ahead and add all the ids to the list.
            // we will first look for it, and if nothing is returned
            // then we assume it does not exist, and in that case we will add it.
            pFId1.Value = fileId;
            pFId2.Value = fileId;
            foreach (var id in ids)
            {
              pWId2.Value = id;

              // first look for that id.
              var value = await ExecuteScalarAsync(cmdSelect, token).ConfigureAwait(false);
              if (null != value && value != DBNull.Value)
              {
                // this value already exists ... no need to go further.
                continue;
              }

              // it does not eixst ... we have to add it.
              pWId1.Value = id;
              if ( 1 != await ExecuteNonQueryAsync(cmdInsert, token).ConfigureAwait(false))
              {
                _logger.Error( $"There was an issue inserting word : {id} for file : {fileId}");
              }
            }
          }// Command Select
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
        return false;
      }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteFileFromFilesAndWordsAsync(long fileId, IDbTransaction transaction, CancellationToken token)
    {
      try
      {
        var sqlDelete = $"DELETE FROM {TableFilesWords} WHERE fileid=@fileid";
        using (var cmd = CreateDbCommand(sqlDelete, transaction))
        {
          var pFId = cmd.CreateParameter();
          pFId.DbType = DbType.Int64;
          pFId.ParameterName = "@fileid";
          cmd.Parameters.Add(pFId);

          pFId.Value = fileId;

          // get out if needed.
          token.ThrowIfCancellationRequested();

          // if we delete nothing, (or get a zero), then something else could have gone wrong.
          if (await ExecuteNonQueryAsync(cmd, token).ConfigureAwait(false) <= 0 )
          {
            _logger.Verbose($"Could not delete file > word {fileId}, does it still exist? Did it have any words?");
          }
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
    /// <param name="ids"></param>
    /// <param name="fileId"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> RemoveIdsInvalidWordsInfFile(IReadOnlyList<long> ids, long fileId, IDbTransaction transaction, CancellationToken token)
    {
      // if we have nothing in the list then we are removeing everything?
      if (!ids.Any())
      {
        return await DeleteFileFromFilesAndWordsAsync(fileId, transaction, token).ConfigureAwait(false);
      }

      // get all the word ids on record.
      var currentIds = await GetWordIdsForFile(fileId, transaction, token).ConfigureAwait(false);

      // then remove all the ids that are in our _current_ list
      // but that are not in our given list.
      var diffIds = currentIds.Except(ids).ToList();

      // those are the id that we want to delete.
      if (!diffIds.Any())
      {
        // we have nothing to do
        // but it is no an error in itslef.
        return true;
      }

      try
      {
        var sqlDelete = $"DELETE FROM {TableFilesWords} WHERE wordid=@wordid and fileid=@fileid";
        using (var cmd = CreateDbCommand(sqlDelete, transaction))
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
          foreach (var diffId in diffIds)
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // set the word id we are getting rid off.
            pWId.Value = diffId;
            if (1 != await ExecuteNonQueryAsync(cmd, token).ConfigureAwait(false))
            {
              _logger.Warning($"Could not delete word : {diffId} for file : {fileId}, does it still exist?");
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
        return false;
      }
    }

    /// <summary>
    /// Get all the woord ids for a given file.
    /// </summary>
    /// <param name="fileId"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<List<long>> GetWordIdsForFile( long fileId, IDbTransaction transaction, CancellationToken token)
    {
      // first we get the ids that are in the 
      try
      {
        var sqlSelect = $"SELECT wordid FROM {TableFilesWords} WHERE fileid=@fileid";
        using (var cmd = CreateDbCommand(sqlSelect, transaction))
        {
          // and the prameters for selecting.
          var pFId = cmd.CreateParameter();
          pFId.DbType = DbType.Int64;
          pFId.ParameterName = "@fileid";
          cmd.Parameters.Add(pFId);

          // set the file id.
          pFId.Value = fileId;

          // the list of wordIds 
          var wordIds = new List<long>();

          //  execute the query itself.
          var reader = await ExecuteReaderAsync(cmd, token).ConfigureAwait(false);
          while (reader.Read())
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // add this id to the list.
            wordIds.Add((long) reader["wordid"]);
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
        return new List<long>();
      }
    }
    #endregion
  }
}
