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
    /// Add or update an existing folder.
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> AddOrUpdateFolderAsync(DirectoryInfo directory, CancellationToken token);

    /// <summary>
    /// Update multiple directories at once.
    /// </summary>
    /// <param name="directories"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> AddOrUpdateFoldersAsync(IEnumerable<DirectoryInfo> directories, CancellationToken token);

    /// <summary>
    /// Delete a single folder.
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> DeleteFolderAsync(DirectoryInfo directory, CancellationToken token);

    /// <summary>
    /// Delete multiple folders.
    /// </summary>
    /// <param name="directories"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> DeleteFoldersAsync(IEnumerable<DirectoryInfo> directories, CancellationToken token);
  }
}