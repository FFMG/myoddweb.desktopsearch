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
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.helper.Persisters
{
  internal class PersisterInsertPartsSearchHelper : PersisterHelper
  {
    /// <summary>
    /// The part id.
    /// </summary>
    private IDbDataParameter _id;

    /// <summary>
    /// The part value.
    /// </summary>
    private IDbDataParameter _part;

    /// <summary>
    /// The part Id parameter.
    /// </summary>
    private IDbDataParameter Id
    {
      get
      {
        if (null != _id)
        {
          return _id;
        }

        _id = Command.CreateParameter();
        _id.DbType = DbType.Int64;
        _id.ParameterName = "@id";
        Command.Parameters.Add(_id);
        return _id;
      }
    }

    /// <summary>
    /// The part Insert parameter.
    /// </summary>
    private IDbDataParameter Part
    {
      get
      {
        if (null != _part)
        {
          return _part;
        }

        _part = Command.CreateParameter();
        _part.DbType = DbType.String;
        _part.ParameterName = "@part";
        Command.Parameters.Add(_part);
        return _part;
      }
    }

    public PersisterInsertPartsSearchHelper(IConnectionFactory factory, string sql) : base(factory, sql)
    {
    }

    public async Task InsertAsync(IReadOnlyCollection<IPart> parts, CancellationToken token)
    {
      using (await Lock.TryAsync(token).ConfigureAwait(false))
      {
        foreach (var part in parts)
        {
          // insert the word.
          Id.Value = part.Id;
          Part.Value = part.Value;
          await Factory.ExecuteWriteAsync(Command, token).ConfigureAwait(false);
        }
      }
    }
  }

  public class PartsSearchHelper : IPartsSearchHelper
  {
    #region Member variables
    /// <summary>
    /// Insert a single part.
    /// </summary>
    private readonly MultiplePersisterHelper<PersisterInsertPartsSearchHelper> _insert;

    /// <summary>
    /// Check if this item has been disposed or not.
    /// </summary>
    private bool _disposed;
    #endregion

    public PartsSearchHelper(IConnectionFactory factory, string tableName)
    {
      const int numberOfItems = 10;

      // insert
      _insert = new MultiplePersisterHelper<PersisterInsertPartsSearchHelper>(() => new PersisterInsertPartsSearchHelper( factory, $"INSERT OR IGNORE INTO {tableName} (id, part) VALUES (@id, @part)"), numberOfItems);
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

    /// <inheritdoc />
    public void Dispose()
    {
      if (_disposed)
      {
        return;
      }

      // we are now done.
      _disposed = true;

      _insert?.Dispose();
    }

    /// <inheritdoc />
    public Task InsertAsync(IReadOnlyCollection<IPart> parts, CancellationToken token)
    {
      // sanity check
      ThrowIfDisposed();

      // insert the part.
      return _insert.Next().InsertAsync(parts, token);
    }
  }
}
