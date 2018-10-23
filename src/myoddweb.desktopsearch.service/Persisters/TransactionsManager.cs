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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Configs;
using myoddweb.desktopsearch.interfaces.Persisters;
using ILogger = myoddweb.desktopsearch.interfaces.Logging.ILogger;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal class TransactionPerformanceCounter : helper.IO.PerformanceCounter
  {
    /// <summary>
    /// The performance counter
    /// </summary>
    private readonly PerformanceCounter _counter;

    public TransactionPerformanceCounter(IPerformance performance, string counterName, ILogger logger)
    {
      // now create the counters.
      _counter = CreatePerformanceCounter(performance, counterName, PerformanceCounterType.RateOfCountsPerSecond32, logger);
    }

    /// <inheritdoc/>
    protected override void OnIncremenFromUtcTime(DateTime startTime)
    {
      throw new NotImplementedException();
    }

    /// <inheritdoc/>
    protected override void OnIncrement()
    {
      _counter.Increment();
    }

    /// <inheritdoc/>
    protected override void OnDispose()
    {
      _counter.Dispose();
    }
  }

  internal class TransactionsManager : IDisposable
  {
    #region Member variables

    private readonly TransactionPerformanceCounter _counterBegin;
    private readonly TransactionPerformanceCounter _counterCommit;
    private readonly TransactionPerformanceCounter _counterRollback;

    /// <summary>
    /// The function that will create the connection every time we need one.
    /// </summary>
    /// <param name="isReadOnly">If we want a readonly factory only.</param>
    /// <returns></returns>
    public delegate IConnectionFactory CreateFactory( bool isReadOnly );

    /// <summary>
    /// This is the write connection factory
    /// We can only have one at a time.
    /// </summary>
    private IConnectionFactory _writeFactory;

    /// <summary>
    /// Our lock object
    /// </summary>
    private readonly object _lock = new object(); 

    private readonly CreateFactory _createFactory;
    #endregion

    public TransactionsManager(CreateFactory createFactory, IPerformance performance, ILogger logger )
    {
      _counterBegin = new TransactionPerformanceCounter( performance, "Database: Begin", logger );
      _counterCommit = new TransactionPerformanceCounter(performance, "Database: Commit", logger);
      _counterRollback = new TransactionPerformanceCounter(performance, "Database: Rollback", logger);

      _createFactory = createFactory ?? throw new ArgumentNullException(nameof(createFactory));
    }

    /// <summary>
    /// Begin a read transaction.
    /// There can be more than one.
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task<IConnectionFactory> BeginRead(CancellationToken token)
    {
      // update the counter.
      _counterBegin.Increment();

      // get out if needed.
      token.ThrowIfCancellationRequested();

      // create the transaction
      var trans = _createFactory( true );

      // return our created factory.
      return Task.FromResult(trans);
    }

    /// <summary>
    /// Begin a write transaction.
    /// There can only be one
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<IConnectionFactory> BeginWrite( CancellationToken token )
    {
      // update the counter.
      _counterBegin.Increment();

      for (;;)
      {
        // wait for the transaction to no longer be null
        // outside of the lock, (so it can be freed.
        await helper.Wait.UntilAsync(() => _writeFactory == null, token).ConfigureAwait(false);

        // now trans and create the transaction
        var lockTaken = false;
        try
        {
          Monitor.Enter(_lock, ref lockTaken);

          // oops... we didn't really get it.
          // go around again to make sure.
          if (_writeFactory != null)
          {
            continue;
          }

          // create the connection
          // we were able to get a null transaction
          // ... and we are inside the lock
          _writeFactory = _createFactory(false);
          return _writeFactory;
        }
        finally 
        {
          if (lockTaken)
          {
            Monitor.Exit( _lock );
          }
        }
      }
    }

    /// <summary>
    /// Rollback a transaction
    /// </summary>
    /// <param name="connectionFactory"></param>
    public void Rollback(IConnectionFactory connectionFactory)
    {
      // update the counter.
      _counterRollback.Increment();

      // we don't need the lock for readonly factories
      if (connectionFactory.IsReadOnly)
      {
        connectionFactory.Rollback();
        return;
      }

      var lockTaken = false;
      try
      {
        Monitor.Enter(_lock, ref lockTaken);
        // this is not a readonly transaction
        // so if it is not our factory
        // then we have no idea where it comes from.
        if (connectionFactory != _writeFactory)
        {
          throw new ArgumentException("The given transaction was not created by this class");
        }
        RollbackInLock();
      }
      finally
      {
        if (lockTaken)
        {
          Monitor.Exit(_lock);
        }
      }
    }

    /// <summary>
    /// Commit a transaction.
    /// </summary>
    /// <param name="connectionFactory"></param>
    public void Commit(IConnectionFactory connectionFactory )
    {
      // update the counter.
      _counterCommit.Increment();

      // we don't need the lock for readonly
      if (connectionFactory.IsReadOnly)
      {
        connectionFactory.Commit();
        return;
      }

      var lockTaken = false;
      try
      {
        Monitor.Enter(_lock, ref lockTaken);
        // if this is not a readonly factory
        // and this is not our factory, then we do not know where it comes from.
        if (connectionFactory != _writeFactory)
        {
          throw new ArgumentException("The given transaction was not created by this class");
        }
        CommitInLock();
      }
      finally
      {
        if (lockTaken)
        {
          Monitor.Exit(_lock);
        }
      }
    }

    /// <summary>
    /// Rollback a transaction within a lock.
    /// </summary>
    private void RollbackInLock()
    {
      try
      {
        // try and roll back
        _writeFactory?.Rollback();

      }
      finally
      {
        // whatever happens, we are done with the transaction
        // it is up to the factory to catch/handle any issues.
        _writeFactory = null;
      }
    }

    /// <summary>
    /// Commit the tansaction while we are in lock.
    /// </summary>
    private void CommitInLock()
    {
      try
      {
        // try and commit 
        _writeFactory?.Commit();
      }
      finally 
      {
        // whatever happens, we are done with the transaction
        // it is up to the factory to catch/handle any issues.
        _writeFactory = null;
      }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
      _counterBegin?.Dispose();
      _counterCommit?.Dispose();
      _counterRollback?.Dispose();
      _writeFactory = null;
    }
  }
}
