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
using System.Data;
using System.Threading;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal class TransactionSpiner
  {
    public delegate IDbConnection CreateConnection();

    private readonly object _lock = new object();
    private IDbConnection _connection;
    private IDbTransaction _transaction;
    private readonly CreateConnection _createConnection;
    private readonly CancellationToken _token;

    public IDbConnection Connection
    {
      get
      {
        lock (_lock)
        {
          return _connection;
        }
      }
    }

    public TransactionSpiner(CreateConnection createConnection, CancellationToken token)
    {
      _token = token;
      _createConnection = createConnection ?? throw new ArgumentNullException(nameof(createConnection));
    }

    public IDbTransaction Begin()
    {
      for (; ; )
      {
        // wait for the transaction to no longer be null
        // outside of the lock, (so it can be freed.
        if (null != _transaction)
        {
          SpinWait.SpinUntil(() => _transaction == null || _token.IsCancellationRequested);
        }

        if (_token.IsCancellationRequested)
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
        _transaction.Rollback();

        _connection.Close();
        _connection.Dispose();

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

        _transaction.Commit();
        _connection.Close();
        _connection.Dispose();

        _transaction = null;
        _connection = null;
      }
    }
  }
}