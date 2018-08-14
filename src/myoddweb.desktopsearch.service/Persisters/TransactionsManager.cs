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
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.service.Persisters
{
  /// <summary>
  /// Class to manage readonly transactions.
  /// </summary>
  internal class TransactionsManagerReadOnly : IDbTransaction
  {
    /// <summary>
    /// The parent transaction.
    /// </summary>
    public IDbTransaction Transaction { get; }

    public TransactionsManagerReadOnly(IDbTransaction transaction)
    {
      Transaction = transaction;
    }

    #region IDBTransaction
    public void Dispose()
    {
      Transaction?.Dispose();
    }

    public void Commit()
    {
      throw new InvalidOperationException("You cannot commit or rollback this ...");
    }

    public void Rollback()
    {
      throw new InvalidOperationException( "You cannot commit or rollback this ...");
    }

    public IDbConnection Connection => Transaction.Connection;
    public IsolationLevel IsolationLevel => Transaction.IsolationLevel;
    #endregion
  }

  internal class TransactionsManager
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
    private readonly List<TransactionsManagerReadOnly> _readonlyTransactions = new List<TransactionsManagerReadOnly>();

    private readonly CreateConnection _createConnection;
    private readonly FreeResources _freeResources;

    private enum TransactionOwner
    {
      None,
      ReadyToRollBack,
      ReadyToCommit,
      ReadonlyIsOwner
    }

    private TransactionOwner _transactionOwner;
    #endregion

    public TransactionsManager(CreateConnection createConnection, FreeResources freeResources)
    {
      _createConnection = createConnection ?? throw new ArgumentNullException(nameof(createConnection));
      _freeResources = freeResources ?? throw new ArgumentNullException(nameof(freeResources));
    }

    /// <summary>
    /// Begin a read transaction.
    /// There can be more than one.
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task<IDbTransaction> BeginRead(CancellationToken token)
    {
      for (; ; )
      {
        // get out if needed.
        token.ThrowIfCancellationRequested();

        // now trans and create the transaction
        lock (_lock)
        {
          // if the transaction is null, then create one.
          if (_transaction == null)
          {
            BeginWrite(token).Wait(token);
            _transactionOwner = TransactionOwner.ReadonlyIsOwner;
          }

          // return the current transaction
          var trans = new TransactionsManagerReadOnly(_transaction);
          
          // and add it to the list.
          _readonlyTransactions.Add(trans);
          return Task.FromResult<IDbTransaction>(trans);
        }
      }
    }

    /// <summary>
    /// Begin a write transaction.
    /// There can only be one
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<IDbTransaction> BeginWrite( CancellationToken token )
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
        if (transaction is TransactionsManagerReadOnly trans)
        {
          FinishReadonlyTransaction(trans);
          return;
        }
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
        RollbackInLock();
      }
    }

    /// <summary>
    /// Rollback a transaction within a lock.
    /// </summary>
    private void RollbackInLock()
    {
      if (null == _transaction)
      {
        return;
      }

      if (_readonlyTransactions.Any())
      {
        _transactionOwner = TransactionOwner.ReadyToRollBack;
        return;
      }

      // roll back
      _transaction.Rollback();

      // free resources
      _freeResources();

      // done  with the transaction
      _transaction = null;
    }

    /// <summary>
    /// Commit a transaction.
    /// </summary>
    /// <param name="transaction"></param>
    public void Commit(IDbTransaction transaction)
    {
      lock (_lock)
      {
        if (transaction is TransactionsManagerReadOnly trans)
        {
          FinishReadonlyTransaction( trans );
          return;
        }
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
        CommitInLock();
      }
    }

    /// <summary>
    /// Commit the tansaction while we are in lock.
    /// </summary>
    private void CommitInLock()
    {
      // do we actually have anything to do?
      if (null == _transaction)
      {
        return;
      }

      // if we have any read only transactions, then we can stop.
      if (_readonlyTransactions.Any())
      {
        _transactionOwner = TransactionOwner.ReadyToCommit;
        return;
      }

      // commit 
      _transaction.Commit();

      // free resources
      _freeResources();

      // done  with the transaction
      _transaction = null;
    }

    /// <summary>
    /// Complete the readonly transaction.
    /// </summary>
    /// <param name="trans"></param>
    private void FinishReadonlyTransaction(TransactionsManagerReadOnly trans)
    {
      lock (_lock)
      {
        // remove ourselves from the list.
        _readonlyTransactions.RemoveAll(t => t == trans);
        if (_readonlyTransactions.Any())
        {
          return;
        }

        // now complete the parent transaction
        switch (_transactionOwner)
        {
          case TransactionOwner.None:
            break;

          case TransactionOwner.ReadyToRollBack:
          case TransactionOwner.ReadonlyIsOwner:
            Rollback(trans.Transaction);
            break;

          case TransactionOwner.ReadyToCommit:
            Commit(trans.Transaction);
            break;

          default:
            throw new ArgumentOutOfRangeException();
        }
        _transactionOwner = TransactionOwner.None;
      }
    }
  }
}
