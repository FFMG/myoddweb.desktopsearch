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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal class SqlitePersisterFileUpdates : IFileUpdates
  {
    /// <summary>
    /// The files table
    /// </summary>
    private string TableFiles { get; }

    /// <summary>
    /// The folders table.
    /// </summary>
    private string TableFolders { get; }
  
    /// <summary>
    /// The files update table
    /// </summary>
    private string TableFileUpdates { get; }

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The files interface.
    /// </summary>
    private readonly IFiles _files;

    /// <summary>
    /// The counters
    /// </summary>
    private readonly ICounts _counts;

    public SqlitePersisterFileUpdates(IFiles files, ICounts counts, ILogger logger)
    {
      //  the files interface.
      _files = files ?? throw new ArgumentNullException(nameof(files));

      // the counter interface
      _counts = counts ?? throw new ArgumentNullException(nameof(counts));

      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> TouchFileAsync(FileInfo file, UpdateType type, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory),
          "You have to be within a tansaction when calling this function.");
      }

      var fileId = await _files.GetFileIdAsync(file, connectionFactory, token, false).ConfigureAwait(false);
      if (-1 == fileId)
      {
        return true;
      }

      // then we can do the update
      return await TouchFilesAsync(new [] {fileId}, type, connectionFactory, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> TouchFileAsync(long fileId, UpdateType type, IConnectionFactory connectionFactory, CancellationToken token)
    {
      // just make the files do all the work.
      return await TouchFilesAsync(new [] { fileId }, type, connectionFactory, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> TouchFilesAsync(IEnumerable<long> fileIds, UpdateType type, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == fileIds)
      {
        throw new ArgumentNullException( nameof(fileIds) );
      }

      // if it is not a valid id then there is nothing for us to do.
      var ids = fileIds as long[] ?? fileIds.ToArray();
      if (!ids.Any())
      {
        return true;
      }

      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      // first mark that folder id as procesed.
      if (!await MarkFilesProcessedAsync( ids, connectionFactory, token).ConfigureAwait(false))
      {
        return false;
      }

      var insertCount = 0;
      var sqlInsert = $"INSERT INTO {TableFileUpdates} (fileid, type, ticks) VALUES (@id, @type, @ticks)";
      using (var cmd = connectionFactory.CreateCommand(sqlInsert))
      {
        try
        {
          var pId = cmd.CreateParameter();
          pId.DbType = DbType.Int64;
          pId.ParameterName = "@id";
          cmd.Parameters.Add(pId);

          var pType = cmd.CreateParameter();
          pType.DbType = DbType.Int64;
          pType.ParameterName = "@type";
          cmd.Parameters.Add(pType);

          var pTicks = cmd.CreateParameter();
          pTicks.DbType = DbType.Int64;
          pTicks.ParameterName = "@ticks";
          cmd.Parameters.Add(pTicks);

          foreach (var fileId in ids)
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            pId.Value = fileId;
            pType.Value = (long) type;
            pTicks.Value = DateTime.UtcNow.Ticks;
            if (0 == await connectionFactory.ExecuteWriteAsync( cmd, token).ConfigureAwait(false))
            {
              _logger.Error($"There was an issue adding file the file update: {fileId} to persister");
              return false;
            }
            ++insertCount;
          }
        }
        catch (OperationCanceledException)
        {
          _logger.Warning("Received cancellation request - Insert multiple files.");
          throw;
        }
        catch (Exception ex)
        {
          _logger.Exception(ex);
          return false;
        }
      }

      // update the pending files count.
      await _counts.UpdatePendingUpdatesCountAsync(insertCount, connectionFactory, token).ConfigureAwait(false);
      return true;
    }

    /// <inheritdoc />
    public async Task<bool> MarkFileProcessedAsync(FileInfo file, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      // look for the files id
      var fileId = await _files.GetFileIdAsync(file, connectionFactory, token, false).ConfigureAwait(false);

      // did we find it?
      if (fileId == -1)
      {
        return true;
      }

      // then we can do it by id.
      return await MarkFilesProcessedAsync(new []{fileId}, connectionFactory, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> MarkFileProcessedAsync(long fileId, IConnectionFactory connectionFactory, CancellationToken token)
    {
      // just make the files do all the work.
      return await MarkFilesProcessedAsync(new [] { fileId }, connectionFactory, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> MarkFilesProcessedAsync(IEnumerable<long> fileIds, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      var deletedCount = 0;
      var sqlDelete = $"DELETE FROM {TableFileUpdates} WHERE fileid = @id";
      using (var cmd = connectionFactory.CreateCommand(sqlDelete))
      {
        try
        {
          var pId = cmd.CreateParameter();
          pId.DbType = DbType.Int64;
          pId.ParameterName = "@id";
          cmd.Parameters.Add(pId);

          foreach (var fileId in fileIds)
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // set the folder id.
            pId.Value = fileId;

            // this could return 0 if the row has already been processed
            if (0 == await connectionFactory.ExecuteWriteAsync(cmd, token).ConfigureAwait(false))
            {
              continue;
            }

            // it was deleted
            ++deletedCount;
          }
        }
        catch (OperationCanceledException)
        {
          _logger.Warning("Received cancellation request - Removing file id from update");
          throw;
        }
        catch (Exception ex)
        {
          _logger.Exception(ex);
          return false;
        }
      }

      // update the pending files count.
      await _counts.UpdatePendingUpdatesCountAsync(-1 * deletedCount, connectionFactory, token).ConfigureAwait(false);

      // return if this cancelled or not
      return true;
    }

    /// <inheritdoc />
    public async Task<List<PendingFileUpdate>> GetPendingFileUpdatesAsync(long limit, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      // the pending updates
      var pendingUpdates = new List<PendingFileUpdate>();
      try
      {
        // we want to get the latest updated folders.
        var sql = "SELECT fu.fileid as fileid, fu.type as type, f.name as name, fo.path FROM "+
                 $"{TableFileUpdates} fu, {TableFiles} f, {TableFolders} fo "+
                 $"WHERE f.id = fu.fileid and fo.id = f.folderid ORDER BY fu.ticks DESC LIMIT {limit}";
        using (var cmd = connectionFactory.CreateCommand(sql))
        {
          var reader = await connectionFactory.ExecuteReadAsync(cmd, token).ConfigureAwait(false);
          while (reader.Read())
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // the file id
            var fileid = (long)reader["fileid"];

            // the update type
            var type = (UpdateType)(long)reader["type"];

            // the folder path
            var path = (string) reader["path"];

            // the file name
            var name = (string) reader["name"];

            // add this update
            pendingUpdates.Add(new PendingFileUpdate(
              fileid,
              new FileInfo( Path.Combine(path, name)),
              type
            ));
          }
        }

        // deleted files are not found here, this is because
        // the previous query looks for left join for the file name/directory name
        // if the files is deleted... they do not exist anymore.
        if (pendingUpdates.Count < limit)
        {
          sql = $"SELECT fileid FROM {TableFileUpdates} WHERE type={(int)UpdateType.Deleted} ORDER BY ticks DESC LIMIT {limit - pendingUpdates.Count}";
          using (var cmd = connectionFactory.CreateCommand(sql))
          {
            var reader = await connectionFactory.ExecuteReadAsync(cmd, token).ConfigureAwait(false);
            while (reader.Read())
            {
              // get out if needed.
              token.ThrowIfCancellationRequested();

              // the file id
              var fileid = (long)reader["fileid"];

              // add this update
              pendingUpdates.Add(new PendingFileUpdate(
                fileid,
                null,
                UpdateType.Deleted
              ));
            }
          }
        }

        // return whatever we found
        return pendingUpdates;
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Getting pending updates.");
        throw;
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        return null;
      }
    }
  }
}
