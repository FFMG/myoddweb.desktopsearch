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
using myoddweb.desktopsearch.helper.IO;
using myoddweb.desktopsearch.helper.Persisters;
using myoddweb.desktopsearch.interfaces.Enums;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal class SqlitePersisterFolders : IFolders
  {
    /// <summary>
    /// The folders helper to help us add/remove/update folders.
    /// </summary>
    private IFoldersHelper _foldersHelper;

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
    public void Prepare(IPersister persister, IConnectionFactory factory)
    {
      // prepare the files
      Files.Prepare(persister, factory);

      // prepare the folder updates
      FolderUpdates.Prepare(persister, factory);

      // no readonly event posible here.
      if (factory.IsReadOnly)
      {
        return;
      }

      // sanity check.
      Contract.Assert(_foldersHelper == null);
      _foldersHelper = new FoldersHelper(factory, Tables.Folders );
    }

    /// <inheritdoc />
    public void Complete(IConnectionFactory factory, bool success)
    {
      Files.Complete(factory, success);
      FolderUpdates.Complete(factory, success);

      if (factory.IsReadOnly)
      {
        return;
      }
      _foldersHelper?.Dispose();
      _foldersHelper = null;
    }

    /// <inheritdoc />
    public Task<bool> AddOrUpdateDirectoryAsync(DirectoryInfo directory, CancellationToken token)
    {
      return AddOrUpdateDirectoriesAsync(new [] {directory}, token );
    }

    /// <inheritdoc />
    public async Task<bool> AddOrUpdateDirectoriesAsync(IReadOnlyList<DirectoryInfo> directories, CancellationToken token )
    {
      // rebuild the list of directory with only those that need to be inserted.
      await InsertDirectoriesAsync(
        await RebuildDirectoriesListAsync(directories, token).ConfigureAwait(false), token).ConfigureAwait(false);

      return true;
    }

    /// <inheritdoc />
    public async Task<long> RenameOrAddDirectoryAsync(DirectoryInfo directory, DirectoryInfo oldDirectory, CancellationToken token)
    {
      try
      {
        // sanity check
        Contract.Assert(_foldersHelper != null);

        // get the current value
        var oldFolderId = await _foldersHelper.GetAsync(oldDirectory, token).ConfigureAwait(false);

        // then try and update it
        var updatedFolderId = await _foldersHelper.RenameAsync(directory, oldDirectory, token).ConfigureAwait(false);
        if (-1 == updatedFolderId )
        {
          // we could not rename it, this could be because of an error
          // or because the old path simply does not exist.
          // in that case we can try and simply add the new path.
          _logger.Error($"There was an issue renaming folder: {directory.FullName} to persister");
          return -1;
        }

        // touch that folder as changed
        if (updatedFolderId == oldFolderId)
        {
          await FolderUpdates.TouchDirectoriesAsync(new[] { updatedFolderId }, UpdateType.Changed, token).ConfigureAwait(false);
        }
        else
        {
          // we created the new one
          await FolderUpdates.TouchDirectoriesAsync(new[] { updatedFolderId }, UpdateType.Created, token).ConfigureAwait(false);

          // but if the old one is not the same as the new one
          // then we must have removed the old one.
          await FolderUpdates.TouchDirectoriesAsync(new[] { oldFolderId }, UpdateType.Deleted, token).ConfigureAwait(false);
        }

        // get out if needed.
        token.ThrowIfCancellationRequested();

        // we are done
        return updatedFolderId;
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Rename directory");
        throw;
      }
      catch (Exception ex)
      {
        _logger.Exception(ex);
        throw;
      }
    }
 
    /// <inheritdoc />
    public Task<bool> DeleteDirectoryAsync(DirectoryInfo directory, CancellationToken token)
    {
      return DeleteDirectoriesAsync(new[] { directory }, token);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteDirectoriesAsync(IReadOnlyList<DirectoryInfo> directories, CancellationToken token)
    {
      // if we have nothing to do... we are done.
      if (!directories.Any())
      {
        return true;
      }

      // sanity check
      Contract.Assert(_foldersHelper != null );

      try
      {
        foreach (var directory in directories)
        {
          // get out if needed.
          token.ThrowIfCancellationRequested();

          // try and delete files given directory info.
          await Files.DeleteFilesAsync(directory, token).ConfigureAwait(false);

          // then do the actual delete.
          if (!await _foldersHelper.DeleteAsync(directory, token).ConfigureAwait(false))
          {
            _logger.Warning($"Could not delete folder: {directory.FullName}, does it still exist?");
          }
        }

        // touch all the folders as deleted.
        await FolderUpdates.TouchDirectoriesAsync(directories, UpdateType.Deleted, token).ConfigureAwait(false);

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

    /// <inheritdoc />
    public async Task<bool> DirectoryExistsAsync(DirectoryInfo directory, CancellationToken token)
    {
      return await GetDirectoryIdAsync(directory, token, false).ConfigureAwait(false) != -1;
    }

    /// <inheritdoc />
    public async Task<DirectoryInfo> GetDirectoryAsync(long directoryId, CancellationToken token)
    {
      try
      {
        // sanity check
        Contract.Assert(_foldersHelper != null);

        // return the valid paths.
        var path = await _foldersHelper.GetAsync(directoryId, token).ConfigureAwait(false);
        return null == path ? null : helper.File.DirectoryInfo( path, _logger );
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        return null;
      }
    }

    /// <inheritdoc />
    public async Task<List<long>> GetDirectoriesIdAsync(IReadOnlyCollection<DirectoryInfo> directories, CancellationToken token, bool createIfNotFound)
    {
      if (null == directories)
      {
        throw new ArgumentNullException(nameof(directories), "The given directory list is null");
      }

      if (!directories.Any())
      {
        return new List<long>();
      }

      // sanity check
      Contract.Assert(_foldersHelper != null);

      // the list of ids.
      var ids = new List<long>(directories.Count);
      var directoriesToAdd = new List<DirectoryInfo>();

      foreach (var directory in directories)
      {
        var id = await _foldersHelper.GetAsync(directory, token).ConfigureAwait(false);
        if (id != -1 )
        {
          // get the path id.
          ids.Add(id);
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

      // we will then try and add all the folsers in our list
      // and return whatever we found.
      ids.AddRange(await InsertDirectoriesAsync(directoriesToAdd, token).ConfigureAwait(false));

      // we are done
      return ids;
    }

    /// <inheritdoc />
    public async Task<long> GetDirectoryIdAsync(DirectoryInfo directory, CancellationToken token, bool createIfNotFound)
    {
      if (null == directory)
      {
        throw new ArgumentNullException(nameof(directory), "The given directory is null");
      }
      var ids = await GetDirectoriesIdAsync(new List<DirectoryInfo> { directory }, token, createIfNotFound).ConfigureAwait(false);
      return ids.Any() ? ids.First() : -1;
    }
    
    #region private functions
    /// <summary>
    /// Given a list of directories, re-create the ones that we need to insert.
    /// </summary>
    /// <param name="directories"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<List<long>> InsertDirectoriesAsync(IReadOnlyCollection<DirectoryInfo> directories, CancellationToken token)
    {
      // if we have nothing to do... we are done.
      if (!directories.Any())
      {
        return new List<long>();
      }

      // sanity check
      Contract.Assert(_foldersHelper != null);

      // all the ids we have just added.
      var ids = new List<long>(directories.Count);
      foreach (var directory in directories)
      {
        try
        {
          // get out if needed.
          token.ThrowIfCancellationRequested();

          var id = await _foldersHelper.InsertAndGetAsync( directory, token ).ConfigureAwait( false);
          if (-1 == id )
          {
            _logger.Error($"There was an issue finding the folder id: {directory.FullName} from persister");
            continue;
          }

          // add it to our list of ids.
          ids.Add(id);
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

      // then we can touch all the folders we created.
      await FolderUpdates.TouchDirectoriesAsync(ids, UpdateType.Created, token).ConfigureAwait(false);

      // return all the ids that we added.
      return ids;
    }

    /// <summary>
    /// Given a list of directories, re-create the ones that we need to insert.
    /// </summary>
    /// <param name="directories"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<List<DirectoryInfo>> RebuildDirectoriesListAsync(IEnumerable<DirectoryInfo> directories, CancellationToken token )
    {
      try
      {
        // sanity check
        Contract.Assert(_foldersHelper != null );

        // make that list unique
        var uniqueList = directories.Distinct(new DirectoryInfoComparer());

        // The list of directories we will be adding to the list.
        var actualDirectories = new List<DirectoryInfo>();
        foreach (var directory in uniqueList)
        {
          // get out if needed.
          token.ThrowIfCancellationRequested();

          // only valid paths are added.
          if (!directory.Exists)
          {
            continue;
          }

          // if the value exists, then we do not want to add this
          var id = await _foldersHelper.GetAsync( directory, token ).ConfigureAwait(false);
          if (id != -1)
          {
            continue;
          }

          // we could not find this path ... so just add it.
          // this item does not exist, so we have to add it.
          actualDirectories.Add(directory);
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
