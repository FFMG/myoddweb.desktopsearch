using System;
using System.Threading;
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.helper.Lock
{
  internal class Key : IDisposable
  {
    #region Member Variables
    private readonly Lock _parent;

    private readonly long _id = (long) (((ulong) Thread.CurrentThread.ManagedThreadId) << 32) | ((uint) (Task.CurrentId ?? 0));
    #endregion

    public Key(Lock parent)
    {
      _parent = parent;
    }

    public void Dispose()
    {
      _parent.Exit(_id);
    }

    public async Task<IDisposable> TryAsync()
    {
      await _parent.EnterAsync(Task.CurrentId ?? 0).ConfigureAwait(false);
      return this;
    }
  }

  public class Lock
  {
    #region Member Variables
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    private readonly object _counterLock = new object();

    private int _counter;
    #endregion

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
      // get the lock
      await _semaphore.WaitAsync().ConfigureAwait( false );

      // we are in, so increase the count.
      lock (_counterLock)
      {
        ++_counter;
      }
    }

    internal void Exit( long id )
    {
      // get the lock and release if need be.
      lock (_counterLock)
      {
        if (_counter == 0)
        {
          throw new InvalidOperationException( "Error trying to release the lock already released." );
        }
        --_counter;

        // release the lock if we are the last one.
        if (_counter == 0)
        {
          _semaphore.Release();
        }
      }
    }
  }
}
