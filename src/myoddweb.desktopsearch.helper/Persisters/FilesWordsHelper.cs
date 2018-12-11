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
  public class FilesWordsHelper : IFilesWordsHelper
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
    /// The parameters to check if a file id exists.
    /// </summary>
    private IDbDataParameter _existsFileId;

    /// <summary>
    /// The sql string that we will use to look for an id.
    /// </summary>
    private string ExistsSql => $"SELECT 1 FROM {_tableName} where wordid=@wordid AND fileid=@fileid";

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

        lock (_lock)
        {
          if (_existsCommand == null)
          {
            _existsCommand = _factory.CreateCommand(ExistsSql);
          }
          return _existsCommand;
        }
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

        lock (_lock)
        {
          if (null == _existsWordId)
          {
            _existsWordId = ExistsCommand.CreateParameter();
            _existsWordId.DbType = DbType.Int64;
            _existsWordId.ParameterName = "@wordid";
            ExistsCommand.Parameters.Add(_existsWordId);
          }
          return _existsWordId;
        }
      }
    }

    /// <summary>
    /// The exists file id parameter.
    /// </summary>
    private IDbDataParameter ExistsFileId
    {
      get
      {
        if (null != _existsFileId)
        {
          return _existsFileId;
        }

        lock (_lock)
        {
          if (null == _existsFileId)
          {
            _existsFileId = ExistsCommand.CreateParameter();
            _existsFileId.DbType = DbType.Int64;
            _existsFileId.ParameterName = "@fileid";
            ExistsCommand.Parameters.Add(_existsFileId);
          }
          return _existsFileId;
        }
      }
    }
    #endregion

    #region Insert
    /// <summary>
    /// The insert command;
    /// </summary>
    private IDbCommand _insertCommand;

    /// <summary>
    /// The word id to insert.
    /// </summary>
    private IDbDataParameter _insertWordId;

    /// <summary>
    /// The file id to insert.
    /// </summary>
    private IDbDataParameter _insertFileId;

    /// <summary>
    /// The sql string that we will use to insert an id.
    /// </summary>
    private string InsertSql => $"INSERT OR IGNORE INTO {_tableName} (wordid, fileid) VALUES (@wordid, @fileid)";

    /// <summary>
    /// Create the exists command if needed.
    /// </summary>
    private IDbCommand InsertCommand
    {
      get
      {
        if (_insertCommand != null)
        {
          return _insertCommand;
        }

        lock (_lock)
        {
          if (_insertCommand == null)
          {
            _insertCommand = _factory.CreateCommand(InsertSql);
          }
          return _insertCommand;
        }
      }
    }

    /// <summary>
    /// The insert word id parameter.
    /// </summary>
    private IDbDataParameter InsertWordId
    {
      get
      {
        if (null != _insertWordId)
        {
          return _insertWordId;
        }

        lock (_lock)
        {
          if (null == _insertWordId)
          {
            _insertWordId = InsertCommand.CreateParameter();
            _insertWordId.DbType = DbType.Int64;
            _insertWordId.ParameterName = "@wordid";
            InsertCommand.Parameters.Add(_insertWordId);
          }
          return _insertWordId;
        }
      }
    }

    /// <summary>
    /// The insert file id parameter.
    /// </summary>
    private IDbDataParameter InsertFileId
    {
      get
      {
        if (null != _insertFileId)
        {
          return _insertFileId;
        }

        lock (_lock)
        {
          if (null == _insertFileId)
          {
            _insertFileId = InsertCommand.CreateParameter();
            _insertFileId.DbType = DbType.Int64;
            _insertFileId.ParameterName = "@fileid";
            InsertCommand.Parameters.Add(_insertFileId);
          }
          return _insertFileId;
        }
      }
    }
    #endregion

    #region Delete
    /// <summary>
    /// The delete command
    /// </summary>
    private IDbCommand _deleteCommand;

    /// <summary>
    /// The file id to delete.
    /// </summary>
    private IDbDataParameter _deleteFileId;

    /// <summary>
    /// The sql string that we will use to delete the id
    /// </summary>
    private string DeletetSql => $"DELETE FROM {_tableName} WHERE fileid=@fileid";

    /// <summary>
    /// Create the delete command if needed.
    /// </summary>
    private IDbCommand DeleteCommand
    {
      get
      {
        if (_deleteCommand != null)
        {
          return _deleteCommand;
        }

        lock (_lock)
        {
          if (_deleteCommand == null)
          {
            _deleteCommand = _factory.CreateCommand(DeletetSql);
          }
          return _deleteCommand;
        }
      }
    }

    /// <summary>
    /// The delete file id parameter.
    /// </summary>
    private IDbDataParameter DeleteFileId
    {
      get
      {
        if (null != _deleteFileId)
        {
          return _deleteFileId;
        }

        lock (_lock)
        {
          if (null == _deleteFileId)
          {
            _deleteFileId = InsertCommand.CreateParameter();
            _deleteFileId.DbType = DbType.Int64;
            _deleteFileId.ParameterName = "@fileid";
            DeleteCommand.Parameters.Add(_deleteFileId);
          }
          return _deleteFileId;
        }
      }
    }
    #endregion

    #region Member variables
    /// <summary>
    /// The lock to make sure that we do not create the same thing over and over.
    /// </summary>
    private readonly object _lock = new object();

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

    public FilesWordsHelper(IConnectionFactory factory, string tableName)
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

      _existsCommand?.Dispose();
      _insertCommand?.Dispose();
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(long wordId, long fileId, CancellationToken token)
    {
      // sanity check
      ThrowIfDisposed();

      // we are first going to look for that id
      // if it does not exist, then we cannot update the files table.
      ExistsWordId.Value = wordId;
      ExistsFileId.Value = fileId;
      var value = await _factory.ExecuteReadOneAsync(ExistsCommand, token).ConfigureAwait(false);

      // return if we found a value.
      return null != value && value != DBNull.Value;
    }

    /// <inheritdoc />
    public async Task<bool> InsertAsync(long wordId, long fileId, CancellationToken token)
    {
      // sanity check
      ThrowIfDisposed();

      // insert the word.
      InsertWordId.Value = wordId;
      InsertFileId.Value = fileId;
      if (1 == await _factory.ExecuteWriteAsync(InsertCommand, token).ConfigureAwait(false))
      {
        // the insert woked.
        return true;
      }

      // was there an error ... or is it a duplicate.
      return await ExistsAsync(wordId, fileId, token).ConfigureAwait(false);
    }

    public async Task<bool> DeleteFileAsync(long fileId, CancellationToken token)
    {
      // sanity check
      ThrowIfDisposed();

      // delete the file
      DeleteFileId.Value = fileId;
      return (1 == await _factory.ExecuteWriteAsync(DeleteCommand, token).ConfigureAwait(false));
    }
  }
}
