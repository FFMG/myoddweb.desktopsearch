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
using myoddweb.desktopsearch.helper.IO;
using myoddweb.desktopsearch.interfaces.Enums;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal class SqlitePersisterFolders : IFolders
  {
    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;

    /// <inheritdoc />
    public IFolderUpdates FolderUpdates { get; }

    /// <inheritdoc />
    public IFiles Files { get; }

    public SqlitePersisterFolders(ICounts counts, IList<IFileParser> parsers, ILogger logger)
    {
      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      // the files ... in our folders
      Files = new SqlitePersisterFiles(this, counts, parsers, logger);

      // the folder updates.
      FolderUpdates = new SqlitePersisterFolderUpdates(Files, this, logger);
    }

    /// <inheritdoc />
    public async Task<bool> AddOrUpdateDirectoryAsync(DirectoryInfo directory, IConnectionFactory connectionFactory, CancellationToken token)
    {
      return await AddOrUpdateDirectoriesAsync(new [] {directory}, connectionFactory, token ).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> AddOrUpdateDirectoriesAsync(IReadOnlyList<DirectoryInfo> directories, IConnectionFactory connectionFactory, CancellationToken token )
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      // rebuild the list of directory with only those that need to be inserted.
      await InsertDirectoriesAsync(
        await RebuildDirectoriesListAsync(directories, connectionFactory, token).ConfigureAwait(false),
        connectionFactory, token).ConfigureAwait(false);

      return true;
    }

    /// <inheritdoc />
    public async Task<long> RenameOrAddDirectoryAsync(DirectoryInfo directory, DirectoryInfo oldDirectory, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      try
      {
        var sql = $"UPDATE {Tables.Folders} SET path=@path1 WHERE path=@path2";
        using (var cmd = connectionFactory.CreateCommand(sql))
        {
          var pPath1 = cmd.CreateParameter();
          pPath1.DbType = DbType.String;
          pPath1.ParameterName = "@path1";
          cmd.Parameters.Add(pPath1);

          var pPath2 = cmd.CreateParameter();
          pPath2.DbType = DbType.String;
          pPath2.ParameterName = "@path2";
          cmd.Parameters.Add(pPath2);

          // try and replace path1 with path2
          pPath1.Value = directory.FullName.ToLowerInvariant();
          pPath2.Value = oldDirectory.FullName.ToLowerInvariant();
          if (0 == await connectionFactory.ExecuteWriteAsync(cmd, token).ConfigureAwait(false))
          {
            // we could not rename it, this could be because of an error
            // or because the old path simply does not exist.
            // in that case we can try and simply add the new path.
            _logger.Error($"There was an issue renaming folder: {directory.FullName} to persister");

            // try and add it back in...
            var ids = await InsertDirectoriesAsync(new[] {oldDirectory}, connectionFactory, token).ConfigureAwait(false);
            if( !ids.Any())
            {
              return -1;
            }
          }

          // if we are here, we either renamed the directory or we managed 
          // to add add a new directory
          // in either cases, we can now return the id of this newly added path.
          // we won't ask the function to insert a new file as we _just_ renamed it.
          // so it has to exist...
          var folderId = await GetDirectoryIdAsync(directory, connectionFactory, token, false).ConfigureAwait(false);

          // touch that folder as changed
          await FolderUpdates.TouchDirectoriesAsync(new []{folderId}, UpdateType.Changed, connectionFactory, token).ConfigureAwait(false);

          // get out if needed.
          token.ThrowIfCancellationRequested();

          // we are done
          return folderId;
        }
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Update directory");
        throw;
      }
      catch (Exception ex)
      {
        _logger.Exception(ex);
      }
      return -1;
    }
 
    /// <inheritdoc />
    public async Task<bool> DeleteDirectoryAsync(DirectoryInfo directory, IConnectionFactory connectionFactory, CancellationToken token)
    {
      return await DeleteDirectoriesAsync(new[] { directory }, connectionFactory, token).ConfigureAwait( false );
    }

    /// <inheritdoc />
    public async Task<bool> DeleteDirectoriesAsync(IReadOnlyList<DirectoryInfo> directories, IConnectionFactory connectionFactory, CancellationToken token)
    {
      // if we have nothing to do... we are done.
      if (!directories.Any())
      {
        return true;
      }

      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      var sqlDelete = $"DELETE FROM {Tables.Folders} WHERE path=@path";
      using (var cmd = connectionFactory.CreateCommand(sqlDelete))
      {
        var pPath = cmd.CreateParameter();
        pPath.DbType = DbType.String;
        pPath.ParameterName = "@path";
        cmd.Parameters.Add(pPath);

        try
        {
          foreach (var directory in directories)
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // try and delete files given directory info.
            await Files.DeleteFilesAsync(directory, connectionFactory, token).ConfigureAwait(false);

            // then do the actual delete.
            pPath.Value = directory.FullName.ToLowerInvariant();
            if (0 == await connectionFactory.ExecuteWriteAsync(cmd, token).ConfigureAwait(false))
            {
              _logger.Warning($"Could not delete folder: {directory.FullName}, does it still exist?");
            }
          }

          // touch all the folders as deleted.
          await FolderUpdates.TouchDirectoriesAsync(directories, UpdateType.Deleted, connectionFactory, token).ConfigureAwait(false);

          // we are done.
          return true;
        }
        catch (OperationCanceledException)
        {
          _logger.Warning("Received cancellation request - Delete directories");
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
    public async Task<bool> DirectoryExistsAsync(DirectoryInfo directory, IConnectionFactory connectionFactory, CancellationToken token)
    {
      return await GetDirectoryIdAsync(directory, connectionFactory, token, false).ConfigureAwait(false) != -1;
    }

    /// <inheritdoc />
    public async Task<DirectoryInfo> GetDirectoryAsync(long directoryId, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      try
      {
        // we want to get the latest updated folders.
        var sql = $"SELECT path FROM {Tables.Folders} WHERE id = @id";
        using (var cmd = connectionFactory.CreateCommand(sql))
        {
          var pId = cmd.CreateParameter();
          pId.DbType = DbType.Int64;
          pId.ParameterName = "@id";
          cmd.Parameters.Add(pId);

          // set the folder id.
          cmd.Parameters["@id"].Value = directoryId;

          // get the path
          var path = await connectionFactory.ExecuteReadOneAsync(cmd, token).ConfigureAwait(false);
          if (null == path || path == DBNull.Value)
          {
            return null;
          }

          // return the valid paths.
          return helper.File.DirectoryInfo( (string)path, _logger );
        }
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        return null;
      }
    }

    /// <inheritdoc />
    public async Task<List<long>> GetDirectoriesIdAsync(IReadOnlyCollection<DirectoryInfo> directories, IConnectionFactory connectionFactory, CancellationToken token, bool createIfNotFound)
    {
      if (null == directories)
      {
        throw new ArgumentNullException(nameof(directories), "The given directory list is null");
      }

      if (!directories.Any())
      {
        return new List<long>();
      }

      // the list of ids.
      var ids = new List<long>(directories.Count);

      var directoriesToAdd = new List<DirectoryInfo>();

      // we first look for it, and, if we find it then there is nothing to do.
      var sql = $"SELECT id FROM {Tables.Folders} WHERE path=@path";
      using (var cmd = connectionFactory.CreateCommand(sql))
      {
        var pPath = cmd.CreateParameter();
        pPath.DbType = DbType.String;
        pPath.ParameterName = "@path";
        cmd.Parameters.Add(pPath);

        foreach (var directory in directories)
        {
          pPath.Value = directory.FullName.ToLowerInvariant();
          var value = await connectionFactory.ExecuteReadOneAsync(cmd, token).ConfigureAwait(false);
          if (null != value && value != DBNull.Value)
          {
            // get the path id.
            ids.Add((long)value);
            continue;
          }

          if (!createIfNotFound)
          {
            // we could not find it and we do not wish to go further.
            ids.Add(-1);
            continue;
          }

          // we will add this
          directoriesToAdd.Add(directory);
        }
      }

      // we will then try and add all the folsers in our list
      // and return whatever we found.
      ids.AddRange(await InsertDirectoriesAsync(directoriesToAdd, connectionFactory, token).ConfigureAwait(false));

      // we are done
      return ids;
    }

    /// <inheritdoc />
    public async Task<long> GetDirectoryIdAsync(DirectoryInfo directory, IConnectionFactory connectionFactory, CancellationToken token, bool createIfNotFound)
    {
      if (null == directory)
      {
        throw new ArgumentNullException(nameof(directory), "The given directory is null");
      }
      var ids = await GetDirectoriesIdAsync(new List<DirectoryInfo> { directory }, connectionFactory, token, createIfNotFound).ConfigureAwait(false);
      return ids.Any() ? ids.First() : -1;
    }
    
    #region private functions
    /// <summary>
    /// Get the next row ID we can use.
    /// </summary>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<long> GetNextDirectoryIdAsync(IConnectionFactory connectionFactory, CancellationToken token)
    {
      // we first look for it, and, if we find it then there is nothing to do.
      var sql = $"SELECT max(id) from {Tables.Folders};";
      using (var cmd = connectionFactory.CreateCommand(sql))
      {
        var value = await connectionFactory.ExecuteReadOneAsync(cmd, token).ConfigureAwait(false);
        if (null == value || value == DBNull.Value)
        {
          return 0;
        }

        // we could not find this path ... so just add it.
        return ((long) value) + 1;
      }
    }

    /// <summary>
    /// Given a list of directories, re-create the ones that we need to insert.
    /// </summary>
    /// <param name="directories"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<List<long>> InsertDirectoriesAsync(IReadOnlyList<DirectoryInfo> directories, IConnectionFactory connectionFactory, CancellationToken token)
    {
      // if we have nothing to do... we are done.
      if (!directories.Any())
      {
        return new List<long>();
      }

      // all the ids we have just added.
      var ids = new List<long>(directories.Count);

      // get the next id.
      var nextId = await GetNextDirectoryIdAsync(connectionFactory, token).ConfigureAwait(false);

      var sqlInsert = $"INSERT INTO {Tables.Folders} (id, path) VALUES (@id, @path)";
      using (var cmd = connectionFactory.CreateCommand(sqlInsert))
      {
        var pId = cmd.CreateParameter();
        pId.DbType = DbType.Int64;
        pId.ParameterName = "@id";
        cmd.Parameters.Add(pId);

        var pPath = cmd.CreateParameter();
        pPath.DbType = DbType.String;
        pPath.ParameterName = "@path";
        cmd.Parameters.Add(pPath);
        foreach (var directory in directories)
        {
          try
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            pId.Value = nextId;
            pPath.Value = directory.FullName.ToLowerInvariant();
            if (0 == await connectionFactory.ExecuteWriteAsync(cmd, token).ConfigureAwait(false))
            {
              _logger.Error($"There was an issue adding folder: {directory.FullName} to persister");
              continue;
            }

            // add it to our list of ids
            ids.Add( nextId );

            // we can now move on to the next folder id.
            ++nextId;
          }
          catch (OperationCanceledException)
          {
            _logger.Warning("Received cancellation request - Insert directories");
            throw;
          }
          catch (Exception ex)
          {
            _logger.Exception(ex);
          }
        }
      }

      // then we can touch all the folders we created.
      await FolderUpdates.TouchDirectoriesAsync(ids, UpdateType.Created, connectionFactory, token).ConfigureAwait(false);

      // return all the ids that we added.
      return ids;
    }

    /// <summary>
    /// Given a list of directories, re-create the ones that we need to insert.
    /// </summary>
    /// <param name="directories"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<List<DirectoryInfo>> RebuildDirectoriesListAsync(IEnumerable<DirectoryInfo> directories, IConnectionFactory connectionFactory, CancellationToken token )
    {
      try
      {
        // make that list unique
        var uniqueList = directories.Distinct(new DirectoryInfoComparer());

        // The list of directories we will be adding to the list.
        var actualDirectories = new List<DirectoryInfo>();

        // we first look for it, and, if we find it then there is nothing to do.
        var sql = $"SELECT id FROM {Tables.Folders} WHERE path=@path";
        using (var cmd = connectionFactory.CreateCommand(sql))
        {
          var pPath = cmd.CreateParameter();
          pPath.DbType = DbType.String;
          pPath.ParameterName = "@path";
          cmd.Parameters.Add(pPath);
          foreach (var directory in uniqueList)
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // only valid paths are added.
            if (!directory.Exists)
            {
              continue;
            }

            cmd.Parameters["@path"].Value = directory.FullName.ToLowerInvariant();
            if (null != await connectionFactory.ExecuteReadOneAsync(cmd, token).ConfigureAwait(false))
            {
              continue;
            }

            // we could not find this path ... so just add it.
            actualDirectories.Add(directory);
          }
        }
        return actualDirectories;
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Rebuild directories list");
        throw;
      }
    }
    #endregion
  }
}
