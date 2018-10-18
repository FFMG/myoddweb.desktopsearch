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
  public interface IParserWords
  {
    /// <summary>
    /// Add a word to the list of words for that file id.
    /// </summary>
    /// <param name="fileid"></param>
    /// <param name="words"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns>The number of words that were added.</returns>
    Task<long> AddWordAsync( long fileid, IReadOnlyList<string> words, IConnectionFactory connectionFactory, CancellationToken token);

    /// <summary>
    /// Delete all the words for a file id.
    /// </summary>
    /// <param name="fileid"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> DeleteFileId(long fileid, IConnectionFactory connectionFactory, CancellationToken token);

    /// <summary>
    /// Delete a single word id
    /// </summary>
    /// <param name="wordid"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> DeleteWordIdFileId(long wordid, IConnectionFactory connectionFactory, CancellationToken token);

    /// <summary>
    /// Get words for processing.
    /// </summary>
    /// <param name="fileid">The file we are wanting to process.</param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<IO.IWords> GetWordsForProcessingAsync(long fileid, IConnectionFactory connectionFactory, CancellationToken token);

    /// <summary>
    /// Get the id of pending file 
    /// </summary>
    /// <param name="count">How many ids we would like to get.</param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<IList<long>> GetPendingFileIdsAsync( int count, IConnectionFactory connectionFactory, CancellationToken token);
  }
}