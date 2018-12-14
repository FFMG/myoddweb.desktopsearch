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
  internal class PersisterExistsFilesWordsHelper : PersisterHelper
  {
    /// <summary>
    /// The file id
    /// </summary>
    private IDbDataParameter _fileId;

    /// <summary>
    /// The word id.
    /// </summary>
    private IDbDataParameter _wordId;

    /// <summary>
    /// The insert word parameter;
    /// </summary>
    public IDbDataParameter FileId
    {
      get
      {
        if (null != _fileId)
        {
          return _fileId;
        }

        _fileId = Command.CreateParameter();
        _fileId.DbType = DbType.Int64;
        _fileId.ParameterName = "@fileid";
        Command.Parameters.Add(_fileId);
        return _fileId;
      }
    }

    /// <summary>
    /// The part id Insert parameter.
    /// </summary>
    private IDbDataParameter WordId
    {
      get
      {
        if (null != _wordId )
        {
          return _wordId;
        }

        _wordId = Command.CreateParameter();
        _wordId.DbType = DbType.Int64;
        _wordId.ParameterName = "@wordid";
        Command.Parameters.Add(_wordId);
        return _wordId;
      }
    }

    public PersisterExistsFilesWordsHelper(IConnectionFactory factory, string sql) : base(factory, sql)
    {
    }

    public async Task<bool> ExistAsync(long wordId, long fileId, CancellationToken token)
    {
      using (await Lock.TryAsync().ConfigureAwait(false))
      {
        // we are first going to look for that id
        // if it does not exist, then we cannot update the files table.
        WordId.Value = wordId;
        FileId.Value = fileId;
        var value = await Factory.ExecuteReadOneAsync(Command, token).ConfigureAwait(false);

        // return if we found a value.
        return null != value && value != DBNull.Value;
      }
    }
  }

  internal class PersisterInsertFilesWordsHelper : PersisterHelper
  {
    /// <summary>
    /// The file id
    /// </summary>
    private IDbDataParameter _fileId;

    /// <summary>
    /// The word id.
    /// </summary>
    private IDbDataParameter _wordId;

    /// <summary>
    /// The insert word parameter;
    /// </summary>
    public IDbDataParameter FileId
    {
      get
      {
        if (null != _fileId)
        {
          return _fileId;
        }

        _fileId = Command.CreateParameter();
        _fileId.DbType = DbType.Int64;
        _fileId.ParameterName = "@fileid";
        Command.Parameters.Add(_fileId);
        return _fileId;
      }
    }

    /// <summary>
    /// The part id Insert parameter.
    /// </summary>
    private IDbDataParameter WordId
    {
      get
      {
        if (null != _wordId)
        {
          return _wordId;
        }

        _wordId = Command.CreateParameter();
        _wordId.DbType = DbType.Int64;
        _wordId.ParameterName = "@wordid";
        Command.Parameters.Add(_wordId);
        return _wordId;
      }
    }

    public PersisterInsertFilesWordsHelper(IConnectionFactory factory, string sql) : base(factory, sql)
    {
    }

    public async Task<bool> InsertAsync(long wordId, long fileId, CancellationToken token)
    {
      using (await Lock.TryAsync().ConfigureAwait(false))
      {
        // insert the word.
        WordId.Value = wordId;
        FileId.Value = fileId;
        return (1 == await Factory.ExecuteWriteAsync(Command, token).ConfigureAwait(false));
      }
    }
  }

  internal class PersisterDeleteFilesWordsHelper : PersisterHelper
  {
    /// <summary>
    /// The file id
    /// </summary>
    private IDbDataParameter _fileId;

    /// <summary>
    /// The insert word parameter;
    /// </summary>
    public IDbDataParameter FileId
    {
      get
      {
        if (null != _fileId)
        {
          return _fileId;
        }

        _fileId = Command.CreateParameter();
        _fileId.DbType = DbType.Int64;
        _fileId.ParameterName = "@fileid";
        Command.Parameters.Add(_fileId);
        return _fileId;
      }
    }
    
    public PersisterDeleteFilesWordsHelper(IConnectionFactory factory, string sql) : base(factory, sql)
    {
    }

    public async Task<bool> DeleteAsync(long fileId, CancellationToken token)
    {
      using (await Lock.TryAsync().ConfigureAwait(false))
      {
        using (await Lock.TryAsync().ConfigureAwait(false))
        {
          // delete the file
          FileId.Value = fileId;
          return (1 == await Factory.ExecuteWriteAsync(Command, token).ConfigureAwait(false));
        }
      }
    }
  }

  public class FilesWordsHelper : IFilesWordsHelper
  {
    #region Member variables
    /// <summary>
    /// Insert a FileId/WordId
    /// </summary>
    private readonly MultiplePersisterHelper<PersisterInsertFilesWordsHelper> _insert;

    /// <summary>
    /// Check if a word/file exists.
    /// </summary>
    private readonly MultiplePersisterHelper<PersisterExistsFilesWordsHelper> _exists;

    /// <summary>
    /// Delete by file id.
    /// </summary>
    private readonly MultiplePersisterHelper<PersisterDeleteFilesWordsHelper> _delete;

    /// <summary>
    /// Check if this item has been disposed or not.
    /// </summary>
    private bool _disposed;
    #endregion

    public FilesWordsHelper(IConnectionFactory factory, string tableName)
    {
      const int numberOfItems = 10;

      // check if a file exists.
      _exists = new MultiplePersisterHelper<PersisterExistsFilesWordsHelper>( () => new PersisterExistsFilesWordsHelper(factory, $"SELECT 1 FROM {tableName} where wordid=@wordid AND fileid=@fileid" ), numberOfItems);

      // delete
      _delete = new MultiplePersisterHelper<PersisterDeleteFilesWordsHelper>(() => new PersisterDeleteFilesWordsHelper(factory, $"DELETE FROM {tableName} WHERE fileid=@fileid"), numberOfItems);

      // insert
      _insert = new MultiplePersisterHelper<PersisterInsertFilesWordsHelper>(() => new PersisterInsertFilesWordsHelper(factory, $"INSERT OR IGNORE INTO {tableName} (wordid, fileid) VALUES (@wordid, @fileid)"), numberOfItems);
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

      _exists?.Dispose();
      _insert?.Dispose();
      _delete?.Dispose();
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(long wordId, long fileId, CancellationToken token)
    {
      // sanity check
      ThrowIfDisposed();

      return _exists.Next().ExistAsync(wordId, fileId, token);
    }

    /// <inheritdoc />
    public async Task<bool> InsertAsync(long wordId, long fileId, CancellationToken token)
    {
      // sanity check
      ThrowIfDisposed();

      // try and insert the id
      await _insert.Next().InsertAsync(wordId, fileId, token).ConfigureAwait(false);

      // was there an error ... or is it a duplicate.
      // we have to check outside the lock
      return await ExistsAsync(wordId, fileId, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> DeleteFileAsync(long fileId, CancellationToken token)
    {
      // sanity check
      ThrowIfDisposed();

      return _delete.Next().DeleteAsync(fileId, token);
    }
  }
}
