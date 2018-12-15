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
  internal class PersisterInsertPartsHelper : PersisterHelper
  {
    /// <summary>
    /// The part id.
    /// </summary>
    private IDbDataParameter _part;

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

    public PersisterInsertPartsHelper(IConnectionFactory factory, string sql) : base(factory, sql)
    {
    }

    public async Task InsertAsync(IReadOnlyCollection<string> parts, CancellationToken token)
    {
      using (await Lock.TryAsync().ConfigureAwait(false))
      {
        foreach (var part in parts)
        {
          // insert the word.
          Part.Value = part;
          await Factory.ExecuteWriteAsync(Command, token).ConfigureAwait(false);
        }
      }
    }
  }

  internal class PersisterSelectPartsHelper : PersisterHelper
  {
    /// <summary>
    /// The part id.
    /// </summary>
    private IDbDataParameter _part;

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

    public PersisterSelectPartsHelper(IConnectionFactory factory, string sql) : base(factory, sql)
    {
    }

    public async Task<IList<IPart>> GetAsync(IReadOnlyCollection<string> parts, CancellationToken token)
    {
      using (await Lock.TryAsync().ConfigureAwait(false))
      {
        var partsAndIds = new List<IPart>();

        foreach (var part in parts)
        {
          // get out if needed.
          token.ThrowIfCancellationRequested();

          Part.Value = part;
          var value = await Factory.ExecuteReadOneAsync(Command, token).ConfigureAwait(false);
          if (null == value || value == DBNull.Value)
          {
            // we could not find this word.
            partsAndIds.Add(new Part(part, -1));
            continue;
          }
          partsAndIds.Add( new Part(part, (long)value));
        }
        return partsAndIds;
      }
    }
  }

  public class PartsHelper : IPartsHelper
  {
    #region Member variables
    /// <summary>
    /// Insert a single part.
    /// </summary>
    private readonly MultiplePersisterHelper<PersisterInsertPartsHelper> _insert;

    /// <summary>
    /// Select a part 
    /// </summary>
    private readonly MultiplePersisterHelper<PersisterSelectPartsHelper> _select;

    /// <summary>
    /// Check if this item has been disposed or not.
    /// </summary>
    private bool _disposed;
    #endregion

    public PartsHelper(IConnectionFactory factory, string tableName)
    {
      const int numberOfItems = 10;

      // insert
      _insert = new MultiplePersisterHelper<PersisterInsertPartsHelper>(() => new PersisterInsertPartsHelper( factory, $"INSERT OR IGNORE INTO {tableName} (part) VALUES (@part)"), numberOfItems);

      // select
      _select = new MultiplePersisterHelper<PersisterSelectPartsHelper>(() => new PersisterSelectPartsHelper( factory, $"SELECT id FROM {tableName} WHERE part = @part"), numberOfItems);
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

      _select?.Dispose();
      _insert?.Dispose();
    }

    /// <inheritdoc />
    public Task<IList<IPart>> GetIdsAsync(IReadOnlyCollection<string> parts, CancellationToken token)
    {
      // sanity check
      ThrowIfDisposed();

      return _select.Next().GetAsync(parts, token);
    }

    /// <inheritdoc />
    public async Task<IList<IPart>> InsertAndGetAsync(IReadOnlyCollection<string> parts, CancellationToken token)
    {
      // sanity check
      ThrowIfDisposed();

      // insert the part.
      await _insert.Next().InsertAsync(parts, token).ConfigureAwait(false);
      
      // regardless of the result, get the id, if it existed, get the id
      // and if we inserted it, get the id.
      return await GetIdsAsync(parts, token).ConfigureAwait(false);
    }
  }
}
