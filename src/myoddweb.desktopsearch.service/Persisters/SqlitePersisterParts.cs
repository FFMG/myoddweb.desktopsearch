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
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal partial class SqlitePersister
  {
    /// <inheritdoc />
    public async Task<HashSet<long>> GetPartIdsAsync(IReadOnlyCollection<string> parts, IConnectionFactory connectionFactory, CancellationToken token, bool createIfNotFound)
    {
      // the ids of all the parts, (added or otherwise).
      var partIds = new List<long>(parts.Count);

      // if we have not words... then move on.
      if (!parts.Any())
      {
        return new HashSet<long>(partIds);
      }

      try
      {
        // the parts we actually need to add.
        var partsToAdd = new List<string>(parts.Count);

        // first look for what we have and insert what we do not have.
        var sqlSelect = $"SELECT id FROM {TableParts} WHERE part = @part";
        using (var cmdSelect = connectionFactory.CreateCommand(sqlSelect))
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

            var value = await connectionFactory.ExecuteReadOneAsync(cmdSelect, token).ConfigureAwait(false);
            if (null != value && value != DBNull.Value)
            {
              partIds.Add((long)value);
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
        partIds.AddRange(await InsertPartsAsync(partsToAdd.ToArray(), connectionFactory, token).ConfigureAwait(false));

        // return everything we found.
        return new HashSet<long>(partIds);
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Get Next valid Part id");
        throw;
      }
    }

    /// <inheritdoc />
    public async Task<bool> AddOrUpdatePartsAsync(IReadOnlyCollection<string> parts, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory),
          "You have to be within a tansaction when calling this function.");
      }

      // rebuild the list of directory with only those that need to be inserted.
      await InsertPartsAsync(
        await RebuildPartsListAsync(parts, connectionFactory, token).ConfigureAwait(false),
        connectionFactory, token).ConfigureAwait(false);
      return true;
    }

    #region Private parts function
    /// <summary>
    /// Insert parts and return the id of the added parts.
    /// </summary>
    /// <param name="parts"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<List<long>> InsertPartsAsync(IReadOnlyCollection<string> parts, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (!parts.Any())
      {
        return new List<long>();
      }

      // the ids of all the parts inserted.
      var partIds = new List<long>(parts.Count);

      // get the next valid id.
      var nextId = await GetNextPartIdAsync(connectionFactory, token).ConfigureAwait(false);

      try
      {
        // whatever is now left is to be inserted
        var sqlInsert = $"INSERT INTO {TableParts} (id, part) VALUES (@id, @part)";
        using (var cmdInsert = connectionFactory.CreateCommand(sqlInsert))
        {
          var pId = cmdInsert.CreateParameter();
          pId.DbType = DbType.Int64;
          pId.ParameterName = "@id";
          cmdInsert.Parameters.Add(pId);

          var pIPart = cmdInsert.CreateParameter();
          pIPart.DbType = DbType.String;
          pIPart.ParameterName = "@part";
          cmdInsert.Parameters.Add(pIPart);

          foreach (var part in parts.Distinct())
          {
            pId.Value = nextId;
            pIPart.Value = part;

            if (0 == await connectionFactory.ExecuteWriteAsync(cmdInsert, token).ConfigureAwait(false))
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
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<string[]> RebuildPartsListAsync(IReadOnlyCollection<string> parts, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (!parts.Any())
      {
        return new string[0];
      }

      try
      {
        // the actual list of parts we need to add.
        var actualParts = new List<string>(parts.Count);

        // first look for what we have and insert what we do not have.
        var sql = $"SELECT id FROM {TableParts} WHERE part = @part";
        using (var cmd = connectionFactory.CreateCommand(sql))
        {
          var pSPart = cmd.CreateParameter();
          pSPart.DbType = DbType.String;
          pSPart.ParameterName = "@part";
          cmd.Parameters.Add(pSPart);

          foreach (var part in parts.Distinct())
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            pSPart.Value = part;

            var value = await connectionFactory.ExecuteReadOneAsync(cmd, token).ConfigureAwait(false);
            if (null != value && value != DBNull.Value)
            {
              // this part already exists, no need to go further.
              continue;
            }

            // we could not locate this part
            actualParts.Add(part);
          }

          // we don't need to make is distinct as our for loop
          // make sure that we were only selecting unique values.
          return actualParts.ToArray();
        }
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Building part list");
        throw;
      }
    }

    /// <summary>
    /// Get the next row ID we can use.
    /// </summary>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<long> GetNextPartIdAsync(IConnectionFactory connectionFactory, CancellationToken token)
    {
      try
      {
        // we first look for it, and, if we find it then there is nothing to do.
        var sql = $"SELECT max(id) from {TableParts};";
        using (var cmd = connectionFactory.CreateCommand(sql))
        {
          var value = await connectionFactory.ExecuteReadOneAsync(cmd, token).ConfigureAwait(false);

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
