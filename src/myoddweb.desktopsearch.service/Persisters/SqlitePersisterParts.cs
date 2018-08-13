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
    public async Task<bool> AddOrUpdatePartsAsync(HashSet<string> parts, IDbTransaction transaction, CancellationToken token)
    {
      if (null == transaction)
      {
        throw new ArgumentNullException(nameof(transaction),
          "You have to be within a tansaction when calling this function.");
      }

      // rebuild the list of directory with only those that need to be inserted.
      await InsertPartsAsync(
        await RebuildPartsListAsync(parts, transaction, token).ConfigureAwait(false),
        transaction, token).ConfigureAwait(false);
      return true;
    }

    #region Private parts function
    /// <summary>
    /// Insert parts and return the id of the added parts.
    /// </summary>
    /// <param name="parts"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<HashSet<long>> InsertPartsAsync(HashSet<string> parts, IDbTransaction transaction, CancellationToken token)
    {
      if (!parts.Any())
      {
        return new HashSet<long>();
      }

      // the ids of all the parts inserted.
      var partIds = new HashSet<long>();

      // get the next valid id.
      var nextId = await GetNextPartIdAsync(transaction, token).ConfigureAwait(false);

      try
      {
        // whatever is now left is to be inserted
        var sqlInsert = $"INSERT INTO {TableParts} (id, part) VALUES (@id, @part)";
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

          foreach (var part in parts)
          {
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

    /// <summary>
    /// Given a list of directories, re-create the ones that we need to insert.
    /// </summary>
    /// <param name="parts"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<HashSet<string>> RebuildPartsListAsync(HashSet<string> parts, IDbTransaction transaction, CancellationToken token)
    {
      if (!parts.Any())
      {
        return new HashSet<string>();
      }

      try
      {
        // the actual list of parts we need to add.
        var actualParts = new HashSet<string>();

        // first look for what we have and insert what we do not have.
        var sql = $"SELECT id FROM {TableParts} WHERE part = @part";
        using (var cmd = CreateDbCommand(sql, transaction))
        {
          var pSPart = cmd.CreateParameter();
          pSPart.DbType = DbType.String;
          pSPart.ParameterName = "@part";
          cmd.Parameters.Add(pSPart);

          foreach (var part in parts)
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            pSPart.Value = part;

            var value = await ExecuteScalarAsync(cmd, token).ConfigureAwait(false);
            if (null != value && value != DBNull.Value)
            {
              // this part already exists, no need to go further.
              continue;
            }

            // we could not locate this part
            actualParts.Add(part);
          }
          return actualParts;
        }
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Building part list");
        throw;
      }
    }

    /// <summary>
    /// Get the id number of all the parts.
    /// </summary>
    /// <param name="parts"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <param name="createIfNotFound"></param>
    /// <returns></returns>
    private async Task<HashSet<long>> GetPartIdsAsync(HashSet<string> parts, IDbTransaction transaction, CancellationToken token, bool createIfNotFound)
    { 
      // the ids of all the parts, (added or otherwise).
      var partIds = new HashSet<long>();

      // if we have not words... then move on.
      if (!parts.Any())
      {
        return partIds;
      }

      try
      {
        // the parts we actually need to add.
        var partsToAdd = new HashSet<string>();

        // first look for what we have and insert what we do not have.
        var sqlSelect = $"SELECT id FROM {TableParts} WHERE part = @part";
        using (var cmdSelect = CreateDbCommand(sqlSelect, transaction))
        {
          var pSPart = cmdSelect.CreateParameter();
          pSPart.DbType = DbType.String;
          pSPart.ParameterName = "@part";
          cmdSelect.Parameters.Add(pSPart);

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

            if (!createIfNotFound)
            {
              // we could not find it and we do not wish to go further.
              partIds.Add(-1);
              continue;
            }

            // we could not locate this part
            // so it will need to be added.
            partsToAdd.Add(part);
          }
        }
        
        // then add the ids of the remaining parts.
        partIds.UnionWith( await InsertPartsAsync( partsToAdd, transaction, token ).ConfigureAwait(false));

        // return everything we found.
        return partIds;
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Get Next valid Part id");
        throw;
      }
    }

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
