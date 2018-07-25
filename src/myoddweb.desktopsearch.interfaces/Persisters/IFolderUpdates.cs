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
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.interfaces.Persisters
{
  public interface IFolderUpdates
  {
    /// <summary>
    /// Flag a folder as having changed.
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="type"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> TouchDirectoryAsync(DirectoryInfo directory, UpdateType type, IDbTransaction transaction, CancellationToken token);

    /// <summary>
    /// Flag a folder as having changed.
    /// </summary>
    /// <param name="folderId"></param>
    /// <param name="type"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> TouchDirectoryAsync(long folderId, UpdateType type, IDbTransaction transaction, CancellationToken token);

    /// <summary>
    /// Flag that we have processed the given directory
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> MarkDirectoryProcessedAsync(DirectoryInfo directory, IDbTransaction transaction, CancellationToken token);

    /// <summary>
    /// Flag that we have processed the given directory
    /// </summary>
    /// <param name="folderId"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> MarkDirectoryProcessedAsync(long folderId, IDbTransaction transaction, CancellationToken token);

    /// <summary>
    /// Flag that we have processed the given directory
    /// </summary>
    /// <param name="folderIds"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> MarkDirectoriesProcessedAsync(IEnumerable<long> folderIds, IDbTransaction transaction, CancellationToken token);

    /// <summary>
    /// Get a number of pending updates.
    /// </summary>
    /// <param name="limit">The maximum number of pending updates we are looking for.</param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<List<PendingFolderUpdate>> GetPendingFolderUpdatesAsync( long limit, IDbTransaction transaction, CancellationToken token);
  }
}