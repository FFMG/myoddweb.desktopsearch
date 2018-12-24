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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.helper.IO;
using myoddweb.desktopsearch.helper.Persisters;
using myoddweb.desktopsearch.interfaces.Enums;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal class SqlitePersisterFiles : IFiles
  {
    #region Member Variables
    /// <inheritdoc />
    public IConnectionFactory Factory { get; set; }

    /// <summary>
    /// The files update helper
    /// </summary>
    private IFilesHelper _filesHelper;

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
    public void Prepare(IPersister persister, IConnectionFactory factory)
    {
      // no readonly event posible here.
      if (factory.IsReadOnly)
      {
        return;
      }

      // sanity check.
      Contract.Assert(_filesHelper == null);
      Contract.Assert(Factory == null);

      _filesHelper = new FilesHelper(factory, Tables.Files );
      FileUpdates.Prepare( persister, factory );
      Factory = factory;
    }

    /// <inheritdoc />
    public void Complete(IConnectionFactory factory, bool success)
    {
      FileUpdates.Complete( factory, success );
      if (factory != Factory)
      {
        return;
      }

      _filesHelper?.Dispose();
      _filesHelper = null;
      Factory = null;
    }

    /// <inheritdoc />
    public Task<bool> AddOrUpdateFileAsync(FileInfo file, CancellationToken token)
    {
      return AddOrUpdateFilesAsync(new [] { file }, token );
    }

    /// <inheritdoc />
    public async Task<bool> AddOrUpdateFilesAsync(IList<FileInfo> files, CancellationToken token)
    {
      Contract.Assert( _filesHelper != null );

      // get all the news files
      var newFiles = await RebuildFilesListAsync(files, token).ConfigureAwait(false);

      // then the other fies are not new, they are just updates
      var oldFiles = helper.File.RelativeComplement(newFiles, files);
      foreach (var file in oldFiles)
      {
        if (!IsFileSupportedByAnyParsers(file))
        {
          continue;
        }
        await FileUpdates.TouchFileAsync(file, UpdateType.Changed, token).ConfigureAwait(false);
      }

      // rebuild the list of directory with only those that need to be inserted.
      return await InsertFilesAsync( newFiles, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<long> RenameOrAddFileAsync(FileInfo file, FileInfo oldFile, CancellationToken token)
    {
      try
      {
        // this is the new folder, we might as well create it if it does not exit.
        var folderId = await _folders.GetDirectoryIdAsync(file.Directory, token, true).ConfigureAwait(false);
        if (-1 == folderId)
        {
          // we cannot create the parent folder id
          // so there is nothing more to do really.
          return -1;
        }

        // get the old folder.
        var oldFolderId = await _folders.GetDirectoryIdAsync(oldFile.Directory, token, true).ConfigureAwait(false);
        if (-1 == oldFolderId)
        {
          // this cannot be a renaming, as the parent dirctory does not exist.
          // so we will just try and add it.
          // by calling the 'GetFile' function and creating it if needed we will insert the file.
          return await GetFileIdAsync( file, token, true ).ConfigureAwait(false);
        }

        // sanity check
        Contract.Assert( _filesHelper != null );

        // get the 'current' value in case something changed.
        var oldFileId = await _filesHelper.GetAsync(oldFolderId, oldFile.Name, token).ConfigureAwait(false);
        var updatedFileId = await _filesHelper.RenameAsync(folderId, file.Name, oldFolderId, oldFile.Name, token).ConfigureAwait(false);
        if (-1 == updatedFileId)
        {
          return -1;
        }

        if (IsFileSupportedByAnyParsers(file))
        {
          // touch that file as changed
          if (oldFileId != updatedFileId)
          {
            await FileUpdates.TouchFileAsync(updatedFileId, file.LastWriteTimeUtc.Ticks, UpdateType.Created, token).ConfigureAwait(false);
          }
          else
          {
            await FileUpdates.TouchFileAsync(updatedFileId, file.LastWriteTimeUtc.Ticks, UpdateType.Changed, token).ConfigureAwait(false);
          }
        }

        // if the file id is not the same, it means that the file was either
        // created and/or already existed and was therefore deleted.
        if (oldFileId != updatedFileId)
        {
          await FileUpdates.TouchFileAsync(oldFileId, file.LastWriteTimeUtc.Ticks, UpdateType.Deleted, token).ConfigureAwait(false);
        }

        // return the new file id
        return updatedFileId;
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Rename file");
        throw;
      }
      catch (Exception ex)
      {
        _logger.Exception(ex);
        throw;
      }
    }

    /// <inheritdoc />
    public Task<bool> DeleteFileAsync(FileInfo file, CancellationToken token)
    {
      return DeleteFilesAsync(new[] { file }, token);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteFilesAsync(IList<FileInfo> files, CancellationToken token)
    {
      // if we have nothing to do... we are done.
      if (!files.Any())
      {
        return true;
      }

      // sanity check.
      Contract.Assert( _filesHelper != null );

      var deletedFileIdsToTouch = new List<long>(files.Count);
      long deletedCount = 0;
      foreach (var file in files)
      {
        try
        {
          // get out if needed.
          token.ThrowIfCancellationRequested();

          // get the folder id, no need to create it.
          var folderId = await _folders.GetDirectoryIdAsync(file.Directory, token, false).ConfigureAwait(false);
          if (-1 == folderId)
          {
            _logger.Warning($"Could not delete file: {file.FullName}, could not locate the parent folder?");
            continue;
          }

          // get the file ids
          var fileId = await GetFileIdAsync(file, token, false).ConfigureAwait(false);

          // if we have no ids... then no point in going further
          if (fileId == -1)
          {
            _logger.Warning($"Could not delete file: {file.FullName}, could not locate the file?");
            continue;
          }

          if (IsFileSupportedByAnyParsers(file))
          {
            // touch that file as changed.
            deletedFileIdsToTouch.Add(fileId);
          }

          // then we can delete this file.
          if (!await _filesHelper.DeleteAsync(folderId, file.Name, token ).ConfigureAwait(false))
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

      // touch the files, the ticks time does not matter....
      await FileUpdates.TouchFilesAsync(deletedFileIdsToTouch, DateTime.UtcNow.Ticks, UpdateType.Deleted, token).ConfigureAwait(false);

      // update the files count.
      await _counts.UpdateFilesCountAsync( -1* deletedCount, token).ConfigureAwait(false);

      // we are done.
      return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteFilesAsync(DirectoryInfo directory, CancellationToken token)
    {
      // get the folder id, we do not want to create the folder.
      var folderId = await _folders.GetDirectoryIdAsync(directory, token, false).ConfigureAwait(false);
      if (folderId == -1)
      {
        // there was no error.
        return true;
      }

      // delete this by folder id.
      return await DeleteFilesAsync(folderId, token).ConfigureAwait(false);
    }
    
    /// <inheritdoc />
    public async Task<bool> DeleteFilesAsync(long folderId, CancellationToken token)
    {
      Contract.Assert(_filesHelper != null);

      // get the files for that folder so we can fag them as touched.
      var fileIds = await GetFileIdsAsync(folderId, token).ConfigureAwait(false);
      if (fileIds.Any())
      {
        // touch that folder as changed
        // as the file is deleted we don't really care about the date/time.
        await FileUpdates.TouchFilesAsync(fileIds, DateTime.UtcNow.Ticks, UpdateType.Deleted, token).ConfigureAwait(false);

        // we are about to delete those files.
        await _counts.UpdateFilesCountAsync(fileIds.Count, token).ConfigureAwait(false);
      }

      try
      {
        // delete the files.
        var deletedFiles = await _filesHelper.DeleteAsync(folderId, token).ConfigureAwait(false);

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

      // we are done.
      return true;
    }

    /// <inheritdoc />
    public async Task<bool> FileExistsAsync(FileInfo file, CancellationToken token)
    {
      return (await GetFileIdAsync(file, token, false ).ConfigureAwait(false) != -1);
    }

    /// <inheritdoc />
    public async Task<IList<FileInfo>> GetFilesAsync(long directoryId, CancellationToken token)
    {
      // look for the directory
      var directory = await _folders.GetDirectoryAsync(directoryId, token).ConfigureAwait(false);
      if (null == directory)
      {
        // with no directory, there is nothing we can do.
        return null;
      }

      // then pass the two values we have
      return await GetFilesAsync(directoryId, directory, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<long> GetFileIdAsync(FileInfo file, CancellationToken token, bool createIfNotFound)
    {
      try
      {
        // get the folder id
        var folderid = await _folders.GetDirectoryIdAsync(file.Directory, token, false).ConfigureAwait(false);
        if (-1 == folderid)
        {
          if (!createIfNotFound)
          {
            return -1;
          }

          // add the file, if we get an error now, there is nothing we can do about it.
          if (!await InsertFilesAsync(new[] { file }, token).ConfigureAwait(false))
          {
            return -1;
          }

          // try and look for it
          folderid = await GetFileIdAsync(file, token, false).ConfigureAwait(false);
          if (-1 == folderid)
          {
            // we cannot get the folder that we just add!
            _logger.Error($"I could not find the folder that I _just_ added! (path: {file.FullName}).");
            return -1;
          }
        }

        Contract.Assert( _filesHelper != null );
        var fileId = await _filesHelper.GetAsync(folderid, file.Name, token).ConfigureAwait(false);
        if (-1 != fileId)
        {
          // return the file id.
          return fileId;
        }

        if (!createIfNotFound)
        {
          // we could not find it and we do not wish to go further.
          return -1;
        }

        // try and add the file, if that does not work, then we cannot go further.
        if (!await InsertFilesAsync(new[] { file }, token).ConfigureAwait(false))
        {
          return -1;
        }

        // try one more time to look for it .. and if we do not find it, then just return
        return await _filesHelper.GetAsync(folderid, file.Name, token).ConfigureAwait(false);
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Get single file id.");
        throw;
      }
    }

    /// <inheritdoc />
    public async Task<FileInfo> GetFileAsync(long fileId, CancellationToken token)
    {
      try
      {
        Contract.Assert( _filesHelper != null );
        Contract.Assert( Factory != null);

        // we want to get the latest updated folders.
        var sql = $"SELECT folderid, name FROM {Tables.Files} WHERE id=@id";
        using (var cmd = Factory.CreateCommand(sql))
        {
          var pId = cmd.CreateParameter();
          pId.DbType = DbType.Int64;
          pId.ParameterName = "@id";
          cmd.Parameters.Add(pId);

          // set the folder id.
          pId.Value = fileId;
          using (var reader = await Factory.ExecuteReadAsync(cmd, token).ConfigureAwait(false))
          {
            var folderIdPos = reader.GetOrdinal("folderid");
            var namePos = reader.GetOrdinal("name");
            if (!reader.Read())
            {
              return null;
            }

            // get the directory
            var directory = await _folders.GetDirectoryAsync((long) reader[folderIdPos], token).ConfigureAwait(false);

            // sanity check
            if (null == directory)
            {
              _logger.Error( $"The file '{(string) reader[namePos]}'({fileId}) is on record, but the fodler ({(long) reader[folderIdPos]}) does not exist!");
              return null;
            }

            // get out if needed.
            token.ThrowIfCancellationRequested();

            // we can now rebuild the file info.
            return new FileInfo(Path.Combine(directory.FullName, (string) reader[namePos]));
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
        throw;
      }
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

    private async Task<IList<FileInfo>> GetFilesAsync(long directoryId, DirectoryInfo directory, CancellationToken token)
    {
      if (null == directory)
      {
        throw new ArgumentNullException(nameof(directory), "The directory passed to us cannot be empty.");
      }

      // the pending updates
      var fileInfos = new List<FileInfo>();
      try
      {
        Contract.Assert( _filesHelper != null );
        var files = await _filesHelper.GetAsync(directoryId, token).ConfigureAwait(false);
        foreach (var file in files)
        {
          // get out if needed.
          token.ThrowIfCancellationRequested();

          // add this update
          fileInfos.Add(new FileInfo( Path.Combine(directory.FullName, file.Name )));
        }

        // return whatever we found
        return fileInfos;
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
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> InsertFilesAsync(IList<FileInfo> files, CancellationToken token)
    {
      // if we have nothing to do... we are done.
      if (!files.Any())
      {
        return true;
      }

      // what we actually inserted
      var insertedCount = 0;

      // sanity check...
      Contract.Assert(_filesHelper != null);

      var idsToTouch = new List<long>();

      foreach (var file in files)
      {
        try
        {
          // get out if needed.
          token.ThrowIfCancellationRequested();

          // Get the folder for this file and insert it, if need be.
          var folderId = await _folders.GetDirectoryIdAsync(file.Directory, token, true).ConfigureAwait(false);
          if (-1 == folderId)
          {
            _logger.Error($"I was unable to insert {file.FullName} as I could not locate and insert the directory!");
            return false;
          }

          var fileId = await _filesHelper.InsertAndGetAsync(folderId, file.Name, token).ConfigureAwait(false);
          if (-1 == fileId )
          {
            _logger.Error($"There was an issue adding file: {file.FullName} to persister");
            continue;
          }

          // do we need to 'touch' that file?
          if (IsFileSupportedByAnyParsers(file))
          {
            // we will need to touch this id.
            idsToTouch.Add( fileId );
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

        // we can then touch all the updated items
        // we use 'now' as the created time.
        await FileUpdates.TouchFilesAsync(idsToTouch, DateTime.UtcNow.Ticks, UpdateType.Created, token).ConfigureAwait(false);
      }

      // we are about to delete those files.
      await _counts.UpdateFilesCountAsync(insertedCount, token).ConfigureAwait(false);

      return true;
    }

    /// <summary>
    /// Given a list of directories, re-create the ones that we need to insert.
    /// </summary>
    /// <param name="files"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<IList<FileInfo>> RebuildFilesListAsync(IEnumerable<FileInfo> files, CancellationToken token)
    {
      try
      {
        // make that list unique
        var uniqueList = files.Distinct( new FileInfoComparer() );

        // The list of files we do not currently have saved.
        var actualFiles = new List<FileInfo>();

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
          var folderId = await _folders.GetDirectoryIdAsync(file.Directory, token, true).ConfigureAwait(false);
          if (-1 == folderId)
          {
            _logger.Error($"I was unable to insert {file.FullName} as I could not locate and insert the directory!");
            continue;
          }

          // get the file id, if the file already exists, we don't want to add it to our list.
          var fileId = await _filesHelper.GetAsync(folderId, file.Name, token).ConfigureAwait(false);
          if (-1 != fileId )
          {
            continue;
          }

          // we could not find this file
          // so we will just add it to our list.
          actualFiles.Add(file);
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
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<IList<long>> GetFileIdsAsync(long folderId, CancellationToken token)
    {
      try
      {
        Contract.Assert( _filesHelper != null );
        var files = await _filesHelper.GetAsync(folderId, token).ConfigureAwait(false);
        return files.Select(f => f.Id).ToList();
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
