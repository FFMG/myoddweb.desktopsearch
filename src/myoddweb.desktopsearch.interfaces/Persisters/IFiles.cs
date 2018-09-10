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
  public interface IFiles
  {
    /// <summary>
    /// Files update manager
    /// </summary>
    IFileUpdates FileUpdates { get; }

    /// <summary>
    /// Add or update a file to a folder.
    /// </summary>
    /// <param name="file"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> AddOrUpdateFileAsync( FileInfo file, IConnectionFactory connectionFactory, CancellationToken token);

    /// <summary>
    /// Add or update multiple files.
    /// </summary>
    /// <param name="files"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> AddOrUpdateFilesAsync(IReadOnlyList<FileInfo> files, IConnectionFactory connectionFactory, CancellationToken token);

    /// <summary>
    /// Rename a file
    /// </summary>
    /// <param name="file"></param>
    /// <param name="oldFile"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<long> RenameOrAddFileAsync(FileInfo file, FileInfo oldFile, IConnectionFactory transaction, CancellationToken token);

    /// <summary>
    /// Delete a single file.
    /// </summary>
    /// <param name="file"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> DeleteFileAsync(FileInfo file, IConnectionFactory connectionFactory, CancellationToken token);

    /// <summary>
    /// Delete multiple files.
    /// </summary>
    /// <param name="files"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> DeleteFilesAsync(IReadOnlyList<FileInfo> files, IConnectionFactory connectionFactory, CancellationToken token);

    /// <summary>
    /// Delete multiple files from a directory.
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> DeleteFilesAsync(DirectoryInfo directory, IConnectionFactory connectionFactory, CancellationToken token);

    /// <summary>
    /// Delete multiple files from a directory.
    /// </summary>
    /// <param name="directoryId"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> DeleteFilesAsync(long directoryId, IConnectionFactory connectionFactory, CancellationToken token);

    /// <summary>
    /// Check that we have the file on record.
    /// </summary>
    /// <param name="file"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> FileExistsAsync(FileInfo file, IConnectionFactory connectionFactory, CancellationToken token );

    /// <summary>
    /// Get all the files in the directory
    /// </summary>
    /// <param name="directoryId"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<List<FileInfo>> GetFilesAsync(long directoryId, IConnectionFactory connectionFactory, CancellationToken token);

    /// <summary>
    /// Get a file information
    /// </summary>
    /// <param name="fileId"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<FileInfo> GetFileAsync(long fileId, IConnectionFactory connectionFactory, CancellationToken token);

    /// <summary>
    /// Get the id of a file or -1.
    /// </summary>
    /// <param name="file"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <param name="createIfNotFound"></param>
    /// <returns></returns>
    Task<long> GetFileIdAsync(FileInfo file, IConnectionFactory connectionFactory, CancellationToken token, bool createIfNotFound);
  }
}