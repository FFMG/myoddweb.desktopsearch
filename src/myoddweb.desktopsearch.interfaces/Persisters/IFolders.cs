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
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.interfaces.Persisters
{
  public interface IFolders
  {
    /// <summary>
    /// The files interface
    /// </summary>
    IFiles Files { get; }

    /// <summary>
    /// The folders update manager.
    /// </summary>
    IFolderUpdates FolderUpdates { get; }

    /// <summary>
    /// rename or add an existing folder.
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="oldDirectory"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<long> RenameOrAddDirectoryAsync(DirectoryInfo directory, DirectoryInfo oldDirectory, IConnectionFactory connectionFactory, CancellationToken token);

    /// <summary>
    /// Add or update an existing directory.
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> AddOrUpdateDirectoryAsync(DirectoryInfo directory, IConnectionFactory connectionFactory, CancellationToken token);

    /// <summary>
    /// Update multiple directories at once.
    /// </summary>
    /// <param name="directories"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> AddOrUpdateDirectoriesAsync(IReadOnlyList<DirectoryInfo> directories, IConnectionFactory connectionFactory, CancellationToken token);

    /// <summary>
    /// Delete a single folder.
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> DeleteDirectoryAsync(DirectoryInfo directory, IConnectionFactory connectionFactory, CancellationToken token);

    /// <summary>
    /// Delete multiple folders.
    /// </summary>
    /// <param name="directories"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> DeleteDirectoriesAsync(IReadOnlyList<DirectoryInfo> directories, IConnectionFactory connectionFactory, CancellationToken token);

    /// <summary>
    /// Check that we have the directory on record.
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> DirectoryExistsAsync(DirectoryInfo directory, IConnectionFactory connectionFactory, CancellationToken token);

    /// <summary>
    /// Get the directory info if posible, otherwise return null if we have no record of it.
    /// </summary>
    /// <param name="directoryId"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<DirectoryInfo> GetDirectoryAsync(long directoryId, IConnectionFactory connectionFactory, CancellationToken token);

    /// <summary>
    /// Get the id of a list of directories
    /// </summary>
    /// <param name="directories"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <param name="createIfNotFound"></param>
    /// <returns></returns>
    Task<List<long>> GetDirectoriesIdAsync(IReadOnlyCollection<DirectoryInfo> directories, IConnectionFactory connectionFactory, CancellationToken token, bool createIfNotFound);

    /// <summary>
    /// Get the id of a folder or -1.
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <param name="createIfNotFound"></param>
    /// <returns></returns>
    Task<long> GetDirectoryIdAsync(DirectoryInfo directory, IConnectionFactory connectionFactory,CancellationToken token, bool createIfNotFound);
  }
}