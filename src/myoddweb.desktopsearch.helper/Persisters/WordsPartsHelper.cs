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
  public class WordsPartsHelper : IWordsPartsHelper
  {
    #region Exists
    /// <summary>
    /// The select command;
    /// </summary>
    private IDbCommand _existsCommand;

    /// <summary>
    /// The parameters to check if a word id exists.
    /// </summary>
    private IDbDataParameter _existsWordId;

    /// <summary>
    /// The parameters to check if a part id exists.
    /// </summary>
    private IDbDataParameter _existsPartId;

    /// <summary>
    /// The sql string that we will use to look for an id.
    /// </summary>
    private string ExistsSql => $"SELECT 1 FROM {_tableName} WHERE wordid=@wordid and partid=@partid";

    /// <summary>
    /// Create the exists command if needed.
    /// </summary>
    private IDbCommand ExistsCommand
    {
      get
      {
        if (_existsCommand != null)
        {
          return _existsCommand;
        }

        _existsCommand = _factory.CreateCommand(ExistsSql);
        return _existsCommand;
      }
    }

    /// <summary>
    /// The exists word id parameter.
    /// </summary>
    private IDbDataParameter ExistsWordId
    {
      get
      {
        if (null != _existsWordId)
        {
          return _existsWordId;
        }

        _existsWordId = ExistsCommand.CreateParameter();
        _existsWordId.DbType = DbType.Int64;
        _existsWordId.ParameterName = "@wordid";
        ExistsCommand.Parameters.Add(_existsWordId);
        return _existsWordId;
      }
    }

    /// <summary>
    /// The exists file id parameter.
    /// </summary>
    private IDbDataParameter ExistsPartId
    {
      get
      {
        if (null != _existsPartId)
        {
          return _existsPartId;
        }

        _existsPartId = ExistsCommand.CreateParameter();
        _existsPartId.DbType = DbType.Int64;
        _existsPartId.ParameterName = "@partid";
        ExistsCommand.Parameters.Add(_existsPartId);
        return _existsPartId;
      }
    }
    #endregion

    #region Insert
    /// <summary>
    /// The insert command;
    /// </summary>
    private IDbCommand _insertCommand;

    /// <summary>
    /// The insert word id parameter;
    /// </summary>
    private IDbDataParameter _insertWordId;

    /// <summary>
    /// The insert part id parameter;
    /// </summary>
    private IDbDataParameter _insertPartId;

    /// <summary>
    /// The sql string that we will use to insert a word.
    /// We will only insert if there are no duplicates.
    /// </summary>
    private string InsertSql => $"INSERT OR IGNORE INTO {_tableName} (wordid, partid) VALUES (@wordid, @partid)";

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
    /// The word id Insert parameter.
    /// </summary>
    private IDbDataParameter InsertWordId
    {
      get
      {
        if (null != _insertWordId)
        {
          return _insertWordId;
        }

        _insertWordId = InsertCommand.CreateParameter();
        _insertWordId.DbType = DbType.Int64;
        _insertWordId.ParameterName = "@wordId";
        InsertCommand.Parameters.Add(_insertWordId);
        return _insertWordId;
      }
    }

    /// <summary>
    /// The part id Insert parameter.
    /// </summary>
    private IDbDataParameter InsertPartId
    {
      get
      {
        if (null != _insertPartId)
        {
          return _insertPartId;
        }

        _insertPartId = InsertCommand.CreateParameter();
        _insertPartId.DbType = DbType.Int64;
        _insertPartId.ParameterName = "@partId";
        InsertCommand.Parameters.Add(_insertPartId);
        return _insertPartId;
      }
    }
    #endregion

    #region Delete
    /// <summary>
    /// The delete command;
    /// </summary>
    private IDbCommand _deleteCommand;

    /// <summary>
    /// The delete word id parameter;
    /// </summary>
    private IDbDataParameter _deleteWordId;

    /// <summary>
    /// The delete part id parameter;
    /// </summary>
    private IDbDataParameter _deletePartId;

    /// <summary>
    /// The sql string that we will use to delete a word/part.
    /// </summary>
    private string DeleteSql => $"DELETE FROM {_tableName} WHERE wordid=@wordid AND partid=@partid";

    /// <summary>
    /// Create the Delete command if needed.
    /// </summary>
    private IDbCommand DeleteCommand
    {
      get
      {
        if (_deleteCommand != null)
        {
          return _deleteCommand;
        }

        _deleteCommand = _factory.CreateCommand(DeleteSql);
        return _deleteCommand;
      }
    }

    /// <summary>
    /// The word id delete parameter.
    /// </summary>
    private IDbDataParameter DeleteWordId
    {
      get
      {
        if (null != _deleteWordId)
        {
          return _deleteWordId;
        }

        _deleteWordId = DeleteCommand.CreateParameter();
        _deleteWordId.DbType = DbType.Int64;
        _deleteWordId.ParameterName = "@wordId";
        DeleteCommand.Parameters.Add(_deleteWordId);
        return _deleteWordId;
      }
    }

    /// <summary>
    /// The part id Delete parameter.
    /// </summary>
    private IDbDataParameter DeletePartId
    {
      get
      {
        if (null != _deletePartId)
        {
          return _deletePartId;
        }

        _deletePartId = DeleteCommand.CreateParameter();
        _deletePartId.DbType = DbType.Int64;
        _deletePartId.ParameterName = "@partId";
        DeleteCommand.Parameters.Add(_insertPartId);
        return _deletePartId;
      }
    }
    #endregion

    #region Select Ids
    /// <summary>
    /// The select command;
    /// </summary>
    private IDbCommand _selectIdsCommand;

    /// <summary>
    /// The insert word parameter;
    /// </summary>
    private IDbDataParameter _selectPartIdsWordId;

    /// <summary>
    /// The sql string that we will use to look for an id.
    /// </summary>
    private string SelectPartIdsSql => $"SELECT partid FROM {_tableName} WHERE wordid = @wordid";

    /// <summary>
    /// Create the select command if needed.
    /// </summary>
    private IDbCommand SelectPartIdsCommand
    {
      get
      {
        if (_selectIdsCommand != null)
        {
          return _selectIdsCommand;
        }

        _selectIdsCommand = _factory.CreateCommand(SelectPartIdsSql);
        return _selectIdsCommand;
      }
    }

    /// <summary>
    /// The word select parameter.
    /// </summary>
    private IDbDataParameter SelectPartIdsWordId
    {
      get
      {
        if (null != _selectPartIdsWordId)
        {
          return _selectPartIdsWordId;
        }

        _selectPartIdsWordId = SelectPartIdsCommand.CreateParameter();
        _selectPartIdsWordId.DbType = DbType.Int64;
        _selectPartIdsWordId.ParameterName = "@wordid";
        SelectPartIdsCommand.Parameters.Add(_selectPartIdsWordId);
        return _selectPartIdsWordId;
      }
    }
    #endregion

    #region Member variables
    /// <summary>
    /// The lock to make sure that we do not create the same thing over and over.
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

    public WordsPartsHelper(IConnectionFactory factory, string tableName)
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

      _selectIdsCommand?.Dispose();
      _insertCommand?.Dispose();
      _existsCommand?.Dispose();
      _deleteCommand?.Dispose();
    }

    /// <inheritdoc />
    public async Task<IList<long>> GetPartIdsAsync(long wordid, CancellationToken token )
    {
      using (await _lock.TryAsync().ConfigureAwait(false))
      {
        // sanity check
        ThrowIfDisposed();

        // select all the ids that belong to that word.
        SelectPartIdsWordId.Value = wordid;
        using (var reader = await _factory.ExecuteReadAsync(SelectPartIdsCommand, token).ConfigureAwait(false))
        {
          var partIds = new List<long>();
          while (reader.Read())
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // add this part
            partIds.Add(reader.GetInt64(0));
          }

          //  return all the part ids we found.
          return partIds;
        }
      }
    }

    /// <inheritdoc />
    public async Task<bool> InsertAsync(long wordId, long partId, CancellationToken token)
    {
      using (await _lock.TryAsync().ConfigureAwait(false))
      {
        // sanity check
        ThrowIfDisposed();

        InsertWordId.Value = wordId;
        InsertPartId.Value = partId;

        // select all the ids that belong to that word.
        if (1 == await _factory.ExecuteWriteAsync(InsertCommand, token).ConfigureAwait(false))
        {
          return true;
        }

        // we could not insert it
        // so we have to check if the reason was because this exists already.
        return await ExistsAsync(wordId, partId, token).ConfigureAwait(false);
      }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(long wordId, long partId, CancellationToken token)
    {
      using (await _lock.TryAsync().ConfigureAwait(false))
      {
        // sanity check
        ThrowIfDisposed();

        DeleteWordId.Value = wordId;
        DeletePartId.Value = partId;

        // select all the ids that belong to that word.
        return 0 != await _factory.ExecuteWriteAsync(DeleteCommand, token).ConfigureAwait(false);
      }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(long wordId, long partId, CancellationToken token)
    {
      using (await _lock.TryAsync().ConfigureAwait(false))
      {
        // sanity check
        ThrowIfDisposed();

        // we are first going to look for that id
        // if it does not exist, then we cannot update the files table.
        ExistsWordId.Value = wordId;
        ExistsPartId.Value = partId;
        var value = await _factory.ExecuteReadOneAsync(ExistsCommand, token).ConfigureAwait(false);

        // return if we found a value.
        return null != value && value != DBNull.Value;
      }
    }
  }
}
