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
using System.Threading;
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.interfaces.Persisters
{
  public interface ICounts : ITransaction
  {
    /// <summary>
    /// Start the counter.
    /// <param name="token"></param>
    /// </summary>
    Task Initialise( CancellationToken token);

    /// <summary>
    /// Get the pending update count.
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<long> GetPendingUpdatesCountAsync( CancellationToken token);

    /// <summary>
    /// Get the pending update count.
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<long> GetFilesCountAsync( CancellationToken token);

    /// <summary>
    /// Update the number of pending updates
    /// </summary>
    /// <param name="addOrRemove"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> UpdatePendingUpdatesCountAsync( long addOrRemove, CancellationToken token);

    /// <summary>
    /// Update the number of files
    /// </summary>
    /// <param name="addOrRemove"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> UpdateFilesCountAsync(long addOrRemove, CancellationToken token);
  }
}