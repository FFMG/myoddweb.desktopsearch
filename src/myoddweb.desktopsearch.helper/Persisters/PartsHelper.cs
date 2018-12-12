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
  public class PartsHelper : IPartsHelper
  {
    #region Insert
    /// <summary>
    /// The insert command;
    /// </summary>
    private IDbCommand _insertCommand;

    /// <summary>
    /// The insert part parameter;
    /// </summary>
    private IDbDataParameter _insertPart;

    /// <summary>
    /// The sql string that we will use to insert a word.
    /// We will only insert if there are no duplicates.
    /// </summary>
    private string InsertSql => $"INSERT OR IGNORE INTO {_tableName} (part) VALUES (@part)";

    /// <summary>
    /// Create the Insert command if needed.
    /// </summary>
    private IDbCommand InsertCommand
    {
      get
      {
        if (_insertCommand != null)
        {
          return _insertCommand;
        }

        _insertCommand = _factory.CreateCommand(InsertSql);
        return _insertCommand;
      }
    }

    /// <summary>
    /// The word Insert parameter.
    /// </summary>
    private IDbDataParameter InsertPart
    {
      get
      {
        if (null != _insertPart)
        {
          return _insertPart;
        }

        _insertPart = InsertCommand.CreateParameter();
        _insertPart.DbType = DbType.String;
        _insertPart.ParameterName = "@part";
        InsertCommand.Parameters.Add(_insertPart);
        return _insertPart;
      }
    }
    #endregion

    #region Select
    /// <summary>
    /// The select command;
    /// </summary>
    private IDbCommand _selectCommand;

    /// <summary>
    /// The select part parameter;
    /// </summary>
    private IDbDataParameter _selectPart;

    /// <summary>
    /// The sql string that we will use to look for an id.
    /// </summary>
    private string SelectSql => $"SELECT id FROM {_tableName} WHERE part = @part";

    /// <summary>
    /// Create the select command if needed.
    /// </summary>
    private IDbCommand SelectCommand
    {
      get
      {
        if (_selectCommand != null)
        {
          return _selectCommand;
        }

        _selectCommand = _factory.CreateCommand(SelectSql);
        return _selectCommand;
      }
    }

    /// <summary>
    /// The word select parameter.
    /// </summary>
    private IDbDataParameter SelectPart
    {
      get
      {
        if (null != _selectPart)
        {
          return _selectPart;
        }
        _selectPart = SelectCommand.CreateParameter();
        _selectPart.DbType = DbType.String;
        _selectPart.ParameterName = "@part";
        SelectCommand.Parameters.Add(_selectPart);
        return _selectPart;
      }
    }
    #endregion

    #region Member variables
    /// <summary>
    /// The async await lock
    /// </summary>
    private readonly Lock.Lock _lock = new Lock.Lock();

    /// <summary>
    /// Check if this item has been disposed or not.
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// The connection factory.
    /// </summary>
    private readonly IConnectionFactory _factory;

    /// <summary>
    /// The name of the table.
    /// </summary>
    private readonly string _tableName;
    #endregion

    public PartsHelper(IConnectionFactory factory, string tableName)
    {
      // the table name
      _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));

      // save the factory.
      _factory = factory ?? throw new ArgumentNullException(nameof(factory));
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

      _selectCommand?.Dispose();
      _insertCommand?.Dispose();
    }

    /// <inheritdoc />
    public async Task<long> GetIdAsync(string part, CancellationToken token)
    {
      using (await _lock.TryAsync().ConfigureAwait(false))
      {
        // sanity check
        ThrowIfDisposed();

        // we are first going to look for that id
        // if it does not exist, then we cannot update the files table.
        SelectPart.Value = part;
        var value = await _factory.ExecuteReadOneAsync(SelectCommand, token).ConfigureAwait(false);
        if (null == value || value == DBNull.Value)
        {
          // we could not find this word.
          return -1;
        }

        return (long) value;
      }
    }

    /// <inheritdoc />
    public async Task<long> InsertAndGetIdAsync(string part, CancellationToken token)
    {
      using (await _lock.TryAsync().ConfigureAwait(false))
      {
        // insert the word.
        InsertPart.Value = part;
        await _factory.ExecuteWriteAsync(InsertCommand, token).ConfigureAwait(false);

        // regardless of the result, get the id
        // if it existed, get the id
        // if we inserted it, get the id.
        return await GetIdAsync(part, token).ConfigureAwait(false);
      }
    }
  }
}
