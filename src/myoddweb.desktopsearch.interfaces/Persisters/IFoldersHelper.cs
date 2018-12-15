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
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.interfaces.Persisters
{
  public interface IFoldersHelper : IDisposable
  {
    /// <summary>
    /// Get the id of an item.
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<long> GetAsync(DirectoryInfo directory, CancellationToken token);

    /// <summary>
    /// Get the path of an item, given the id.
    /// </summary>
    /// <param name="directoryId"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<string> GetAsync(long directoryId, CancellationToken token);

    /// <summary>
    /// Insert a single path and get the corresponding Id, (or -1)
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<long> InsertAndGetAsync(DirectoryInfo directory, CancellationToken token);

    /// <summary>
    /// Rename an old directory.
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="oldDirectory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> RenameAsync(DirectoryInfo directory, DirectoryInfo oldDirectory, CancellationToken token);

    /// <summary>
    /// Delete a folder.
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> DeleteAsync(DirectoryInfo directory, CancellationToken token);
  }
}