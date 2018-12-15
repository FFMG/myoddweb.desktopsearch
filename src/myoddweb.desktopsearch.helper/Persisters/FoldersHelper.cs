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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.helper.Persisters
{
  internal class PersisterInsertFoldersHelper : PersisterHelper
  {
    /// <summary>
    /// The insert path parameter;
    /// </summary>
    private IDbDataParameter _path;

    /// <summary>
    /// The insert path parameter;
    /// </summary>
    public IDbDataParameter Path
    {
      get
      {
        if (null != _path)
        {
          return _path;
        }

        _path = Command.CreateParameter();
        _path.DbType = DbType.String;
        _path.ParameterName = "@path";
        Command.Parameters.Add(_path);
        return _path;
      }
    }

    public PersisterInsertFoldersHelper(IConnectionFactory factory, string sql) : base(factory, sql)
    {
    }

    public async Task InsertAsync(DirectoryInfo directory, CancellationToken token)
    {
      using (await Lock.TryAsync().ConfigureAwait(false))
      {
        // the path we are adding/getting.
        var path = directory.FullName.ToLowerInvariant();

        // insert the path.
        Path.Value = path;
        await Factory.ExecuteWriteAsync(Command, token).ConfigureAwait(false);
      }
    }
  }

  internal class PersisterRenameFoldersHelper : PersisterHelper
  {
    /// <summary>
    /// The insert path parameter;
    /// </summary>
    private IDbDataParameter _path1;

    /// <summary>
    /// The insert path parameter;
    /// </summary>
    private IDbDataParameter _path2;

    /// <summary>
    /// The insert path parameter;
    /// </summary>
    public IDbDataParameter Path1
    {
      get
      {
        if (null != _path1)
        {
          return _path1;
        }

        _path1 = Command.CreateParameter();
        _path1.DbType = DbType.String;
        _path1.ParameterName = "@path1";
        Command.Parameters.Add(_path1);
        return _path1;
      }
    }

    /// <summary>
    /// The insert path parameter;
    /// </summary>
    public IDbDataParameter Path2
    {
      get
      {
        if (null != _path2)
        {
          return _path2;
        }

        _path2 = Command.CreateParameter();
        _path2.DbType = DbType.String;
        _path2.ParameterName = "@path2";
        Command.Parameters.Add(_path2);
        return _path2;
      }
    }

    public PersisterRenameFoldersHelper(IConnectionFactory factory, string sql) : base(factory, sql)
    {
    }

    public async Task<bool> RenameAsync(DirectoryInfo directory, DirectoryInfo oldDirectory, CancellationToken token)
    {
      using (await Lock.TryAsync().ConfigureAwait(false))
      {
        // the path we are adding/getting.
        var path1 = directory.FullName.ToLowerInvariant();
        var path2 = oldDirectory.FullName.ToLowerInvariant();

        // insert the path.
        Path1.Value = path1;
        Path2.Value = path2;
        return (1 == await Factory.ExecuteWriteAsync(Command, token).ConfigureAwait(false));
      }
    }
  }

  internal class PersisterDeleteFoldersHelper : PersisterHelper
  {
    /// <summary>
    /// The insert path parameter;
    /// </summary>
    private IDbDataParameter _path;

    /// <summary>
    /// The insert path parameter;
    /// </summary>
    public IDbDataParameter Path
    {
      get
      {
        if (null != _path)
        {
          return _path;
        }

        _path = Command.CreateParameter();
        _path.DbType = DbType.String;
        _path.ParameterName = "@path";
        Command.Parameters.Add(_path);
        return _path;
      }
    }

    public PersisterDeleteFoldersHelper(IConnectionFactory factory, string sql) : base(factory, sql)
    {
    }

    public async Task<bool> DeleteAsync(DirectoryInfo directory, CancellationToken token)
    {
      using (await Lock.TryAsync().ConfigureAwait(false))
      {
        // the path we are adding/getting.
        var path = directory.FullName.ToLowerInvariant();

        // insert the path.
        Path.Value = path;
        return (1 == await Factory.ExecuteWriteAsync(Command, token).ConfigureAwait(false));
      }
    }
  }

  internal class PersisterSelectFoldersHelper : PersisterHelper
  {
    /// <summary>
    /// The insert path parameter;
    /// </summary>
    private IDbDataParameter _path;

    /// <summary>
    /// The insert path parameter;
    /// </summary>
    public IDbDataParameter Path
    {
      get
      {
        if (null != _path)
        {
          return _path;
        }

        _path = Command.CreateParameter();
        _path.DbType = DbType.String;
        _path.ParameterName = "@path";
        Command.Parameters.Add(_path);
        return _path;
      }
    }

    public PersisterSelectFoldersHelper(IConnectionFactory factory, string sql) : base(factory, sql)
    {
    }

    public async Task<long> GetIdAsync(DirectoryInfo directory, CancellationToken token)
    {
      using (await Lock.TryAsync().ConfigureAwait(false))
      {
        // the path we are adding/getting.
        var path = directory.FullName.ToLowerInvariant();

        // look for the given path
        Path.Value = path;
        var value = await Factory.ExecuteReadOneAsync(Command, token).ConfigureAwait(false);
        if (null == value || value == DBNull.Value)
        {
          // could not find the path.
          // so we can get out.
          return -1;
        }
        return (long)value;
      }
    }
  }

  internal class PersisterSelectPathFoldersHelper : PersisterHelper
  {
    /// <summary>
    /// The id parameter;
    /// </summary>
    private IDbDataParameter _id;

    /// <summary>
    /// The id parameter;
    /// </summary>
    public IDbDataParameter Id
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

    public PersisterSelectPathFoldersHelper(IConnectionFactory factory, string sql) : base(factory, sql)
    {
    }

    public async Task<string> GetPathAsync(long id, CancellationToken token)
    {
      using (await Lock.TryAsync().ConfigureAwait(false))
      {
        // look for the given path
        Id.Value = id;
        var value = await Factory.ExecuteReadOneAsync(Command, token).ConfigureAwait(false);
        if (null == value || value == DBNull.Value)
        {
          // could not find the path.
          // so we can get out.
          return null;
        }
        return (string)value;
      }
    }
  }

  public class FoldersHelper : IFoldersHelper
  {
    #region Member variables
    /// <summary>
    /// The words helper.
    /// </summary>
    private readonly PersisterInsertFoldersHelper _insert;

    /// <summary>
    /// Select a single path id.
    /// </summary>
    private readonly PersisterSelectFoldersHelper _selectId;

    /// <summary>
    /// Select a single path.
    /// </summary>
    private readonly PersisterSelectPathFoldersHelper _selectPath;

    /// <summary>
    /// Delete a folder by path
    /// </summary>
    private readonly PersisterDeleteFoldersHelper _delete;

    /// <summary>
    /// Rename a path
    /// </summary>
    private readonly PersisterRenameFoldersHelper _rename;

    /// <summary>
    /// Check if this item has been disposed or not.
    /// </summary>
    private bool _disposed;
    #endregion

    public FoldersHelper(IConnectionFactory factory, string tableName)
    {
      // create the word command.
      _insert = new PersisterInsertFoldersHelper(factory, $"INSERT INTO {tableName} (path) VALUES (@path)");

      // create the select id
      _selectId = new PersisterSelectFoldersHelper(factory, $"SELECT id FROM {tableName} where path=@path");

      // create the select path
      _selectPath = new PersisterSelectPathFoldersHelper(factory, $"SELECT path FROM {tableName} WHERE id = @id");

      // delete
      _delete = new PersisterDeleteFoldersHelper(factory, $"DELETE FROM {tableName} WHERE path=@path");

      // rename
      _rename = new PersisterRenameFoldersHelper(factory, $"UPDATE {tableName} SET path=@path1 WHERE path=@path2");
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


    public void Dispose()
    {
      if (_disposed)
      {
        return;
      }

      // we are now done.
      _disposed = true;

      _insert?.Dispose();
      _selectId?.Dispose();
      _selectPath?.Dispose();
      _delete?.Dispose();
      _rename?.Dispose();
    }

    /// <inheritdoc />
    public Task<string> GetAsync(long directoryId, CancellationToken token)
    {
      ThrowIfDisposed();

      // get the id.
      return _selectPath.GetPathAsync(directoryId, token);
    }

    /// <inheritdoc />
    public Task<long> GetAsync(DirectoryInfo directory, CancellationToken token)
    {
      ThrowIfDisposed();

      // get the id.
      return _selectId.GetIdAsync(directory, token);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(DirectoryInfo directory, CancellationToken token)
    {
      ThrowIfDisposed();

      // delete the given directory.
      return _delete.DeleteAsync(directory, token);
    }

    /// <inheritdoc />
    public Task<bool> RenameAsync(DirectoryInfo directory, DirectoryInfo oldDirectory, CancellationToken token)
    {
      ThrowIfDisposed();

      // rename
      return _rename.RenameAsync(directory, oldDirectory, token);
    }

    /// <inheritdoc />
    public async Task<long> InsertAndGetAsync(DirectoryInfo directory, CancellationToken token)
    {
      ThrowIfDisposed();

      // try and add the path
      await _insert.InsertAsync(directory, token).ConfigureAwait(false);

      // and return the value for that path
      return await _selectId.GetIdAsync(directory, token).ConfigureAwait(false);
    }
  }
}
