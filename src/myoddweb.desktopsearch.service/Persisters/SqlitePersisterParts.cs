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

namespace myoddweb.desktopsearch.service.Persisters
{
  internal partial class SqlitePersister
  {
    /// <inheritdoc />
    public async Task<HashSet<long>> AddOrUpdateParts(HashSet<string> parts, IDbTransaction transaction, CancellationToken token)
    {
      if (null == transaction)
      {
        throw new ArgumentNullException(nameof(transaction),
          "You have to be within a tansaction when calling this function.");
      }

      // the ids of all the parts, (added or otherwise).
      var partIds = new HashSet<long>();

      // if we have not words... then move on.
      if (!parts.Any())
      {
        return partIds;
      }

      // get the next id, we only get the next id when actually needed.
      long nextId = -1;

      try
      {
        // the query to insert a new word
        var sqlInsert = $"INSERT INTO {TableParts} (id, part) VALUES (@id, @part)";
        var sqlSelect = $"SELECT id FROM {TableParts} WHERE part = @part";
        using (var cmdInsert = CreateDbCommand(sqlInsert, transaction))
        {
          var pId = cmdInsert.CreateParameter();
          pId.DbType = DbType.Int64;
          pId.ParameterName = "@id";
          cmdInsert.Parameters.Add(pId);

          var pIPart = cmdInsert.CreateParameter();
          pIPart.DbType = DbType.String;
          pIPart.ParameterName = "@part";
          cmdInsert.Parameters.Add(pIPart);

          // then create the select command
          using (var cmdSelect = CreateDbCommand(sqlSelect, transaction))
          {
            var pSPart = cmdInsert.CreateParameter();
            pSPart.DbType = DbType.String;
            pSPart.ParameterName = "@part";
            cmdSelect.Parameters.Add(pSPart);

            // we can now go around first looking for the part
            // if we find it, then we can add it to the list of ids.
            // if we do not find it, we will add it and move the ids along
            foreach (var part in parts)
            {
              // get out if needed.
              token.ThrowIfCancellationRequested();

              pSPart.Value = part;

              var value = await ExecuteScalarAsync(cmdSelect, token).ConfigureAwait(false);
              if (null != value && value != DBNull.Value)
              {
                partIds.Add((long) value);
                continue;
              }

              // this part does not exist
              // so we must now add it.
              if (-1 == nextId)
              {
                nextId = await GetNextPartIdAsync(transaction, token).ConfigureAwait(false);
              }
              pId.Value = nextId;
              pIPart.Value = part;
              if (0 == await ExecuteNonQueryAsync(cmdInsert, token).ConfigureAwait(false))
              {
                _logger.Error($"There was an issue adding part: {part} to persister");
                continue;
              }

              // we added it, so we can add it to our list
              partIds.Add(nextId);

              // and move on to the next id.
              ++nextId;
            }

            // return all the ids we added.
            return partIds;
          }
        }
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Inserting parts");
        throw;
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        throw;
      }
    }

    #region Private parts function
    /// <summary>
    /// Get the next row ID we can use.
    /// </summary>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<long> GetNextPartIdAsync(IDbTransaction transaction, CancellationToken token)
    {
      try
      {
        // we first look for it, and, if we find it then there is nothing to do.
        var sqlNextRowId = $"SELECT max(id) from {TableParts};";
        using (var cmd = CreateDbCommand(sqlNextRowId, transaction))
        {
          var value = await ExecuteScalarAsync(cmd, token).ConfigureAwait(false);

          // get out if needed.
          token.ThrowIfCancellationRequested();

          // does not exist ...
          if (null == value || value == DBNull.Value)
          {
            return 0;
          }

          // this is the next counter.
          return ((long) value) + 1;
        }
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Get Next valid Part id");
        throw;
      }

      #endregion
    }
  }
}
