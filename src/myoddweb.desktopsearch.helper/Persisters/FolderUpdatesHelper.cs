using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Enums;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.helper.Persisters
{
  internal class PersisterDeleteFolderUpdatesHelper : PersisterHelper
  {
    /// <summary>
    /// The folder id
    /// </summary>
    private IDbDataParameter _id;

    /// <summary>
    /// The folder id
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

    public PersisterDeleteFolderUpdatesHelper(IConnectionFactory factory, string sql) : base(factory, sql)
    {
    }

    public async Task DeleteAsync(IReadOnlyCollection<long> ids, CancellationToken token)
    {
      if (!ids.Any())
      {
        return;
      }

      using (await Lock.TryAsync().ConfigureAwait(false))
      {
        foreach (var id in ids)
        {
          token.ThrowIfCancellationRequested();

          // insert the path.
          Id.Value = id;
          await Factory.ExecuteWriteAsync(Command, token).ConfigureAwait(false);
        }
      }
    }
  }

  internal class PersisterTouchFolderUpdatesHelper : PersisterHelper
  {
    /// <summary>
    /// The folder id
    /// </summary>
    private IDbDataParameter _id;

    /// <summary>
    /// The touch type
    /// </summary>
    private IDbDataParameter _type;

    /// <summary>
    /// The touch time/ticks
    /// </summary>
    private IDbDataParameter _ticks;

    /// <summary>
    /// The folder id
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

    /// <summary>
    /// The folder id
    /// </summary>
    public IDbDataParameter Type
    {
      get
      {
        if (null != _type)
        {
          return _type;
        }

        _type = Command.CreateParameter();
        _type.DbType = DbType.Int64;
        _type.ParameterName = "@type";
        Command.Parameters.Add(_type);
        return _type;
      }
    }

    /// <summary>
    /// The folder ticks
    /// </summary>
    public IDbDataParameter Ticks
    {
      get
      {
        if (null != _ticks)
        {
          return _ticks;
        }

        _ticks = Command.CreateParameter();
        _ticks.DbType = DbType.Int64;
        _ticks.ParameterName = "@ticks";
        Command.Parameters.Add(_ticks);
        return _ticks;
      }
    }

    public PersisterTouchFolderUpdatesHelper(IConnectionFactory factory, string sql) : base(factory, sql)
    {
    }

    public async Task TouchAsync(IReadOnlyCollection<long> ids, UpdateType type, CancellationToken token)
    {
      if (!ids.Any())
      {
        return;
      }

      using (await Lock.TryAsync().ConfigureAwait(false))
      {
        Type.Value = (long)type;
        Ticks.Value = DateTime.UtcNow.Ticks;

        foreach (var id in ids)
        {
          token.ThrowIfCancellationRequested();

          // insert the path.
          Id.Value = id;
          await Factory.ExecuteWriteAsync(Command, token).ConfigureAwait(false);
        }
      }
    }
  }

  public class FolderUpdatesHelper : IFolderUpdatesHelper
  {
    #region Member variables
    /// <summary>
    /// Delete a folder by id
    /// </summary>
    private readonly PersisterDeleteFolderUpdatesHelper _delete;

    /// <summary>
    /// Touch multiple types
    /// </summary>
    private readonly PersisterTouchFolderUpdatesHelper _touch;

    /// <summary>
    /// Check if this item has been disposed or not.
    /// </summary>
    private bool _disposed;
    #endregion

    public FolderUpdatesHelper(IConnectionFactory factory, string tableName)
    {
      // delete
      _delete = new PersisterDeleteFolderUpdatesHelper(factory, $"DELETE FROM {tableName} WHERE folderid = @id");

      // type
      _touch = new PersisterTouchFolderUpdatesHelper( factory, $"INSERT INTO {tableName} (folderid, type, ticks) VALUES (@id, @type, @ticks)");
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

      _delete?.Dispose();
      _touch?.Dispose();
    }

    /// <inheritdoc />
    public Task DeleteAsync(IReadOnlyCollection<long> ids, CancellationToken token)
    {
      ThrowIfDisposed();

      // delete the given ids.
      return _delete.DeleteAsync(ids, token);
    }

    /// <inheritdoc />
    public Task TouchAsync(IReadOnlyCollection<long> folderIds, UpdateType type, CancellationToken token)
    {
      ThrowIfDisposed();

      return _touch.TouchAsync(folderIds, type, token );
    }

  }
}
