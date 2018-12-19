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
  internal class Key : IDisposable
  {
    #region Member Variables
    private readonly Lock _parent;
    /// <summary>
    /// Get a unique Id ... it is kind of unique.
    /// If the process is truelly syncronious then we will allow the lock to re-enter.
    /// </summary>
    private readonly long _id = (long)(((ulong)Thread.CurrentThread.ManagedThreadId) << 32) | ((uint)(Task.CurrentId ?? 0));
    #endregion

    public Key(Lock parent)
    {
      _parent = parent;
    }

    public void Dispose()
    {
      _parent.Exit(_id);
    }

    /// <summary>
    /// Try and enter the lock and return ourselves as an IDisposable.
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<IDisposable> TryAsync(CancellationToken token)
    {
      await _parent.EnterAsync(_id, token).ConfigureAwait(false);
      return this;
    }
  }
}
