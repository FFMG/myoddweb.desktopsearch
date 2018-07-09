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
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.interfaces.Persisters
{
  public interface IFolders
  {
    /// <summary>
    /// Add or update an existing folder.
    /// </summary>
    /// <param name="directory"></param>
    /// <returns></returns>
    Task<bool> AddOrUpdateFolderAsync(DirectoryInfo directory);

    /// <summary>
    /// Update multiple directories at once.
    /// </summary>
    /// <param name="directories"></param>
    /// <returns></returns>
    Task<bool> AddOrUpdateFoldersAsync(IEnumerable<DirectoryInfo> directories);

    /// <summary>
    /// Delete a folder by its id
    /// </summary>
    /// <param name="folderId"></param>
    /// <returns></returns>
    Task<bool> DeleteFolderAsync(int folderId );

    /// <summary>
    /// Delete a folder by its path
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    Task<bool> DeleteFolderAsync(string path);
  }
}