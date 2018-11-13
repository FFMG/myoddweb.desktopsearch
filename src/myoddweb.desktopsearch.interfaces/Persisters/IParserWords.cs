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
using System.Threading;
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.interfaces.Persisters
{
  public interface IParserWords
  {
    /// <summary>
    /// The name of the words table
    /// </summary>
    string TableWordName { get; }

    /// <summary>
    /// The name of the words table
    /// </summary>
    string TableFilesName { get; }

    /// <summary>
    /// Add a word to the list of words for that file id.
    /// </summary>
    /// <param name="fileid"></param>
    /// <param name="words"></param>
    /// <param name="wordsHelper"></param>
    /// <param name="filesWordsHelper"></param>
    /// <param name="parserWordsHelper"></param>
    /// <param name="parserFilesWordsHelper"></param>
    /// <param name="token"></param>
    /// <returns>The number of words that were added.</returns>
    Task<long> AddWordsAsync( 
      long fileid, 
      IReadOnlyList<string> words, 
      IWordsHelper wordsHelper, 
      IFilesWordsHelper filesWordsHelper,
      IParserWordsHelper parserWordsHelper,
      IParserFilesWordsHelper parserFilesWordsHelper,
      CancellationToken token);

    /// <summary>
    /// Delete file ids for a word id
    /// </summary>
    /// <param name="wordId"></param>
    /// <param name="fileIds"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> DeleteFileIds(long wordId, IList<long> fileIds, IConnectionFactory connectionFactory, CancellationToken token);

    /// <summary>
    /// Delete all the words for a file id.
    /// </summary>
    /// <param name="fileId"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> DeleteFileId( long fileId, IConnectionFactory connectionFactory, CancellationToken token);

    /// <summary>
    /// Delete a single word id
    /// </summary>
    /// <param name="wordids"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> DeleteWordIds(IList<long> wordids, IConnectionFactory connectionFactory, CancellationToken token);

    /// <summary>
    /// Get the id of pending file 
    /// </summary>
    /// <param name="limit">How many updates we want to work with.</param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<IList<IPendingParserWordsUpdate>> GetPendingParserWordsUpdatesAsync(long limit, IConnectionFactory connectionFactory, CancellationToken token);
  }
}