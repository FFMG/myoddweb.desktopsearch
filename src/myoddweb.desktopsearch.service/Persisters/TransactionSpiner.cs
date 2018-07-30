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
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal class TransactionSpiner
  {
    public delegate IDbConnection CreateConnection();
    public delegate void FreeResources(IDbConnection connection);

    private readonly object _lock = new object();
    private IDbConnection _connection;
    private IDbTransaction _transaction;
    private readonly CreateConnection _createConnection;
    private readonly FreeResources _freeResources;
    
    public TransactionSpiner(CreateConnection createConnection, FreeResources freeResources )
    {
      _createConnection = createConnection ?? throw new ArgumentNullException(nameof(createConnection));
      _freeResources = freeResources ?? throw new ArgumentNullException(nameof(freeResources));
    }

    public async Task<IDbTransaction> Begin( CancellationToken token )
    {
      for (; ; )
      {
        // wait for the transaction to no longer be null
        // outside of the lock, (so it can be freed.
        if (null != _transaction)
        {
          await Task.Run(() => SpinWait.SpinUntil(() => _transaction == null || token.IsCancellationRequested), token).ConfigureAwait( false );
        }

        if (token.IsCancellationRequested)
        {
          return null;
        }

        // now trans and create the transaction
        lock (_lock)
        {
          // oops... we didn't get it.
          if (_transaction != null)
          {
            continue;
          }

          // create the connection
          _connection = _createConnection();

          // we were able to get a null transaction
          // ... and we are inside the lock
          _transaction = _connection.BeginTransaction();
          return _transaction;
        }
      }
    }

    public void Rollback(IDbTransaction transaction)
    {
      lock (_lock)
      {
        if (transaction != _transaction)
        {
          throw new ArgumentException("The given transaction was not created by this class");
        }
      }

      if (null == _transaction)
      {
        return;
      }

      lock (_lock)
      {
        if (null == _transaction)
        {
          return;
        }
        // roll back
        _transaction.Rollback();

        // free resources
        _freeResources( _connection );

        _transaction = null;
        _connection = null;
      }
    }

    public void Commit(IDbTransaction transaction)
    {
      lock (_lock)
      {
        if (transaction != _transaction)
        {
          throw new ArgumentException("The given transaction was not created by this class");
        }
      }

      if (null == _transaction)
      {
        return;
      }

      lock (_lock)
      {
        if (null == _transaction)
        {
          return;
        }

        // commit 
        _transaction.Commit();

        // free resources
        _freeResources(_connection);

        // done 
        _transaction = null;
        _connection = null;
      }
    }
  }
}
