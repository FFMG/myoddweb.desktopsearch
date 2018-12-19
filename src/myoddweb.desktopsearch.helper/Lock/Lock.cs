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

namespace myoddweb.desktopsearch.helper.Lock
{
  public class Lock
  {
    #region Member Variables
    private const long NoOwner = -1;

    private readonly object _lock = new object();
    private long _owningId = NoOwner;
    #endregion

    /// <summary>
    /// Try and get the lock async
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task<IDisposable> TryAsync( CancellationToken token)
    {
      var key = new Key( this );
      return key.TryAsync( token );
    }

    /// <summary>
    /// Try and get the lock, (non-async)
    /// </summary>
    /// <returns></returns>
    public IDisposable Try()
    {
      var key = new Key(this);
      return key.TryAsync( default(CancellationToken) ).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Enter the locks.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    internal async Task EnterAsync(long id, CancellationToken token)
    {
      // wait until we can get the lock
      // we do not want to hold the lock
      // so the lock ownwe can release it.
      await Wait.UntilAsync(  () =>
      {
        lock (_lock)
        {
          if (_owningId != NoOwner)
          {
            return false;
          }

          // we are the owner.
          _owningId = id;
          return true;
        }
      }, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Exit/Release the lock
    /// </summary>
    /// <param name="id"></param>
    internal void Exit( long id )
    {
      lock (_lock)
      {
        if (_owningId != id)
        {
          throw new ArgumentException($"An id: {id} is trying to release a lock it does not have.");
        }
        _owningId = NoOwner;
      }
    }
  }
}
