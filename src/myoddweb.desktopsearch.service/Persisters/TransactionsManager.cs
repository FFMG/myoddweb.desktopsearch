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
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal class TransactionsManager
  {
    #region Member variables
    /// <summary>
    /// The function that will create the connection every time we need one.
    /// </summary>
    /// <param name="isReadOnly">If we want a readonly factory only.</param>
    /// <returns></returns>
    public delegate IConnectionFactory CreateFactory( bool isReadOnly );

    /// <summary>
    /// Our lock
    /// </summary>
    private readonly object _lock = new object();

    /// <summary>
    /// This is the write connection factory
    /// We can only have one at a time.
    /// </summary>
    private IConnectionFactory _writeFactory;

    private readonly CreateFactory _createFactory;
    #endregion

    public TransactionsManager(CreateFactory createFactory)
    {
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
      for (;;)
      {
        // wait for the transaction to no longer be null
        // outside of the lock, (so it can be freed.
        await helper.Wait.UntilAsync(() => _writeFactory == null, token).ConfigureAwait(false);

        // now trans and create the transaction
        lock (_lock)
        {
          // oops... we didn't get it.
          if (_writeFactory != null)
          {
            continue;
          }

          // create the connection
          // we were able to get a null transaction
          // ... and we are inside the lock
          _writeFactory = _createFactory( false );
          return _writeFactory;
        }
      }
    }

    /// <summary>
    /// Rollback a transaction
    /// </summary>
    /// <param name="connectionFactory"></param>
    public void Rollback(IConnectionFactory connectionFactory)
    {
      // we don't need the lock for readonly factories
      if (connectionFactory.IsReadOnly)
      {
        connectionFactory.Rollback();
        return;
      }

      lock (_lock)
      {
        // this is not a readonly transaction
        // so if it is not our factory
        // then we have no idea where it comes from.
        if (connectionFactory != _writeFactory)
        {
          throw new ArgumentException("The given transaction was not created by this class");
        }
      }

      if (null == _writeFactory)
      {
        return;
      }

      lock (_lock)
      {
        RollbackInLock();
      }
    }

    /// <summary>
    /// Rollback a transaction within a lock.
    /// </summary>
    private void RollbackInLock()
    {
      if (null == _writeFactory)
      {
        return;
      }

      // roll back
      _writeFactory.Rollback();

      // done  with the transaction
      _writeFactory = null;
    }

    /// <summary>
    /// Commit a transaction.
    /// </summary>
    /// <param name="connectionFactory"></param>
    public void Commit(IConnectionFactory connectionFactory )
    {
      // we don't need the lock for readonly
      if (connectionFactory.IsReadOnly)
      {
        connectionFactory.Commit();
        return;
      }

      lock (_lock)
      {
        // if this is not a readonly factory
        // and this is not our factory, then we do not know where it comes from.
        if (connectionFactory != _writeFactory)
        {
          throw new ArgumentException("The given transaction was not created by this class");
        }
      }

      if (null == _writeFactory)
      {
        return;
      }

      lock (_lock)
      {
        CommitInLock();
      }
    }

    /// <summary>
    /// Commit the tansaction while we are in lock.
    /// </summary>
    private void CommitInLock()
    {
      // do we actually have anything to do?
      if (null == _writeFactory)
      {
        return;
      }

      // commit 
      _writeFactory.Commit();

      // done  with the transaction
      _writeFactory = null;
    }
  }
}