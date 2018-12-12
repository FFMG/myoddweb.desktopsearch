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
using System;
using System.Threading;
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.helper.Lock
{
  public class Lock
  {
    #region Member Variables

    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0, 1);
    #endregion

    public Lock()
    {
    }
    
    public Task<IDisposable> TryAsync()
    {
      var key = new Key( this );
      return key.TryAsync();
    }

    public IDisposable Try()
    {
      var key = new Key(this);
      return key.TryAsync().GetAwaiter().GetResult();
    }

    internal async Task EnterAsync(long id)
    {
      await _semaphore.WaitAsync().ConfigureAwait(false);
    }

    internal void Exit( long id )
    {
      _semaphore.Release();
    }
  }
}
