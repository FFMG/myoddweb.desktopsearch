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
  public interface IWords
  {
    /// <summary>
    /// The name of the words table
    /// </summary>
    string TableName { get; }

    /// <summary>
    /// Add or update a single word.
    /// </summary>
    /// <param name="wordsHelper"></param>
    /// <param name="partsHelper"></param>
    /// <param name="word"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<long> AddOrUpdateWordAsync(
      IWordsHelper wordsHelper,
      IPartsHelper partsHelper,
      IWord word, 
      IConnectionFactory connectionFactory, 
      CancellationToken token);

    /// <summary>
    /// Add or update multiple word.
    /// </summary>
    /// <param name="wordsHelper"></param>
    /// <param name="partsHelper"></param>
    /// <param name="words"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<IList<long>> AddOrUpdateWordsAsync(
      IWordsHelper wordsHelper,
      IPartsHelper partsHelper,
      IO.IWords words, 
      IConnectionFactory connectionFactory, 
      CancellationToken token);
  }
}
