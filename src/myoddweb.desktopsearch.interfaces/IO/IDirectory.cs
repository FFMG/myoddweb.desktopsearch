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

namespace myoddweb.desktopsearch.interfaces.IO
{
  public interface IDirectory
  {
    /// <summary>
    /// Parse a directory and look for sub folders.
    /// </summary>
    /// <param name="path">The start path</param>
    /// <param name="parseSubDirectory">Called when a directory is found, return true if we want to parse it further,</param>
    /// <param name="token">The cancelation token to cancel the runningtask.</param>
    /// <returns>success or false if the operation was cancelled.</returns>
    Task<bool> ParseDirectoriesAsync( string path, Func<DirectoryInfo, bool> parseSubDirectory, CancellationToken token );

    /// <summary>
    /// Parse all the files in a directory
    /// </summary>
    /// <param name="path"></param>
    /// <param name="actionFile">When a file is found, we will be calling this function.</param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> ParseDirectoryAsync(string path, Action<FileSystemInfo> actionFile, CancellationToken token);
  }
}