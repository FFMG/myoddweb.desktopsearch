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
    #region Member variables
    /// <summary>
    /// The function that will create the connection every time we need one.
    /// </summary>
    /// <returns></returns>
    public delegate IDbConnection CreateConnection();

    /// <summary>
    /// The function that will dispose of the reources.
    /// </summary>
    public delegate void FreeResources();

    /// <summary>
    /// Our lock
    /// </summary>
    private readonly object _lock = new object();

    private IDbTransaction _transaction;
    private readonly CreateConnection _createConnection;
    private readonly FreeResources _freeResources;
    #endregion

    public TransactionSpiner(CreateConnection createConnection, FreeResources freeResources )
    {
      _createConnection = createConnection ?? throw new ArgumentNullException(nameof(createConnection));
      _freeResources = freeResources ?? throw new ArgumentNullException(nameof(freeResources));
    }
    
    /// <summary>
    /// Begin a transaction.
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<IDbTransaction> Begin( CancellationToken token )
    {
      for (;;)
      {
        // get out if needed.
        token.ThrowIfCancellationRequested();

        // wait for the transaction to no longer be null
        // outside of the lock, (so it can be freed.
        await helper.Wait.UntilAsync(() => _transaction == null, token).ConfigureAwait(false);

        // now trans and create the transaction
        lock (_lock)
        {
          // oops... we didn't get it.
          if (_transaction != null)
          {
            continue;
          }

          // create the connection
          var connection = _createConnection();

          // we were able to get a null transaction
          // ... and we are inside the lock
          _transaction = connection.BeginTransaction();
          return _transaction;
        }
      }
    }

    /// <summary>
    /// Rollback a transaction
    /// </summary>
    /// <param name="transaction"></param>
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
        _freeResources( );

        // done  with the transaction
        _transaction = null;
      }
    }

    /// <summary>
    /// Commit a transaction.
    /// </summary>
    /// <param name="transaction"></param>
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
        _freeResources();

        // done  with the transaction
        _transaction = null;
      }
    }
  }
}
