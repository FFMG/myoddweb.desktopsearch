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
  public class ParserWordsHelper : IParserWordsHelper
  {
    #region Insert
    /// <summary>
    /// The insert command;
    /// </summary>
    private IDbCommand _insertCommand;

    /// <summary>
    /// The insert word parameter;
    /// </summary>
    private IDbDataParameter _insertWord;

    /// <summary>
    /// The insert len parameter;
    /// </summary>
    private IDbDataParameter _insertLen;

    /// <summary>
    /// The sql string that we will use to insert a word.
    /// We will only insert if there are no duplicates.
    /// </summary>
    private string InsertSql => $"INSERT OR IGNORE INTO {_tableName} (word, len) VALUES (@word, @len)";

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
    /// The word Insert parameter.
    /// </summary>
    private IDbDataParameter InsertWord
    {
      get
      {
        if (null != _insertWord)
        {
          return _insertWord;
        }

        lock (_lock)
        {
          if (null == _insertWord)
          {
            _insertWord = InsertCommand.CreateParameter();
            _insertWord.DbType = DbType.String;
            _insertWord.ParameterName = "@word";
            InsertCommand.Parameters.Add(_insertWord);
          }
          return _insertWord;
        }
      }
    }
    /// <summary>
    /// The lenght Insert parameter.
    /// </summary>
    private IDbDataParameter InsertLen
    {
      get
      {
        if (null != _insertLen)
        {
          return _insertLen;
        }

        lock (_lock)
        {
          if (null == _insertLen)
          {
            _insertLen = InsertCommand.CreateParameter();
            _insertLen.DbType = DbType.Int64;
            _insertLen.ParameterName = "@len";
            InsertCommand.Parameters.Add(_insertLen);
          }
          return _insertLen;
        }
      }
    }
    #endregion

    #region Select Word Id
    /// <summary>
    /// The select command;
    /// </summary>
    private IDbCommand _selectIdCommand;

    /// <summary>
    /// The insert word parameter;
    /// </summary>
    private IDbDataParameter _selectWord;

    /// <summary>
    /// The sql string that we will use to look for an id.
    /// </summary>
    private string SelectIdSql => $"SELECT id FROM {_tableName} WHERE word = @word";

    /// <summary>
    /// Create the select command if needed.
    /// </summary>
    private IDbCommand SelectIdCommand
    {
      get
      {
        if (_selectIdCommand != null)
        {
          return _selectIdCommand;
        }

        lock (_lock)
        {
          if (_selectIdCommand == null)
          {
            _selectIdCommand = _factory.CreateCommand(SelectIdSql);
          }
          return _selectIdCommand;
        }
      }
    }

    /// <summary>
    /// The word select parameter.
    /// </summary>
    private IDbDataParameter SelectWord
    {
      get
      {
        if (null != _selectWord)
        {
          return _selectWord;
        }

        lock (_lock)
        {
          if (null == _selectWord)
          {
            _selectWord = SelectIdCommand.CreateParameter();
            _selectWord.DbType = DbType.String;
            _selectWord.ParameterName = "@word";
            SelectIdCommand.Parameters.Add(_selectWord);
          }
          return _selectWord;
        }
      }
    }
    #endregion

    #region Select Word
    /// <summary>
    /// The select command;
    /// </summary>
    private IDbCommand _selectWordCommand;

    /// <summary>
    /// The insert word parameter;
    /// </summary>
    private IDbDataParameter _selectId;

    /// <summary>
    /// The sql string that we will use to look for an id.
    /// </summary>
    private string SelectWordSql => $"SELECT word FROM {_tableName} WHERE id = @id";

    /// <summary>
    /// Create the select command if needed.
    /// </summary>
    private IDbCommand SelectWordCommand
    {
      get
      {
        if (_selectWordCommand != null)
        {
          return _selectWordCommand;
        }

        lock (_lock)
        {
          if (_selectWordCommand == null)
          {
            _selectWordCommand = _factory.CreateCommand(SelectWordSql);
          }
          return _selectWordCommand;
        }
      }
    }

    /// <summary>
    /// The id select parameter.
    /// </summary>
    private IDbDataParameter SelectId
    {
      get
      {
        if (null != _selectId)
        {
          return _selectId;
        }

        lock (_lock)
        {
          if (null == _selectId)
          {
            _selectId = SelectWordCommand.CreateParameter();
            _selectId.DbType = DbType.Int64;
            _selectId.ParameterName = "@id";
            SelectWordCommand.Parameters.Add(_selectId);
          }
          return _selectId;
        }
      }
    }
    #endregion

    #region Delete by id
    /// <summary>
    /// The delete command;
    /// </summary>
    private IDbCommand _deleteByIdCommand;

    /// <summary>
    /// The id to delete.
    /// </summary>
    private IDbDataParameter _deleteByIdId;

    /// <summary>
    /// The sql string that we will use to insert an id.
    /// </summary>
    private string DeleteByIdSql => $"DELETE FROM {_tableName} WHERE id=@id";

    /// <summary>
    /// Create the delete command if needed.
    /// </summary>
    private IDbCommand DeleteByIdCommand
    {
      get
      {
        if (_deleteByIdCommand != null)
        {
          return _deleteByIdCommand;
        }

        lock (_lock)
        {
          if (_deleteByIdCommand == null)
          {
            _deleteByIdCommand = _factory.CreateCommand(DeleteByIdSql);
          }
          return _deleteByIdCommand;
        }
      }
    }

    /// <summary>
    /// The delete word id parameter.
    /// </summary>
    private IDbDataParameter DeleteByIdId
    {
      get
      {
        if (null != _deleteByIdId)
        {
          return _deleteByIdId;
        }

        lock (_lock)
        {
          if (null == _deleteByIdId)
          {
            _deleteByIdId = DeleteByIdCommand.CreateParameter();
            _deleteByIdId.DbType = DbType.Int64;
            _deleteByIdId.ParameterName = "@id";
            DeleteByIdCommand.Parameters.Add(_deleteByIdId);
          }
          return _deleteByIdId;
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

    public ParserWordsHelper(IConnectionFactory factory, string tableName )
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

      // dispose of the commands if needed.
      _insertCommand?.Dispose();
      _selectIdCommand?.Dispose();
      _selectWordCommand?.Dispose();
      _deleteByIdCommand?.Dispose();
    }

    /// <inheritdoc />
    public async Task<string> GetWordAsync(long id, CancellationToken token)
    {
      // sanity check
      ThrowIfDisposed();

      // look for that word using the given id.
      SelectId.Value = id;
      var value = await _factory.ExecuteReadOneAsync(SelectWordCommand, token).ConfigureAwait(false);
      if (null == value || value == DBNull.Value)
      {
        // we could not find that id
        return null;
      }
      return (string)value;
    }

    /// <inheritdoc />
    public async Task<long> GetIdAsync(string word, CancellationToken token)
    {
      // sanity check
      ThrowIfDisposed();

      // we are first going to look for that id
      // if it does not exist, then we cannot update the files table.
      SelectWord.Value = word;
      var value = await _factory.ExecuteReadOneAsync( SelectIdCommand, token).ConfigureAwait(false);
      if (null == value || value == DBNull.Value)
      {
        // the word does not exist
        // so we cannot really go any further here.
        return -1;
      }
      return (long) value;
    }

    /// <inheritdoc />
    public async Task<long> InsertAsync(string word, CancellationToken token)
    {
      // sanity check
      ThrowIfDisposed();

      // insert the word.
      InsertWord.Value = word;
      InsertLen.Value = word.Length;
      await _factory.ExecuteWriteAsync(InsertCommand, token).ConfigureAwait(false);

      // regardless of the result, get the id
      // if it existed, get the id
      // if we inserted it, get the id.
      return await GetIdAsync(word, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteWordAsync(long id, CancellationToken token)
    {
      // sanity check
      ThrowIfDisposed();

      // delete by word and file id
      DeleteByIdId.Value = id;
      return await _factory.ExecuteWriteAsync(DeleteByIdCommand, token).ConfigureAwait(false) == 1;
    }
  }
}
