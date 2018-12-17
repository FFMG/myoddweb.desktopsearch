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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.helper.Persisters;
using myoddweb.desktopsearch.interfaces.Enums;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal class SqlitePersisterFileUpdates : IFileUpdates
  {
    /// <summary>
    /// The folder updates helper
    /// </summary>
    private FileUpdatesHelper _filesUpdatesHelper;

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
    public void Prepare(IPersister persister, IConnectionFactory factory)
    {
      // sanity check.
      Contract.Assert(_filesUpdatesHelper == null);
      _filesUpdatesHelper = new FileUpdatesHelper(factory, Tables.FileUpdates);
    }

    /// <inheritdoc />
    public void Complete(bool success)
    {
      _filesUpdatesHelper?.Dispose();
      _filesUpdatesHelper = null;
    }

    /// <inheritdoc />
    public async Task<bool> TouchFileAsync(FileInfo file, UpdateType type, CancellationToken token)
    {
      var fileId = await _files.GetFileIdAsync(file, token, false).ConfigureAwait(false);
      if (-1 == fileId)
      {
        return true;
      }

      // then we can do the update
      return await TouchFilesAsync(new [] {fileId}, type, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> TouchFileAsync(long fileId, UpdateType type, CancellationToken token)
    {
      // just make the files do all the work.
      return await TouchFilesAsync(new [] { fileId }, type, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> TouchFilesAsync(IEnumerable<long> fileIds, UpdateType type, CancellationToken token)
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

      // first mark that folder id as procesed.
      if (!await MarkFilesProcessedAsync( ids, token).ConfigureAwait(false))
      {
        return false;
      }

      try
      {
        Contract.Assert( _filesUpdatesHelper != null );
        var insertCount = await _filesUpdatesHelper.TouchAsync(ids, type, token).ConfigureAwait(false);

        // update the pending files count.
        await _counts.UpdatePendingUpdatesCountAsync(insertCount, token).ConfigureAwait(false);
        return true;
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

    /// <inheritdoc />
    public async Task<bool> MarkFileProcessedAsync(FileInfo file, CancellationToken token)
    {
      // look for the files id
      var fileId = await _files.GetFileIdAsync(file, token, false).ConfigureAwait(false);

      // did we find it?
      if (fileId == -1)
      {
        return true;
      }

      // then we can do it by id.
      return await MarkFilesProcessedAsync(new []{fileId}, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> MarkFileProcessedAsync(long fileId, CancellationToken token)
    {
      // just make the files do all the work.
      return await MarkFilesProcessedAsync(new [] { fileId }, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> MarkFilesProcessedAsync(IEnumerable<long> fileIds, CancellationToken token)
    {
      try
      {
        Contract.Assert( _filesUpdatesHelper != null );
        var deletedCount = await _filesUpdatesHelper.DeleteAsync(fileIds.ToList(), token).ConfigureAwait(false);

        // update the pending files count.
        await _counts.UpdatePendingUpdatesCountAsync(-1 * deletedCount, token).ConfigureAwait(false);

        // all done.
        return true;
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

    /// <inheritdoc />
    public async Task<IList<IPendingFileUpdate>> GetPendingFileUpdatesAsync(long limit, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      // the pending updates
      var pendingUpdates = new List<IPendingFileUpdate>();
      try
      {
        // we want to get the latest updated folders.
        var sql = "SELECT fu.fileid as fileid, fu.type as type, f.name as name, fo.path FROM "+
                 $"{Tables.FileUpdates} fu, {Tables.Files} f, {Tables.Folders} fo "+
                 $"WHERE f.id = fu.fileid and fo.id = f.folderid ORDER BY fu.ticks DESC LIMIT {limit}";
        using (var cmd = connectionFactory.CreateCommand(sql))
        using (var reader = await connectionFactory.ExecuteReadAsync(cmd, token).ConfigureAwait(false))
        {
          var fileIdPos = reader.GetOrdinal("fileid");
          var typePos = reader.GetOrdinal("type");
          var pathPos = reader.GetOrdinal("path");
          var namePos = reader.GetOrdinal("name");
          while (reader.Read())
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // the file id
            var fileid = (long)reader[fileIdPos];

            // the update type
            var type = (UpdateType)(long)reader[typePos];

            // the folder path
            var path = (string)reader[pathPos];

            // the file name
            var name = (string)reader[namePos];

            // add this update
            pendingUpdates.Add(new PendingFileUpdate(
              fileid,
              new FileInfo(Path.Combine(path, name)),
              type
            ));
          }
        }

        // deleted files are not found here, this is because
        // the previous query looks for left join for the file name/directory name
        // if the files is deleted... they do not exist anymore.
        if (pendingUpdates.Count < limit)
        {
          sql = $"SELECT fileid FROM {Tables.FileUpdates} WHERE type={(int)UpdateType.Deleted} ORDER BY ticks DESC LIMIT {limit - pendingUpdates.Count}";
          using (var cmd = connectionFactory.CreateCommand(sql))
          using (var reader = await connectionFactory.ExecuteReadAsync(cmd, token).ConfigureAwait(false))
          {
            var fileIdPos = reader.GetOrdinal("fileid");
            while (reader.Read())
            {
              // get out if needed.
              token.ThrowIfCancellationRequested();

              // the file id
              var fileid = (long)reader[fileIdPos];

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
