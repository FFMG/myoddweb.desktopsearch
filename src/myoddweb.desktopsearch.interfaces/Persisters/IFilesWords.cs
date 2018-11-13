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

namespace myoddweb.desktopsearch.interfaces.Persisters
{
  public interface IFilesWords
  {
    /// <summary>
    /// Get the table name.
    /// </summary>
    string TableName { get; }

    /// <summary>
    /// Add multiple words to a single file id
    /// </summary>
    /// <param name="wordsHelper"></param>
    /// <param name="fileWordsHelper"></param>
    /// <param name="partsHelper"></param>
    /// <param name="wordsPartsHelper"></param>
    /// <param name="pendingUpdates">The words we want to add to the list of words./</param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> AddParserWordsAsync(
      IPendingParserWordsUpdate pendingUpdate,
      IWordsHelper wordsHelper,
      IFilesWordsHelper fileWordsHelper,
      IPartsHelper partsHelper,
      IWordsPartsHelper wordsPartsHelper,
      CancellationToken token);

    /// <summary>
    /// Remove a file id from the FilesWords table.
    /// So when we are looking for a word, by id, this file will not come up.
    /// </summary>
    /// <param name="fileId"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> DeleteWordsAsync( long fileId, IConnectionFactory connectionFactory, CancellationToken token);
  }
}
