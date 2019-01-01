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
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.helper.Persisters
{
  internal class PersisterUpdateCountsHelper : PersisterHelper
  {
    /// <summary>
    /// The count we are adding/removing
    /// </summary>
    private IDbDataParameter _count;

    /// <summary>
    /// The type being updated
    /// </summary>
    private IDbDataParameter _type;

    /// <summary>
    /// The count we are adding/removing
    /// </summary>
    public IDbDataParameter Count
    {
      get
      {
        if (null != _count)
        {
          return _count;
        }

        _count = Command.CreateParameter();
        _count.DbType = DbType.Int64;
        _count.ParameterName = "@count";
        Command.Parameters.Add(_count);
        return _count;
      }
    }

    /// <summary>
    /// The part id Insert parameter.
    /// </summary>
    private IDbDataParameter Type
    {
      get
      {
        if (null != _type)
        {
          return _type;
        }

        _type = Command.CreateParameter();
        _type.DbType = DbType.Int64;
        _type.ParameterName = "@type";
        Command.Parameters.Add(_type);
        return _type;
      }
    }

    public PersisterUpdateCountsHelper(IConnectionFactory factory, string sql) : base(factory, sql)
    {
    }

    public async Task<bool> UpdateAsync(long type, long count, CancellationToken token)
    {
      using (await Lock.TryAsync(token).ConfigureAwait(false))
      {
        // we are first going to look for that id
        // if it does not exist, then we cannot update the files table.
        Count.Value = count;
        Type.Value = type;
        return 1 == await Factory.ExecuteWriteAsync(Command, token).ConfigureAwait(false);
      }
    }
  }

  public class CountsHelper : ICountsHelper
  {
    #region Member variables
    /// <summary>
    /// Update the counters.
    /// </summary>
    private readonly PersisterUpdateCountsHelper _update;

    /// <summary>
    /// Insert the counters.
    /// </summary>
    private readonly PersisterUpdateCountsHelper _insert;

    /// <summary>
    /// Check if this item has been disposed or not.
    /// </summary>
    private bool _disposed;
    #endregion

    public CountsHelper(IConnectionFactory factory, string tableName)
    {
      // update the counters.
      _update = new PersisterUpdateCountsHelper( factory, $"UPDATE {tableName} SET count=count+@count WHERE type=@type" );

      // insert the counters.
      _insert = new PersisterUpdateCountsHelper(factory, $"INSERT OR REPLACE INTO {tableName} (count, type) VALUES (@count, @type);");
    }

    /// <summary>
    /// Check that this class has not been disposed already.
    /// </summary>
    private void ThrowIfDisposed()
    {
      if (!_disposed)
      {
        return;
      }
      throw new ObjectDisposedException(GetType().FullName);
    }

    public void Dispose()
    {
      if (_disposed)
      {
        return;
      }

      // we are now done.
      _disposed = true;

      _update?.Dispose();
      _insert?.Dispose();
    }

    /// <inheritdoc />
    public Task<bool> AddAsync(long type, long addOrRemove, CancellationToken token)
    {
      ThrowIfDisposed();

      return _update.UpdateAsync(type, addOrRemove, token);
    }

    /// <inheritdoc />
    public Task<bool> SetAsync(long type, long value, CancellationToken token)
    {
      ThrowIfDisposed();

      return _insert.UpdateAsync(type, value, token);
    }

  }
}
