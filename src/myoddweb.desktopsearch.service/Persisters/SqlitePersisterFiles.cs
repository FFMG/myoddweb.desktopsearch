﻿//This file is part of Myoddweb.DesktopSearch.
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
  internal class SqlitePersisterFiles : IFiles
  {
    #region Member Variables
    /// <summary>
    /// The folders interface
    /// </summary>
    private readonly IFolders _folders;

    /// <summary>
    /// The counter manager
    /// </summary>
    private readonly ICounts _counts;

    /// <summary>
    /// The parsers.
    /// </summary>
    private readonly IList<IFileParser> _parsers;

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;
    #endregion

    /// <inheritdoc />
    public IFileUpdates FileUpdates { get; }

    public SqlitePersisterFiles(IFolders folders, ICounts counts, IList<IFileParser> parsers, ILogger logger)
    {
      //  the files interface.
      _folders = folders ?? throw new ArgumentNullException(nameof(folders));

      // the counter
      _counts = counts ?? throw new ArgumentNullException(nameof(counts));

      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      // we can now create the 
      FileUpdates = new SqlitePersisterFileUpdates(this, counts, logger);

      // the various parsers.
      _parsers = parsers ?? throw new ArgumentNullException(nameof(parsers));
    }

    /// <inheritdoc />
    public async Task<bool> AddOrUpdateFileAsync(FileInfo file, IConnectionFactory connectionFactory, CancellationToken token)
    {
      return await AddOrUpdateFilesAsync(new [] { file }, connectionFactory, token ).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> AddOrUpdateFilesAsync(IList<FileInfo> files, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      // get all the news files
      var newFiles = await RebuildFilesListAsync(files, connectionFactory, token).ConfigureAwait(false);

      // then the other fies are not new, they are just updates
      var oldFiles = helper.File.RelativeComplement(newFiles, files);
      foreach (var file in oldFiles)
      {
        if (!IsFileSupportedByAnyParsers(file))
        {
          continue;
        }
        await FileUpdates.TouchFileAsync(file, UpdateType.Changed, connectionFactory, token).ConfigureAwait(false);
      }

      // rebuild the list of directory with only those that need to be inserted.
      return await InsertFilesAsync( newFiles, connectionFactory, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<long> RenameOrAddFileAsync(FileInfo file, FileInfo oldFile, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }
      // this is the new folder, we might as well create it if it does not exit.
      var folderId = await _folders.GetDirectoryIdAsync(file.Directory, connectionFactory, token, true).ConfigureAwait(false);
      if (-1 == folderId)
      {
        // we cannot create the parent folder id
        // so there is nothing more to do really.
        return -1;
      }

      // get the old folder.
      var oldFolderId = await _folders.GetDirectoryIdAsync(oldFile.Directory, connectionFactory, token, true).ConfigureAwait(false);
      if (-1 == oldFolderId)
      {
        // this cannot be a renaming, as the parent dirctory does not exist.
        // so we will just try and add it.
        // by calling the 'GetFile' function and creating it if needed we will insert the file.
        return await GetFileIdAsync( file, connectionFactory, token, true ).ConfigureAwait(false);
      }

      // so we have an old folder id and a new folder id
      var sql = $"UPDATE {Tables.Files} SET name=@name1, folderid=@folderid1 WHERE name=@name2 and folderid=@folderid2";
      using (var cmd = connectionFactory.CreateCommand(sql))
      {
        try
        {
          var pName1 = cmd.CreateParameter();
          pName1.DbType = DbType.String;
          pName1.ParameterName = "@name1";
          cmd.Parameters.Add(pName1);

          var pName2 = cmd.CreateParameter();
          pName2.DbType = DbType.String;
          pName2.ParameterName = "@name2";
          cmd.Parameters.Add(pName2);

          var pFolderId1 = cmd.CreateParameter();
          pFolderId1.DbType = DbType.Int64;
          pFolderId1.ParameterName = "@folderid1";
          cmd.Parameters.Add(pFolderId1);

          var pFolderId2 = cmd.CreateParameter();
          pFolderId2.DbType = DbType.Int64;
          pFolderId2.ParameterName = "@folderid2";
          cmd.Parameters.Add(pFolderId2);

          // try and replace path1 with path2
          cmd.Parameters["@name1"].Value = file.Name.ToLowerInvariant();
          cmd.Parameters["@name2"].Value = oldFile.Name.ToLowerInvariant();
          cmd.Parameters["@folderid1"].Value = folderId;
          cmd.Parameters["@folderid2"].Value = oldFolderId;
          if (0 == await connectionFactory.ExecuteWriteAsync(cmd, token).ConfigureAwait(false))
          {
            // we could not rename it, this could be because of an error
            // or because the old path simply does not exist.
            // in that case we can try and simply add the new path.
            if (!await InsertFilesAsync( new []{file}, connectionFactory, token).ConfigureAwait(false))
            {
              _logger.Error($"There was an issue renaming the file: {file.FullName} to persister");
              return -1;
            }
          }

          // get out if needed.
          token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
          _logger.Warning("Received cancellation request - Rename file");
          throw;
        }
        catch (Exception ex)
        {
          _logger.Exception(ex);
          return -1;
        }
      }

      // if we are here, we either renamed the file
      // or we managed to add add it
      // in either cases, we can now return the id of this newly added file.
      // we won't ask the function to insert a new file as we _just_ renamed it.
      // so it has to exist...
      var fileId = await GetFileIdAsync(file, connectionFactory, token, false).ConfigureAwait(false);
      if (-1 == fileId)
      {
        return -1;
      }

      if (IsFileSupportedByAnyParsers(file))
      {
        // touch that file as changed
        await FileUpdates.TouchFileAsync(fileId, UpdateType.Changed, connectionFactory, token).ConfigureAwait(false);
      }

      // return the new file id
      return fileId;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteFileAsync(FileInfo file, IConnectionFactory connectionFactory, CancellationToken token)
    {
      return await DeleteFilesAsync(new[] { file }, connectionFactory, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteFilesAsync(IList<FileInfo> files, IConnectionFactory connectionFactory, CancellationToken token)
    {
      // if we have nothing to do... we are done.
      if (!files.Any())
      {
        return true;
      }

      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      var deletedFileIdsToTouch = new List<long>(files.Count);
      long deletedCount = 0;
      var sqlDelete = $"DELETE FROM {Tables.Files} WHERE folderid=@folderid and name=@name";
      using (var cmd = connectionFactory.CreateCommand(sqlDelete))
      {
        var pFolderId = cmd.CreateParameter();
        pFolderId.DbType = DbType.Int64;
        pFolderId.ParameterName = "@folderid";
        cmd.Parameters.Add(pFolderId);

        var pName = cmd.CreateParameter();
        pName.DbType = DbType.String;
        pName.ParameterName = "@name";
        cmd.Parameters.Add(pName);
        foreach (var file in files)
        {
          try
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // get the folder id, no need to create it.
            var folderId = await _folders.GetDirectoryIdAsync(file.Directory, connectionFactory, token, false).ConfigureAwait(false);
            if (-1 == folderId)
            {
              _logger.Warning($"Could not delete file: {file.FullName}, could not locate the parent folder?");
              continue;
            }

            // get the file ids
            var fileId = await GetFileIdAsync(file, connectionFactory, token, false).ConfigureAwait(false);

            // if we have no ids... then no point in going further
            if (fileId == -1 )
            {
              _logger.Warning($"Could not delete file: {file.FullName}, could not locate the file?");
              continue;
            }

            if (IsFileSupportedByAnyParsers(file))
            {
              // touch that file as changed.
              deletedFileIdsToTouch.Add( fileId );
            }
            
            // then we can delete this file.
            pFolderId.Value = folderId;
            pName.Value = file.Name.ToLowerInvariant();
            if (0 == await connectionFactory.ExecuteWriteAsync(cmd, token).ConfigureAwait(false))
            {
              _logger.Warning($"Could not delete file: {file.FullName}, does it still exist?");
            }
            else
            {
              // it was deleted
              ++deletedCount;
            }
          }
          catch (OperationCanceledException)
          {
            _logger.Warning("Received cancellation request - Deleting files");
            throw;
          }
          catch (Exception ex)
          {
            _logger.Exception(ex);
            // swallow this execption
            // and try and delete the other files.
          }
        }
      }

      // touch the files.
      await FileUpdates.TouchFilesAsync(deletedFileIdsToTouch, UpdateType.Deleted, connectionFactory, token).ConfigureAwait(false);

      // update the files count.
      await _counts.UpdateFilesCountAsync( -1* deletedCount, connectionFactory, token).ConfigureAwait(false);

      // we are done.
      return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteFilesAsync(DirectoryInfo directory, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      // get the folder id, we do not want to create the folder.
      var folderId = await _folders.GetDirectoryIdAsync(directory, connectionFactory, token, false).ConfigureAwait(false);
      if (folderId == -1)
      {
        // there was no error.
        return true;
      }

      // delete this by folder id.
      return await DeleteFilesAsync(folderId, connectionFactory, token).ConfigureAwait(false);
    }
    
    /// <inheritdoc />
    public async Task<bool> DeleteFilesAsync(long folderId, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      // get the files for that folder so we can fag them as touched.
      var fileIds = await GetFileIdsAsync(folderId, connectionFactory, token).ConfigureAwait(false);
      if (fileIds.Any())
      {
        // touch that folder as changed
        await FileUpdates.TouchFilesAsync(fileIds, UpdateType.Deleted, connectionFactory, token).ConfigureAwait(false);

        // we are about to delete those files.
        await _counts.UpdateFilesCountAsync(fileIds.Count, connectionFactory, token).ConfigureAwait(false);
      }

      var sql = $"DELETE FROM {Tables.Files} WHERE folderid=@folderid";
      using (var cmd = connectionFactory.CreateCommand(sql))
      {
        try
        {
          var pFolderId = cmd.CreateParameter();
          pFolderId.DbType = DbType.Int64;
          pFolderId.ParameterName = "@folderid";
          cmd.Parameters.Add(pFolderId);

          // set the folder id.
          cmd.Parameters["@folderid"].Value = folderId;

          // delete the files.
          var deletedFiles = await connectionFactory.ExecuteWriteAsync(cmd, token).ConfigureAwait(false);

          // and give a message...
          _logger.Verbose($"Deleted {deletedFiles} file(s) from folder id: {folderId}.");

          // get out if needed.
          token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
          _logger.Warning("Received cancellation request - Delete files by folder");
          throw;
        }
        catch (Exception ex)
        {
          _logger.Exception(ex);
          return false;
        }
      }

      // we are done.
      return true;
    }

    /// <inheritdoc />
    public async Task<bool> FileExistsAsync(FileInfo file, IConnectionFactory connectionFactory, CancellationToken token)
    {
      return (await GetFileIdAsync(file, connectionFactory, token, false ).ConfigureAwait(false) != -1);
    }

    /// <inheritdoc />
    public async Task<IList<FileInfo>> GetFilesAsync(long directoryId, IConnectionFactory connectionFactory,
      CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory),
          "You have to be within a tansaction when calling this function.");
      }

      // look for the directory
      var directory = await _folders.GetDirectoryAsync(directoryId, connectionFactory, token).ConfigureAwait(false);
      if (null == directory)
      {
        // with no directory, there is nothing we can do.
        return null;
      }

      // then pass the two values we have
      return await GetFilesAsync(directoryId, directory, connectionFactory, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<long> GetFileIdAsync(FileInfo file, IConnectionFactory connectionFactory, CancellationToken token, bool createIfNotFound)
    {
      try
      {
        // get the folder id
        var folderid = await _folders.GetDirectoryIdAsync(file.Directory, connectionFactory, token, false).ConfigureAwait(false);
        if (-1 == folderid)
        {
          if (!createIfNotFound)
          {
            return -1;
          }

          // add the file, if we get an error now, there is nothing we can do about it.
          if (!await InsertFilesAsync(new[] { file }, connectionFactory, token).ConfigureAwait(false))
          {
            return -1;
          }

          // try and look for it
          folderid = await GetFileIdAsync(file, connectionFactory, token, false).ConfigureAwait(false);
          if (-1 == folderid)
          {
            // we cannot get the folder that we just add!
            _logger.Error($"I could not find the folder that I _just_ added! (path: {file.FullName}).");
            return -1;
          }
        }

        // we first look for it, and, if we find it then there is nothing to do.
        var sql = $"SELECT id FROM {Tables.Files} WHERE folderid=@folderid and name=@name";
        using (var cmd = connectionFactory.CreateCommand(sql))
        {
          var pFolderId = cmd.CreateParameter();
          pFolderId.DbType = DbType.Int64;
          pFolderId.ParameterName = "@folderid";
          cmd.Parameters.Add(pFolderId);

          var pName = cmd.CreateParameter();
          pName.DbType = DbType.String;
          pName.ParameterName = "@name";
          cmd.Parameters.Add(pName);

          pFolderId.Value = folderid;
          pName.Value = file.Name.ToLowerInvariant();
          var value = await connectionFactory.ExecuteReadOneAsync(cmd, token).ConfigureAwait(false);
          if (null == value || value == DBNull.Value)
          {
            if (!createIfNotFound)
            {
              // we could not find it and we do not wish to go further.
              return -1;
            }

            // try and add the folder, if that does not work, then we cannot go further.
            if (!await InsertFilesAsync(new[] { file }, connectionFactory, token).ConfigureAwait(false))
            {
              return -1;
            }

            // try one more time to look for it .. and if we do not find it, then just return
            return await GetFileIdAsync(file, connectionFactory, token, false).ConfigureAwait(false);
          }

          // get out if needed.
          token.ThrowIfCancellationRequested();

          // get the path id.
          return (long)value;
        }
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Get single file id.");
        throw;
      }
    }

    /// <inheritdoc />
    public async Task<FileInfo> GetFileAsync(long fileId, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      // the pending updates
      FileInfo file = null;
      try
      {
        // we want to get the latest updated folders.
        var sql = $"SELECT folderid, name FROM {Tables.Files} WHERE id=@id";
        using (var cmd = connectionFactory.CreateCommand(sql))
        {
          var pId = cmd.CreateParameter();
          pId.DbType = DbType.Int64;
          pId.ParameterName = "@id";
          cmd.Parameters.Add(pId);

          // set the folder id.
          cmd.Parameters["@id"].Value = fileId;
          var reader = await connectionFactory.ExecuteReadAsync(cmd, token).ConfigureAwait(false);
          if (reader.Read())
          {
            // get the directory
            var directory = await _folders.GetDirectoryAsync((long) reader["folderid"], connectionFactory, token)
              .ConfigureAwait(false);

            // sanity check
            if (null == directory)
            {
              _logger.Error(
                $"The file '{(string) reader["name"]}'({fileId}) is on record, but the fodler ({(long) reader["folderid"]}) does not exist!");
              return null;
            }

            // we can now rebuild the file info.
            file = new FileInfo(Path.Combine(directory.FullName, (string) reader["name"]));

            // get out if needed.
            token.ThrowIfCancellationRequested();
          }
        }
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Get a file");
        throw;
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        return null;
      }

      // return whatever we found
      return file;
    }

    #region private functions
    /// <summary>
    /// Check if any parser supports this file.
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    private bool IsFileSupportedByAnyParsers(FileSystemInfo file)
    {
      return _parsers.Any(parser => parser.Supported(file));
    }

    private async Task<IList<FileInfo>> GetFilesAsync(long directoryId, DirectoryInfo directory, IConnectionFactory connectionFactory, CancellationToken token)
    {
      if (null == connectionFactory)
      {
        throw new ArgumentNullException(nameof(connectionFactory), "You have to be within a tansaction when calling this function.");
      }

      if (null == directory)
      {
        throw new ArgumentNullException(nameof(directory), "The directory passed to us cannot be empty.");
      }

      // the pending updates
      var fileInfos = new List<FileInfo>();
      try
      {
        // we want to get the latest updated folders.
        var sql = $"SELECT name FROM {Tables.Files} WHERE folderid=@id";
        using (var cmd = connectionFactory.CreateCommand(sql))
        {
          var pId = cmd.CreateParameter();
          pId.DbType = DbType.Int64;
          pId.ParameterName = "@id";
          cmd.Parameters.Add(pId);

          // set the folder id.
          pId.Value = directoryId;
          var reader = await connectionFactory.ExecuteReadAsync(cmd, token).ConfigureAwait(false);
          while (reader.Read())
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // add this update
            fileInfos.Add(new FileInfo(
              Path.Combine(directory.FullName, (string)reader["name"])));
          }

          // return whatever we found
          return fileInfos;
        }
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Get files by folder id");
        throw;
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        throw;
      }
    }

    /// <summary>
    /// Given a list of files, re-create the ones that we need to insert.
    /// </summary>
    /// <param name="files"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> InsertFilesAsync(IList<FileInfo> files, IConnectionFactory connectionFactory, CancellationToken token)
    {
      // if we have nothing to do... we are done.
      if (!files.Any())
      {
        return true;
      }

      // what we actually inserted
      var insertedCount = 0;

      var sqlSelect = $"SELECT id FROM {Tables.Files} where folderid=@folderid AND name=@name";
      var sqlInsert = $"INSERT INTO {Tables.Files} (folderid, name) VALUES (@folderid, @name)";
      using (var cmdInsert = connectionFactory.CreateCommand(sqlInsert))
      using (var cmdSelect = connectionFactory.CreateCommand(sqlSelect))
      {
        var pSFolderId = cmdSelect.CreateParameter();
        pSFolderId.DbType = DbType.Int64;
        pSFolderId.ParameterName = "@folderid";
        cmdSelect.Parameters.Add(pSFolderId);

        var pSName = cmdSelect.CreateParameter();
        pSName.DbType = DbType.String;
        pSName.ParameterName = "@name";
        cmdSelect.Parameters.Add(pSName);

        var pIFolderId = cmdInsert.CreateParameter();
        pIFolderId.DbType = DbType.Int64;
        pIFolderId.ParameterName = "@folderid";
        cmdInsert.Parameters.Add(pIFolderId);

        var pIName = cmdInsert.CreateParameter();
        pIName.DbType = DbType.String;
        pIName.ParameterName = "@name";
        cmdInsert.Parameters.Add(pIName);

        var idsToTouch = new List<long>();

        foreach (var file in files)
        {
          try
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // Get the folder for this file and insert it, if need be.
            var folderId = await _folders.GetDirectoryIdAsync(file.Directory, connectionFactory, token, true).ConfigureAwait(false);
            if (-1 == folderId)
            {
              _logger.Error($"I was unable to insert {file.FullName} as I could not locate and insert the directory!");
              return false;
            }

            pIFolderId.Value = folderId;
            pIName.Value = file.Name.ToLowerInvariant();
            if (0 == await connectionFactory.ExecuteWriteAsync(cmdInsert, token).ConfigureAwait(false))
            {
              _logger.Error($"There was an issue adding file: {file.FullName} to persister");
              continue;
            }

            if (IsFileSupportedByAnyParsers(file))
            {
              pSFolderId.Value = folderId;
              pSName.Value = file.Name.ToLowerInvariant();

              var value = await connectionFactory.ExecuteReadOneAsync(cmdSelect, token).ConfigureAwait(false);
              if (null == value || value == DBNull.Value)
              {
                _logger.Error($"There was an issue finding the file id: {file.FullName} from perister");
                continue;
              }
              // we will need to touch this id.
              idsToTouch.Add( (long)value );
            }

            // this item was inserted
            ++insertedCount;
          }
          catch (OperationCanceledException)
          {
            _logger.Warning("Received cancellation request - Insert multiple files");
            throw;
          }
          catch (Exception ex)
          {
            _logger.Exception(ex);
          }
        }

        // we can then touch all the updated items
        await FileUpdates.TouchFilesAsync(idsToTouch, UpdateType.Created, connectionFactory, token).ConfigureAwait(false);
      }

      // we are about to delete those files.
      await _counts.UpdateFilesCountAsync(insertedCount, connectionFactory, token).ConfigureAwait(false);

      return true;
    }

    /// <summary>
    /// Given a list of directories, re-create the ones that we need to insert.
    /// </summary>
    /// <param name="files"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<IList<FileInfo>> RebuildFilesListAsync(IEnumerable<FileInfo> files, IConnectionFactory connectionFactory, CancellationToken token)
    {
      try
      {
        // make that list unique
        var uniqueList = files.Distinct( new FileInfoComparer() );

        // The list of directories we will be adding to the list.
        var actualFiles = new List<FileInfo>();

        // we first look for it, and, if we find it then there is nothing to do.
        var sql = $"SELECT id FROM {Tables.Files} WHERE folderid=@folderid AND name=@name";
        using (var cmd = connectionFactory.CreateCommand(sql))
        {
          var pFolderId = cmd.CreateParameter();
          pFolderId.DbType = DbType.Int64;
          pFolderId.ParameterName = "@folderid";
          cmd.Parameters.Add(pFolderId);

          var pName = cmd.CreateParameter();
          pName.DbType = DbType.String;
          pName.ParameterName = "@name";
          cmd.Parameters.Add(pName);
          foreach (var file in uniqueList)
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // only valid paths are added.
            if (!file.Exists)
            {
              continue;
            }

            // Get the folder for this file and insert it, if need be.
            var folderId = await _folders.GetDirectoryIdAsync(file.Directory, connectionFactory, token, true).ConfigureAwait(false);
            if (-1 == folderId)
            {
              _logger.Error($"I was unable to insert {file.FullName} as I could not locate and insert the directory!");
              continue;
            }

            pFolderId.Value = folderId;
            pName.Value = file.Name.ToLowerInvariant();
            if (null != await connectionFactory.ExecuteReadOneAsync(cmd, token).ConfigureAwait(false))
            {
              continue;
            }

            // we could not find this file
            // so we will just add it to our list.
            actualFiles.Add(file);
          }
        }
        return actualFiles;
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Building file list");
        throw;
      }
    }
    
    /// <summary>
    /// Get the id of all the files that 'belong' to a folder.
    /// </summary>
    /// <param name="folderId"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<IList<long>> GetFileIdsAsync(long folderId, IConnectionFactory connectionFactory, CancellationToken token)
    {
      try
      {
        // we first look for it, and, if we find it then there is nothing to do.
        var sql = $"SELECT id FROM {Tables.Files} WHERE folderid=@folderid";
        using (var cmd = connectionFactory.CreateCommand(sql))
        {
          var fileIds = new List<long>();
          var pFolderId = cmd.CreateParameter();
          pFolderId.DbType = DbType.Int64;
          pFolderId.ParameterName = "@folderid";
          cmd.Parameters.Add(pFolderId);

          cmd.Parameters["@folderid"].Value = folderId;
          var reader = await connectionFactory.ExecuteReadAsync(cmd, token).ConfigureAwait(false);
          while (reader.Read())
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // add this id to the list.
            fileIds.Add((long) reader["id"]);
          }

          // return all the files we found.
          return fileIds;
        }
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Get file ids in folder");
        throw;
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        throw;
      }
    }
    #endregion
  }
}
