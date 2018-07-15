﻿//This file is part of Myoddweb.DesktopSearch.
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
using System.Data.Common;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.interfaces.Persisters
{
  public interface IFiles
  {
    /// <summary>
    /// Add or update a file to a folder.
    /// </summary>
    /// <param name="file"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> AddOrUpdateFileAsync( FileInfo file, DbTransaction transaction, CancellationToken token);

    /// <summary>
    /// Add or update multiple files.
    /// </summary>
    /// <param name="files"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> AddOrUpdateFilesAsync(IReadOnlyList<FileInfo> files, DbTransaction transaction, CancellationToken token);

    /// <summary>
    /// Rename a file
    /// </summary>
    /// <param name="file"></param>
    /// <param name="oldFile"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<long> RenameOrAddFileAsync(FileInfo file, FileInfo oldFile, DbTransaction transaction, CancellationToken token);

    /// <summary>
    /// Delete a single file.
    /// </summary>
    /// <param name="file"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> DeleteFileAsync(FileInfo file, DbTransaction transaction, CancellationToken token);

    /// <summary>
    /// Delete multiple files.
    /// </summary>
    /// <param name="files"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> DeleteFilesAsync(IReadOnlyList<FileInfo> files, DbTransaction transaction, CancellationToken token);
  }
}