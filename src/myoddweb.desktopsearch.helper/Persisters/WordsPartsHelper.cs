﻿//This file is part of Myoddweb.DesktopSearch.
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.helper.Persisters
{
  internal class PersisterSelectWordPartsHelper : PersisterHelper
  {
    /// <summary>
    /// The insert word parameter;
    /// </summary>
    private IDbDataParameter _wordId;

    /// <summary>
    /// The insert word parameter;
    /// </summary>
    public IDbDataParameter WordId
    {
      get
      {
        if (null != _wordId)
        {
          return _wordId;
        }

        _wordId = Command.CreateParameter();
        _wordId.DbType = DbType.Int64;
        _wordId.ParameterName = "@wordId";
        Command.Parameters.Add(_wordId);
        return _wordId;
      }
    }

    public PersisterSelectWordPartsHelper(IConnectionFactory factory, string sql) : base(factory, sql)
    {
    }

    public async Task<IList<long>> GetAsync(long wordId, CancellationToken token)
    {
      using (await Lock.TryAsync(token).ConfigureAwait(false))
      {
        // select all the ids that belong to that word.
        WordId.Value = wordId;
        using (var reader = await Factory.ExecuteReadAsync( Command, token).ConfigureAwait(false))
        {
          var partIds = new List<long>();

          // now read the part ids as needed.
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
  }

  internal class PersisterInsertWordPartsHelper : PersisterHelper
  {
    /// <summary>
    /// The insert word parameter;
    /// </summary>
    private IDbDataParameter _wordId;

    /// <summary>
    /// The part id.
    /// </summary>
    private IDbDataParameter _partId; 

    /// <summary>
    /// The insert word parameter;
    /// </summary>
    public IDbDataParameter WordId
    {
      get
      {
        if (null != _wordId)
        {
          return _wordId;
        }

        _wordId = Command.CreateParameter();
        _wordId.DbType = DbType.Int64;
        _wordId.ParameterName = "@wordId";
        Command.Parameters.Add(_wordId);
        return _wordId;
      }
    }

    /// <summary>
    /// The part id Insert parameter.
    /// </summary>
    private IDbDataParameter PartId
    {
      get
      {
        if (null != _partId)
        {
          return _partId;
        }

        _partId = Command.CreateParameter();
        _partId.DbType = DbType.Int64;
        _partId.ParameterName = "@partId";
        Command.Parameters.Add(_partId);
        return _partId;
      }
    }
    
    public PersisterInsertWordPartsHelper(IConnectionFactory factory, string sql) : base(factory, sql)
    {
    }

    /// <summary>
    /// Try and insert id, return the ids we could not insert.
    /// </summary>
    /// <param name="wordId"></param>
    /// <param name="partIds"></param>
    /// <param name="token"></param>
    /// <returns>The list of ids we could not insert, for whatever reason.</returns>
    public async Task<IList<long>> InsertAsync(long wordId, IReadOnlyCollection<long> partIds, CancellationToken token)
    {
      if (!partIds.Any())
      {
        return new List<long>();
      }

      using (await Lock.TryAsync(token).ConfigureAwait(false))
      {
        var invalidIds = new List<long>();
        // insert the word.
        WordId.Value = wordId;
        foreach (var partId in partIds)
        {
          // check if we must get out
          token.ThrowIfCancellationRequested();

          // try and insert the id.
          PartId.Value = partId;
          if (1 != await Factory.ExecuteWriteAsync(Command, token).ConfigureAwait(false))
          {
            invalidIds.Add(partId);
          }
        }
        return invalidIds;
      }
    }
  }

  internal class PersisterExistsWordPartsHelper : PersisterHelper
  {
    /// <summary>
    /// The insert word parameter;
    /// </summary>
    private IDbDataParameter _wordId;

    /// <summary>
    /// The part id.
    /// </summary>
    private IDbDataParameter _partId;

    /// <summary>
    /// The insert word parameter;
    /// </summary>
    public IDbDataParameter WordId
    {
      get
      {
        if (null != _wordId)
        {
          return _wordId;
        }

        _wordId = Command.CreateParameter();
        _wordId.DbType = DbType.Int64;
        _wordId.ParameterName = "@wordId";
        Command.Parameters.Add(_wordId);
        return _wordId;
      }
    }

    /// <summary>
    /// The part id Insert parameter.
    /// </summary>
    private IDbDataParameter PartId
    {
      get
      {
        if (null != _partId)
        {
          return _partId;
        }

        _partId = Command.CreateParameter();
        _partId.DbType = DbType.Int64;
        _partId.ParameterName = "@partId";
        Command.Parameters.Add(_partId);
        return _partId;
      }
    }

    public PersisterExistsWordPartsHelper(IConnectionFactory factory, string sql) : base(factory, sql)
    {
    }

    /// <summary>
    /// Check if the given ids exist, return the list of ids that do not exist.
    /// </summary>
    /// <param name="wordId"></param>
    /// <param name="partIds"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<IList<long>> ExistAsync(long wordId, IReadOnlyCollection<long> partIds, CancellationToken token)
    {
      if (!partIds.Any())
      {
        return new List<long>();
      }

      using (await Lock.TryAsync(token).ConfigureAwait(false))
      {
        // insert the word.
        WordId.Value = wordId;

        var unknownIds = new List<long>();
        foreach (var partId in partIds)
        {
          // get out if needed
          token.ThrowIfCancellationRequested();

          // the part we are checking
          PartId.Value = partId;

          // get the value if we have one
          var value = await Factory.ExecuteReadOneAsync(Command, token).ConfigureAwait(false);

          // return if we found a value.
          if (null != value && value != DBNull.Value)
          {
            continue;
          }

          // this id does not exist
          unknownIds.Add(partId);
        }

        return unknownIds;
      }
    }
  }

  internal class PersisterDeleteWordPartsHelper : PersisterHelper
  {
    /// <summary>
    /// The insert word parameter;
    /// </summary>
    private IDbDataParameter _wordId;

    /// <summary>
    /// The part id.
    /// </summary>
    private IDbDataParameter _partId;

    /// <summary>
    /// The insert word parameter;
    /// </summary>
    public IDbDataParameter WordId
    {
      get
      {
        if (null != _wordId)
        {
          return _wordId;
        }

        _wordId = Command.CreateParameter();
        _wordId.DbType = DbType.Int64;
        _wordId.ParameterName = "@wordId";
        Command.Parameters.Add(_wordId);
        return _wordId;
      }
    }

    /// <summary>
    /// The part id Insert parameter.
    /// </summary>
    private IDbDataParameter PartId
    {
      get
      {
        if (null != _partId)
        {
          return _partId;
        }

        _partId = Command.CreateParameter();
        _partId.DbType = DbType.Int64;
        _partId.ParameterName = "@partId";
        Command.Parameters.Add(_partId);
        return _partId;
      }
    }

    public PersisterDeleteWordPartsHelper(IConnectionFactory factory, string sql) : base(factory, sql)
    {
    }

    public async Task<bool> DeleteAsync(long wordId, long partId, CancellationToken token)
    {
      using (await Lock.TryAsync(token).ConfigureAwait(false))
      {
        // insert the word.
        WordId.Value = wordId;
        PartId.Value = partId;

        // select all the ids that belong to that word.
        return 0 != await Factory.ExecuteWriteAsync(Command, token).ConfigureAwait(false);
      }
    }
  }

  public class WordsPartsHelper : IWordsPartsHelper
  {
    #region Member variables
    /// <summary>
    /// The insert helper.
    /// </summary>
    private readonly MultiplePersisterHelper<PersisterInsertWordPartsHelper> _insert;

    /// <summary>
    /// Check if a word/part exists.
    /// </summary>
    private readonly MultiplePersisterHelper<PersisterExistsWordPartsHelper> _exists;

    /// <summary>
    /// Get the value by id.
    /// </summary>
    private readonly MultiplePersisterHelper<PersisterSelectWordPartsHelper> _select;

    /// <summary>
    /// Delete by word/part
    /// </summary>
    private readonly MultiplePersisterHelper<PersisterDeleteWordPartsHelper> _delete;

    /// <summary>
    /// Check if this item has been disposed or not.
    /// </summary>
    private bool _disposed;
    #endregion

    public WordsPartsHelper(IConnectionFactory factory, string tableName)
    {
      const int numberOfItems = 10;

      // the insert command.
      _insert = new MultiplePersisterHelper<PersisterInsertWordPartsHelper>( () => new PersisterInsertWordPartsHelper(factory, $"INSERT OR IGNORE INTO {tableName} (wordid, partid) VALUES (@wordid, @partid)"), numberOfItems);

      // exists?
      _exists = new MultiplePersisterHelper<PersisterExistsWordPartsHelper>(() => new PersisterExistsWordPartsHelper( factory, $"SELECT 1 FROM {tableName} WHERE wordid=@wordid and partid=@partid" ), numberOfItems);

      // select by word id.
      _select = new MultiplePersisterHelper<PersisterSelectWordPartsHelper>(() => new PersisterSelectWordPartsHelper( factory, $"SELECT partid FROM {tableName} WHERE wordid = @wordid"), numberOfItems);

      // delete by word/part id.
      _delete = new MultiplePersisterHelper<PersisterDeleteWordPartsHelper>(() => new PersisterDeleteWordPartsHelper(factory, $"DELETE FROM {tableName} WHERE wordid=@wordid AND partid=@partid"), numberOfItems);
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

      _insert?.Dispose();
      _exists?.Dispose();
      _select?.Dispose();
      _delete?.Dispose();
    }

    /// <inheritdoc />
    public Task<IList<long>> ExistsAsync(long wordId, IReadOnlyCollection<long> partIds, CancellationToken token)
    {
      // sanity check
      ThrowIfDisposed();

      // return if it exists.
      return _exists.Next().ExistAsync(wordId, partIds, token);
    }

    /// <inheritdoc />
    public Task<IList<long>> GetPartIdsAsync(long wordid, CancellationToken token )
    {
      // sanity check
      ThrowIfDisposed();

      // get the data
      return _select.Next().GetAsync(wordid, token);
    }

    /// <inheritdoc />
    public async Task<IList<long>> InsertAsync(long wordId, IReadOnlyCollection<long> partIds, CancellationToken token)
    {
      // sanity check
      ThrowIfDisposed();

      // try and insert the ids
      var invalidInsertIds = await _insert.Next().InsertAsync(wordId, partIds, token).ConfigureAwait(false);

      // now check the invalid ids to check if they exist.
      return await ExistsAsync(wordId, invalidInsertIds.ToList(), token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(long wordId, long partId, CancellationToken token)
    {
      // sanity check
      ThrowIfDisposed();

      // delete the word/part/
      return _delete.Next().DeleteAsync(wordId, partId, token);
    }
  }
}
