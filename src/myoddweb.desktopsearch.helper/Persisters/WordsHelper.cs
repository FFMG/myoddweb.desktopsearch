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
  public class WordsHelper : IWordsHelper
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
    /// The sql string that we will use to insert a word.
    /// We will only insert if there are no duplicates.
    /// </summary>
    private string InsertSql => $"INSERT OR IGNORE INTO {_tableName} (word) VALUES (@word)";

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
    #endregion

    #region Select
    /// <summary>
    /// The select command;
    /// </summary>
    private IDbCommand _selectCommand;

    /// <summary>
    /// The insert word parameter;
    /// </summary>
    private IDbDataParameter _selectword;

    /// <summary>
    /// The sql string that we will use to look for an id.
    /// </summary>
    private string SelectSql => $"SELECT id FROM {_tableName} WHERE word = @word";

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

        lock (_lock)
        {
          if (_selectCommand == null)
          {
            _selectCommand = _factory.CreateCommand(SelectSql);
          }
          return _selectCommand;
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
        if (null != _selectword)
        {
          return _selectword;
        }

        lock (_lock)
        {
          if (null == _selectword)
          {
            _selectword = SelectCommand.CreateParameter();
            _selectword.DbType = DbType.String;
            _selectword.ParameterName = "@word";
            SelectCommand.Parameters.Add(_selectword);
          }
          return _selectword;
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

    public WordsHelper(IConnectionFactory factory, string tableName )
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
      _selectCommand?.Dispose();
    }

    /// <inheritdoc />
    public async Task<long> GetIdAsync(string word, CancellationToken token)
    {
      // sanity check
      ThrowIfDisposed();

      // we are first going to look for that id
      // if it does not exist, then we cannot update the files table.
      SelectWord.Value = word;
      var value = await _factory.ExecuteReadOneAsync( SelectCommand, token).ConfigureAwait(false);
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
      await _factory.ExecuteWriteAsync(InsertCommand, token).ConfigureAwait(false);

      // regardless of the result, get the id
      // if it existed, get the id
      // if we inserted it, get the id.
      return await GetIdAsync(word, token).ConfigureAwait(false);
    }
  }
}
