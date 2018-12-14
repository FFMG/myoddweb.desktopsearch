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
  internal class PersisterInsertWordHelper : PersisterHelper
  {
    /// <summary>
    /// The insert word parameter;
    /// </summary>
    private IDbDataParameter _word;

    /// <summary>
    /// The insert word parameter;
    /// </summary>
    public IDbDataParameter Word
    {
      get
      {
        if (null != _word)
        {
          return _word;
        }

        _word = Command.CreateParameter();
        _word.DbType = DbType.String;
        _word.ParameterName = "@word";
        Command.Parameters.Add(_word);
        return _word;
      }
    }

    public PersisterInsertWordHelper(IConnectionFactory factory, string sql) : base(factory, sql)
    {
    }

    public async Task InsertAsync(string word, CancellationToken token)
    {
      using (await Lock.TryAsync().ConfigureAwait(false))
      {
        // insert the word.
        Word.Value = word;
        await Factory.ExecuteWriteAsync(Command, token).ConfigureAwait(false);
      }
    }
  }

  internal class PersisterSelectWordHelper : PersisterHelper
  {
    /// <summary>
    /// The insert word parameter;
    /// </summary>
    private IDbDataParameter _word;

    /// <summary>
    /// The insert word parameter;
    /// </summary>
    public IDbDataParameter Word
    {
      get
      {
        if (null != _word)
        {
          return _word;
        }

        _word = Command.CreateParameter();
        _word.DbType = DbType.String;
        _word.ParameterName = "@word";
        Command.Parameters.Add(_word);
        return _word;
      }
    }

    public PersisterSelectWordHelper(IConnectionFactory factory, string sql) : base(factory, sql)
    {
    }

    public async Task<long> GetIdAsync(string word, CancellationToken token)
    {
      using (await Lock.TryAsync().ConfigureAwait(false))
      {
        // we are first going to look for that id
        // if it does not exist, then we cannot update the files table.
        Word.Value = word;
        var value = await Factory.ExecuteReadOneAsync(Command, token).ConfigureAwait(false);
        if (null == value || value == DBNull.Value)
        {
          // could not find the word.
          // so we can get out.
          return -1;
        }
        return (long)value;
      }
    }
  }

  public class WordsHelper : IWordsHelper
  {
    #region Member variables

    /// <summary>
    /// The words helper.
    /// </summary>
    private readonly PersisterInsertWordHelper _insert;

    /// <summary>
    /// Select a single word id.
    /// </summary>
    private readonly PersisterSelectWordHelper _select;

    /// <summary>
    /// Check if this item has been disposed or not.
    /// </summary>
    private bool _disposed;
    #endregion

    public WordsHelper(IConnectionFactory factory, string tableName )
    {
      // create the word command.
      _insert = new PersisterInsertWordHelper(factory, $"INSERT OR IGNORE INTO {tableName} (word) VALUES (@word)" );

      // create the select
      _select = new PersisterSelectWordHelper( factory, $"SELECT id FROM {tableName} WHERE word = @word" );
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
      _insert?.Dispose();
      _select?.Dispose();
    }

    /// <inheritdoc />
    public Task<long> GetIdAsync(string word, CancellationToken token)
    {
      // sanity check
      ThrowIfDisposed();

      // then return the value.
      return _select.GetIdAsync(word, token);
    }

    /// <inheritdoc />
    public async Task<long> InsertAndGetIdAsync(string word, CancellationToken token)
    {
      // sanity check
      ThrowIfDisposed();

      // just add it.
      await _insert.InsertAsync(word, token).ConfigureAwait(false);

      // regardless of the result, get the id
      // if it existed, get the id
      // if we inserted it, get the id.
      return await GetIdAsync(word, token).ConfigureAwait(false);
    }
  }
}
