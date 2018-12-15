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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.interfaces.Persisters
{
  public interface IWordsPartsHelper : IDisposable
  {
    /// <summary>
    /// Check if a wordid/partid exists.
    /// </summary>
    /// <param name="wordId"></param>
    /// <param name="partIds"></param>
    /// <param name="token"></param>
    /// <returns>The list of ids that do not exist</returns>
    Task<IList<long>> ExistsAsync(long wordId, IReadOnlyCollection<long> partIds, CancellationToken token);

    /// <summary>
    /// Get all the parts that are linked to a word.
    /// </summary>
    /// <param name="wordid"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<IList<long>> GetPartIdsAsync(long wordid, CancellationToken token);

    /// <summary>
    /// Insert all the parts of a word.
    /// Return the ids that do not exist.
    /// </summary>
    /// <param name="wordId"></param>
    /// <param name="partIds"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<IList<long>> InsertAsync(long wordId, IReadOnlyCollection<long> partIds, CancellationToken token);

    /// <summary>
    /// Delete a word/part id.
    /// </summary>
    /// <param name="wordId"></param>
    /// <param name="partId"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> DeleteAsync(long wordId, long partId, CancellationToken token);
  }
}