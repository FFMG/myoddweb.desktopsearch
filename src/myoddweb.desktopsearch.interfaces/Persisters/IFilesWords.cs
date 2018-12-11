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
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.IO;

namespace myoddweb.desktopsearch.interfaces.Persisters
{
  public interface IFilesWords : ITransaction
  {
    /// <summary>
    /// Get the table name.
    /// </summary>
    string TableName { get; }

    /// <summary>
    /// Add a word to a list of files.
    /// </summary>
    /// <param name="wordToAdd">The word we want to add to the list of words.</param>
    /// <param name="fileIdsToAddWordTo">Once the word is added, the files we will add it to.</param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> AddWordToFilesAsync( IWord wordToAdd, IList<long> fileIdsToAddWordTo, CancellationToken token);

    /// <summary>
    /// Add multiple words to a single file id
    /// </summary>
    /// <param name="wordId">The word id we want to add this to.</param>
    /// <param name="fileIdsToAddWordTo">Once the word is added, the files we will add it to.</param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> AddWordToFilesAsync(long wordId, IList<long> fileIdsToAddWordTo, CancellationToken token);

    /// <summary>
    /// Remove a file id from the FilesWords table.
    /// So when we are looking for a word, by id, this file will not come up.
    /// </summary>
    /// <param name="fileId"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> DeleteFileAsync( long fileId, CancellationToken token);
  }
}
