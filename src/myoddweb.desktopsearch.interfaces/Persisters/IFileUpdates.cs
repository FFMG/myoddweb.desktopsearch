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
using myoddweb.desktopsearch.interfaces.Enums;

namespace myoddweb.desktopsearch.interfaces.Persisters
{
  public interface IFileUpdates : ITransaction
  {
    /// <summary>
    /// Flag a file as having changed.
    /// </summary>
    /// <param name="file"></param>
    /// <param name="type"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> TouchFileAsync(FileInfo file, UpdateType type, CancellationToken token);

    /// <summary>
    /// Flag a file as having changed.
    /// </summary>
    /// <param name="fileId"></param>
    /// <param name="type"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> TouchFileAsync(long fileId, UpdateType type, CancellationToken token);

    /// <summary>
    /// Touch a list of files
    /// </summary>
    /// <param name="fileIds"></param>
    /// <param name="type"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> TouchFilesAsync( IEnumerable<long> fileIds, UpdateType type, CancellationToken token);

    /// <summary>
    /// Flag that we have processed the given file
    /// </summary>
    /// <param name="file"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> MarkFileProcessedAsync(FileInfo file, CancellationToken token);

    /// <summary>
    /// Flag that we have processed the given file
    /// </summary>
    /// <param name="fileId"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> MarkFileProcessedAsync(long fileId, CancellationToken token);

    /// <summary>
    /// Flag that we have processed the given file
    /// </summary>
    /// <param name="fileIds"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> MarkFilesProcessedAsync(IEnumerable<long> fileIds, CancellationToken token);

    /// <summary>
    /// Get a number of pending updates.
    /// </summary>
    /// <param name="limit">The maximum number of pending updates we are looking for.</param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<IList<IPendingFileUpdate>> GetPendingFileUpdatesAsync(long limit, IConnectionFactory connectionFactory, CancellationToken token);
  }
}