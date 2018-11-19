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
  public class ParserFilesWordsHelper : IParserFilesWordsHelper
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

    #region Delete by word and file ids
    /// <summary>
    /// The delete command;
    /// </summary>
    private IDbCommand _deleteByWordAndFileCommand;

    /// <summary>
    /// The word id to delete.
    /// </summary>
    private IDbDataParameter _deleteByWordAndFileWordId;

    /// <summary>
    /// The file id to insert.
    /// </summary>
    private IDbDataParameter _deleteByWordAndFileFileId;

    /// <summary>
    /// The sql string that we will use to insert an id.
    /// </summary>
    private string DeleteByWordAndFileSql => $"DELETE FROM {_tableName} WHERE wordid=@wordid AND fileid=@fileid";

    /// <summary>
    /// Create the delete command if needed.
    /// </summary>
    private IDbCommand DeleteByWordAndFileCommand
    {
      get
      {
        if (_deleteByWordAndFileCommand != null)
        {
          return _deleteByWordAndFileCommand;
        }

        lock (_lock)
        {
          if (_deleteByWordAndFileCommand == null)
          {
            _deleteByWordAndFileCommand = _factory.CreateCommand(DeleteByWordAndFileSql);
          }
          return _deleteByWordAndFileCommand;
        }
      }
    }

    /// <summary>
    /// The delete word id parameter.
    /// </summary>
    private IDbDataParameter DeleteByWordAndFileWordId
    {
      get
      {
        if (null != _deleteByWordAndFileWordId)
        {
          return _deleteByWordAndFileWordId;
        }

        lock (_lock)
        {
          if (null == _deleteByWordAndFileWordId)
          {
            _deleteByWordAndFileWordId = DeleteByWordAndFileCommand.CreateParameter();
            _deleteByWordAndFileWordId.DbType = DbType.Int64;
            _deleteByWordAndFileWordId.ParameterName = "@wordid";
            DeleteByWordAndFileCommand.Parameters.Add(_deleteByWordAndFileWordId);
          }
          return _deleteByWordAndFileWordId;
        }
      }
    }

    /// <summary>
    /// The delete file id parameter.
    /// </summary>
    private IDbDataParameter DeleteByWordAndFileFileId
    {
      get
      {
        if (null != _deleteByWordAndFileFileId)
        {
          return _deleteByWordAndFileFileId;
        }

        lock (_lock)
        {
          if (null == _deleteByWordAndFileFileId)
          {
            _deleteByWordAndFileFileId = DeleteByWordAndFileCommand.CreateParameter();
            _deleteByWordAndFileFileId.DbType = DbType.Int64;
            _deleteByWordAndFileFileId.ParameterName = "@fileid";
            DeleteByWordAndFileCommand.Parameters.Add(_deleteByWordAndFileFileId);
          }
          return _deleteByWordAndFileFileId;
        }
      }
    }
    #endregion

    #region Delete by word id
    /// <summary>
    /// The delete command;
    /// </summary>
    private IDbCommand _deleteByWordIdCommand;

    /// <summary>
    /// The word id to delete.
    /// </summary>
    private IDbDataParameter _deleteByWordIdWordId;

    /// <summary>
    /// The sql string that we will use to delete by word id.
    /// </summary>
    private string DeleteByWordIdSql => $"DELETE FROM {_tableName} WHERE wordid=@wordid";

    /// <summary>
    /// Create the delete command if needed.
    /// </summary>
    private IDbCommand DeleteByWordIdCommand
    {
      get
      {
        if (_deleteByWordIdCommand != null)
        {
          return _deleteByWordIdCommand;
        }

        lock (_lock)
        {
          if (_deleteByWordIdCommand == null)
          {
            _deleteByWordIdCommand = _factory.CreateCommand(DeleteByWordIdSql);
          }
          return _deleteByWordIdCommand;
        }
      }
    }

    /// <summary>
    /// The delete word id parameter.
    /// </summary>
    private IDbDataParameter DeleteByWordIdWordId
    {
      get
      {
        if (null != _deleteByWordIdWordId)
        {
          return _deleteByWordIdWordId;
        }

        lock (_lock)
        {
          if (null == _deleteByWordIdWordId)
          {
            _deleteByWordIdWordId = DeleteByWordIdCommand.CreateParameter();
            _deleteByWordIdWordId.DbType = DbType.Int64;
            _deleteByWordIdWordId.ParameterName = "@wordid";
            DeleteByWordIdCommand.Parameters.Add(_deleteByWordIdWordId);
          }
          return _deleteByWordIdWordId;
        }
      }
    }
    #endregion

    #region Select Word Ids
    /// <summary>
    /// The select command
    /// </summary>
    private IDbCommand _selectWordIdsCommand;

    /// <summary>
    /// The File Id we are looking for.
    /// </summary>
    private IDbDataParameter _selectFileId;

    /// <summary>
    /// The sql string that we will use to look for an id.
    /// </summary>
    private string SelectWordIdSql => $"SELECT wordid FROM {_tableName} WHERE fileid = @fileid";

    /// <summary>
    /// Create the select command if needed.
    /// </summary>
    private IDbCommand SelectWordIdsCommand
    {
      get
      {
        if (_selectWordIdsCommand != null)
        {
          return _selectWordIdsCommand;
        }

        lock (_lock)
        {
          if (_selectWordIdsCommand == null)
          {
            _selectWordIdsCommand = _factory.CreateCommand(SelectWordIdSql);
          }
          return _selectWordIdsCommand;
        }
      }
    }

    /// <summary>
    /// The id select parameter.
    /// </summary>
    private IDbDataParameter SelectFileId
    {
      get
      {
        if (null != _selectFileId)
        {
          return _selectFileId;
        }

        lock (_lock)
        {
          if (null == _selectFileId)
          {
            _selectFileId = SelectWordIdsCommand.CreateParameter();
            _selectFileId.DbType = DbType.Int64;
            _selectFileId.ParameterName = "@fileid";
            SelectWordIdsCommand.Parameters.Add(_selectFileId);
          }
          return _selectFileId;
        }
      }
    }
    #endregion

    #region Select File Ids
    /// <summary>
    /// The select command
    /// </summary>
    private IDbCommand _selectFileIdsCommand;

    /// <summary>
    /// The Word Id we are looking for.
    /// </summary>
    private IDbDataParameter _selectWordId;

    /// <summary>
    /// The sql string that we will use to look for an id.
    /// </summary>
    private string SelectFileIdSql => $"SELECT fileid FROM {_tableName} WHERE wordid = @wordid";

    /// <summary>
    /// Create the select command if needed.
    /// </summary>
    private IDbCommand SelectFileIdsCommand
    {
      get
      {
        if (_selectFileIdsCommand != null)
        {
          return _selectFileIdsCommand;
        }

        lock (_lock)
        {
          if (_selectFileIdsCommand == null)
          {
            _selectFileIdsCommand = _factory.CreateCommand(SelectFileIdSql);
          }
          return _selectFileIdsCommand;
        }
      }
    }

    /// <summary>
    /// The id select parameter.
    /// </summary>
    private IDbDataParameter SelectWordId
    {
      get
      {
        if (null != _selectWordId)
        {
          return _selectWordId;
        }

        lock (_lock)
        {
          if (null == _selectWordId)
          {
            _selectWordId = SelectWordIdsCommand.CreateParameter();
            _selectWordId.DbType = DbType.Int64;
            _selectWordId.ParameterName = "@wordid";
            SelectFileIdsCommand.Parameters.Add(_selectWordId);
          }
          return _selectWordId;
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

    public ParserFilesWordsHelper(IConnectionFactory factory, string tableName)
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

      // dispose of all the commands.
      _existsCommand?.Dispose();
      _insertCommand?.Dispose();
      _deleteByWordAndFileCommand?.Dispose();
      _selectWordIdsCommand?.Dispose();
      _selectFileIdsCommand?.Dispose();
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
    public async Task<IList<long>> GetWordIdsAsync(long fileId, CancellationToken token)
    {
      // sanity check
      ThrowIfDisposed();
      
      // look for that word using the given id.
      SelectFileId.Value = fileId;
      using (var reader = await _factory.ExecuteReadAsync(SelectWordIdsCommand, token).ConfigureAwait(false))
      {
        // the word ids
        var ids = new List<long>();

        var wordIdPos = reader.GetOrdinal("wordid");
        while (reader.Read())
        {
          // get out if needed.
          token.ThrowIfCancellationRequested();

          // add this update
          ids.Add( (long)reader[wordIdPos]);
        }

        return ids;
      }
    }

    /// <inheritdoc />
    public async Task<IList<long>> GetFileIdsAsync(long wordId, CancellationToken token)
    {
      // sanity check
      ThrowIfDisposed();

      // look for that files using the given id.
      SelectWordId.Value = wordId;
      using (var reader = await _factory.ExecuteReadAsync(SelectFileIdsCommand, token).ConfigureAwait(false))
      {
        // the file ids
        var ids = new List<long>();

        // get the data
        var fileIdPos = reader.GetOrdinal("fileid");
        while (reader.Read())
        {
          // get out if needed.
          token.ThrowIfCancellationRequested();

          // add this update
          ids.Add((long)reader[fileIdPos]);
        }
        return ids;
      }
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

    /// <inheritdoc />
    public Task DeleteAsync(long wordId, long fileId, CancellationToken token)
    {
      // sanity check
      ThrowIfDisposed();

      // delete by word and file id
      DeleteByWordAndFileWordId.Value = wordId;
      DeleteByWordAndFileFileId.Value = fileId;
      return _factory.ExecuteWriteAsync(DeleteByWordAndFileCommand, token);
    }

    /// <inheritdoc />
    public Task DeleteWordAsync(long wordId, CancellationToken token)
    {
      // sanity check
      ThrowIfDisposed();

      // delete by word and file id
      DeleteByWordIdWordId.Value = wordId;
      return _factory.ExecuteWriteAsync(DeleteByWordIdCommand, token);
    }
  }
}
