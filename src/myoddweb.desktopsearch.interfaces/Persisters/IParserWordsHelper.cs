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
using System.Threading;
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.interfaces.Persisters
{
  public interface IParserWordsHelper : IDisposable
  {
    /// <summary>
    /// Get the id for a given word or -1 if the word does not exist.
    /// </summary>
    /// <param name="word"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<long> GetIdAsync(string word, CancellationToken token);

    /// <summary>
    /// Insert a word and return the ID.
    /// </summary>
    /// <param name="word"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<long> InsertAsync(string word, CancellationToken token);

    /// <summary>
    /// Delete a word by id.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> DeleteWordAsync( long id, CancellationToken token);
  }
}
