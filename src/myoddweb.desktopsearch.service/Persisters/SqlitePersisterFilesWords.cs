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
using System.Threading;
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal partial class SqlitePersister 
  {
    /// <inheritdoc />
    public async Task<bool> AddOrUpdateWordToFileAsync(string word, long fileId, IDbTransaction transaction, CancellationToken token)
    {
      return await AddOrUpdateWordsToFileAsync(new [] { word }, fileId, transaction, token ).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> AddOrUpdateWordsToFileAsync(IReadOnlyList<string> words, long fileId, IDbTransaction transaction, CancellationToken token)
    {
      if (null == transaction)
      {
        throw new ArgumentNullException(nameof(transaction), "You have to be within a tansaction when calling this function.");
      }

      // get all the word ids, insert the words as needed ...
      var ids = await GetWordIdsAsync(words, transaction, token, true).ConfigureAwait(false);

      try
      {
        // we then go around and look for the word/id, if it already exists then we do not add it again
        // otherwise we will go ahead and add it.
        var sqlInsert = $"INSERT INTO {TableFilesWords} (wordid, fileid) VALUES (@wordid, @fileid)";
        using (var cmdInsert = CreateDbCommand(sqlInsert, transaction))
        {
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
            var pWId2 = cmdSelect.CreateParameter();
            pWId2.DbType = DbType.Int64;
            pWId2.ParameterName = "@wordid";
            cmdSelect.Parameters.Add(pWId2);

            var pFId2 = cmdInsert.CreateParameter();
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
          }
        }
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
      return true;
    }

    #region Private word functions
    #endregion
  }
}
