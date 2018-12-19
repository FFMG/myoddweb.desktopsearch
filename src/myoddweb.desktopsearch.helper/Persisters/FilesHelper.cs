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
  internal class PersisterInsertFilesHelper : PersisterHelper
  {
    /// <summary>
    /// The id parameter;
    /// </summary>
    private IDbDataParameter _folderid;

    /// <summary>
    /// The name parameter;
    /// </summary>
    private IDbDataParameter _name;

    /// <summary>
    /// The id parameter;
    /// </summary>
    public IDbDataParameter FolderId
    {
      get
      {
        if (null != _folderid)
        {
          return _folderid;
        }

        _folderid = Command.CreateParameter();
        _folderid.DbType = DbType.Int64;
        _folderid.ParameterName = "@folderid";
        Command.Parameters.Add(_folderid);
        return _folderid;
      }
    }

    /// <summary>
    /// The id parameter;
    /// </summary>
    public IDbDataParameter Name
    {
      get
      {
        if (null != _name)
        {
          return _name;
        }

        _name = Command.CreateParameter();
        _name.DbType = DbType.String;
        _name.ParameterName = "@name";
        Command.Parameters.Add(_name);
        return _name;
      }
    }

    public PersisterInsertFilesHelper(IConnectionFactory factory, string sql) : base(factory, sql)
    {
    }

    public async Task InsertAsync(long folderid, string name, CancellationToken token)
    {
      using (await Lock.TryAsync(token).ConfigureAwait(false))
      {
        // look for the given path
        FolderId.Value = folderid;
        Name.Value = name.ToLowerInvariant();
        await Factory.ExecuteWriteAsync(Command, token).ConfigureAwait(false);
      }
    }
  }

  internal class PersisterDeleteFilesHelper : PersisterHelper
  {
    /// <summary>
    /// The id parameter;
    /// </summary>
    private IDbDataParameter _folderid;

    /// <summary>
    /// The name parameter;
    /// </summary>
    private IDbDataParameter _name;

    /// <summary>
    /// The id parameter;
    /// </summary>
    public IDbDataParameter FolderId
    {
      get
      {
        if (null != _folderid)
        {
          return _folderid;
        }

        _folderid = Command.CreateParameter();
        _folderid.DbType = DbType.Int64;
        _folderid.ParameterName = "@folderid";
        Command.Parameters.Add(_folderid);
        return _folderid;
      }
    }

    /// <summary>
    /// The id parameter;
    /// </summary>
    public IDbDataParameter Name
    {
      get
      {
        if (null != _name)
        {
          return _name;
        }

        _name = Command.CreateParameter();
        _name.DbType = DbType.String;
        _name.ParameterName = "@name";
        Command.Parameters.Add(_name);
        return _name;
      }
    }

    public PersisterDeleteFilesHelper(IConnectionFactory factory, string sql) : base(factory, sql)
    {
    }

    public async Task<bool> DeleteAsync(long folderid, string name, CancellationToken token)
    {
      using (await Lock.TryAsync(token).ConfigureAwait(false))
      {
        // look for the given path
        FolderId.Value = folderid;
        Name.Value = name.ToLowerInvariant();
        return 1 == await Factory.ExecuteWriteAsync(Command, token).ConfigureAwait(false);
      }
    }
  }

  internal class PersisterDeleteFolderFilesHelper : PersisterHelper
  {
    /// <summary>
    /// The id parameter;
    /// </summary>
    private IDbDataParameter _folderid;

    /// <summary>
    /// The id parameter;
    /// </summary>
    public IDbDataParameter FolderId
    {
      get
      {
        if (null != _folderid)
        {
          return _folderid;
        }

        _folderid = Command.CreateParameter();
        _folderid.DbType = DbType.Int64;
        _folderid.ParameterName = "@folderid";
        Command.Parameters.Add(_folderid);
        return _folderid;
      }
    }

    public PersisterDeleteFolderFilesHelper(IConnectionFactory factory, string sql) : base(factory, sql)
    {
    }

    public async Task<int> DeleteAsync(long folderid, CancellationToken token)
    {
      using (await Lock.TryAsync(token).ConfigureAwait(false))
      {
        FolderId.Value = folderid;
        return await Factory.ExecuteWriteAsync(Command, token).ConfigureAwait(false);
      }
    }
  }

  internal class PersisterSelectIdFilesHelper : PersisterHelper
  {
    /// <summary>
    /// The id parameter;
    /// </summary>
    private IDbDataParameter _folderid;

    /// <summary>
    /// The name parameter;
    /// </summary>
    private IDbDataParameter _name;

    /// <summary>
    /// The id parameter;
    /// </summary>
    public IDbDataParameter FolderId
    {
      get
      {
        if (null != _folderid)
        {
          return _folderid;
        }

        _folderid = Command.CreateParameter();
        _folderid.DbType = DbType.Int64;
        _folderid.ParameterName = "@folderid";
        Command.Parameters.Add(_folderid);
        return _folderid;
      }
    }

    /// <summary>
    /// The id parameter;
    /// </summary>
    public IDbDataParameter Name
    {
      get
      {
        if (null != _name)
        {
          return _name;
        }

        _name = Command.CreateParameter();
        _name.DbType = DbType.String;
        _name.ParameterName = "@name";
        Command.Parameters.Add(_name);
        return _name;
      }
    }

    public PersisterSelectIdFilesHelper(IConnectionFactory factory, string sql) : base(factory, sql)
    {
    }

    public async Task<long> GetAsync(long folderid, string name, CancellationToken token)
    {
      using (await Lock.TryAsync(token).ConfigureAwait(false))
      {
        // look for the given path
        FolderId.Value = folderid;
        Name.Value = name.ToLowerInvariant();
        var value = await Factory.ExecuteReadOneAsync(Command, token).ConfigureAwait(false);
        if (null == value || value == DBNull.Value)
        {
          return -1;
        }

        return (long) value;
      }
    }
  }

  internal class PersisterSelectIdsFilesHelper : PersisterHelper
  {
    /// <summary>
    /// The id parameter;
    /// </summary>
    private IDbDataParameter _folderid;

    /// <summary>
    /// The id parameter;
    /// </summary>
    public IDbDataParameter FolderId
    {
      get
      {
        if (null != _folderid)
        {
          return _folderid;
        }

        _folderid = Command.CreateParameter();
        _folderid.DbType = DbType.Int64;
        _folderid.ParameterName = "@folderid";
        Command.Parameters.Add(_folderid);
        return _folderid;
      }
    }

    public PersisterSelectIdsFilesHelper(IConnectionFactory factory, string sql) : base(factory, sql)
    {
    }

    public async Task<IList<IFileHelper>> GetAsync(long folderid, CancellationToken token)
    {
      using (await Lock.TryAsync(token).ConfigureAwait(false))
      {
        var files = new List<IFileHelper>();

        // look for the given path
        FolderId.Value = folderid;
        using (var reader = await Factory.ExecuteReadAsync(Command, token).ConfigureAwait(false))
        {
          var idPos = reader.GetOrdinal("id");
          var namePos = reader.GetOrdinal("name");

          while (reader.Read())
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // add this id to the list.
            files.Add( new FileHelper((long)reader[idPos], (string)reader[namePos]));
          }

          // return all the files we found.
          return files;
        }
      }
    }
  }

  public class FilesHelper : IFilesHelper
  {
    #region Member variables
    /// <summary>
    /// Get the file ids for one folder.
    /// </summary>
    private readonly PersisterSelectIdsFilesHelper _selectIds;

    /// <summary>
    /// Delete a file by id/name
    /// </summary>
    private readonly PersisterDeleteFilesHelper _delete;

    /// <summary>
    /// Delete all the files for a folder.
    /// </summary>
    private readonly PersisterDeleteFolderFilesHelper _deleteFolder;

    /// <summary>
    /// Select a single id from folder/name
    /// </summary>
    private readonly PersisterSelectIdFilesHelper _selectId;

    /// <summary>
    /// Insert a file/folder/
    /// </summary>
    private readonly PersisterInsertFilesHelper _insert;

    /// <summary>
    /// Check if this item has been disposed or not.
    /// </summary>
    private bool _disposed;
    #endregion

    public FilesHelper(IConnectionFactory factory, string tableName)
    {
      // select the files in a folder.
      _selectIds = new PersisterSelectIdsFilesHelper(factory, $"SELECT id, name FROM {tableName} WHERE folderid=@folderid");

      // select a single file.
      _selectId = new PersisterSelectIdFilesHelper(factory, $"SELECT id FROM {tableName} WHERE folderid=@folderid AND name=@name");

      // delete a file by id/name
      _delete = new PersisterDeleteFilesHelper( factory, $"DELETE FROM {tableName} WHERE folderid=@folderid and name=@name");

      // delete all the files in one folder.
      _deleteFolder = new PersisterDeleteFolderFilesHelper( factory, $"DELETE FROM {tableName} WHERE folderid=@folderid");

      // insert
      _insert = new PersisterInsertFilesHelper(factory, $"INSERT OR IGNORE INTO {tableName} (folderid, name) VALUES (@folderid, @name)");
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

      _selectIds?.Dispose();
      _selectId?.Dispose();
      _delete?.Dispose();
      _deleteFolder?.Dispose();
      _insert?.Dispose();
    }

    /// <inheritdoc />
    public Task<IList<IFileHelper>> GetAsync(long id, CancellationToken token)
    {
      ThrowIfDisposed();
      return _selectIds.GetAsync(id, token);
    }

    /// <inheritdoc />
    public Task<long> GetAsync(long id, string name, CancellationToken token)
    {
      ThrowIfDisposed();
      return _selectId.GetAsync(id, name, token);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(long id, string name, CancellationToken token)
    {
      ThrowIfDisposed();
      return _delete.DeleteAsync(id, name, token);
    }

    /// <inheritdoc />
    public Task<int> DeleteAsync(long id, CancellationToken token)
    {
      ThrowIfDisposed();
      return _deleteFolder.DeleteAsync(id, token);
    }

    /// <inheritdoc />
    public async Task<long> InsertAndGetAsync(long id, string name, CancellationToken token)
    {
      ThrowIfDisposed();

      // insert it
      await _insert.InsertAsync(id, name, token ).ConfigureAwait(false);

      // then try and get the id  
      return await _selectId.GetAsync(id, name, token).ConfigureAwait(false);
    }
  }
}
