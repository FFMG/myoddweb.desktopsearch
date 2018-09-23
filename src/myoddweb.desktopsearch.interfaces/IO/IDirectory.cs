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

namespace myoddweb.desktopsearch.interfaces.IO
{
  public interface IDirectory
  {
    /// <summary>
    /// Parse a directory and look for sub folders.
    /// </summary>
    /// <param name="path">The start path</param>
    /// <param name="token">The cancelation token to cancel the runningtask.</param>
    /// <returns>success or false if the operation was cancelled.</returns>
    Task<IList<DirectoryInfo>> ParseDirectoriesAsync( DirectoryInfo path, CancellationToken token );

    /// <summary>
    /// Parse all the files in a directory
    /// </summary>
    /// <param name="path"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<IList<FileInfo>> ParseDirectoryAsync(DirectoryInfo path, CancellationToken token);

    /// <summary>
    /// Check if the given directory is ignored or not.
    /// </summary>
    /// <param name="directory"></param>
    /// <returns></returns>
    bool IsIgnored(DirectoryInfo directory);

    /// <summary>
    /// Check if the given file is ignored or not.
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    bool IsIgnored(FileInfo file);
  }
}