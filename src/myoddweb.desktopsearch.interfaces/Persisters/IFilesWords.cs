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
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.interfaces.Persisters
{
  public interface IFilesWords
  {
    /// <summary>
    /// Add 
    /// </summary>
    /// <param name="word"></param>
    /// <param name="fileId"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> AddOrUpdateWordToFileAsync(string word, long fileId, IDbTransaction transaction, CancellationToken token);

    /// <summary>
    /// Add multiple words to a single file id
    /// </summary>
    /// <param name="words"></param>
    /// <param name="fileId"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> AddOrUpdateWordsToFileAsync(IReadOnlyList<string> words, long fileId, IDbTransaction transaction, CancellationToken token);

    /// <summary>
    /// Remove a file id from the FilesWords list.
    /// </summary>
    /// <param name="fileId"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> DeleteFileFromFilesAndWordsAsync( long fileId, IDbTransaction transaction, CancellationToken token);
  }
}
