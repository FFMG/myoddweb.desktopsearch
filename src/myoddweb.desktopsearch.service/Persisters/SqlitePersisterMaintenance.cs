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
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal partial class SqlitePersister
  {
    /// <summary>
    /// Create the database to the latest version
    /// </summary>
    protected async Task<bool> CreateDatabase(IConnectionFactory connectionFactory)
    {
      // create the config table.
      if (!await CreateConfigAsync(connectionFactory).ConfigureAwait(false))
      {
        return false;
      }

      // the files tables
      if (!await CreateFilesAsync(connectionFactory).ConfigureAwait(false))
      {
        return false;
      }

      // the words tables
      if (!await CreateWordsAsync(connectionFactory).ConfigureAwait(false))
      {
        return false;
      }

      // the files words tables
      if (!await CreateFilesWordsAsync(connectionFactory).ConfigureAwait(false))
      {
        return false;
      }

      // the folders table.
      if (!await CreateFoldersAsync(connectionFactory).ConfigureAwait(false))
      {
        return false;
      }

      // the folders update table.
      if (!await CreateFoldersUpdateAsync(connectionFactory).ConfigureAwait(false))
      {
        return false;
      }

      // the files update table
      if (!await CreateFilesUpdateAsync(connectionFactory).ConfigureAwait(false))
      {
        return false;
      }

      // the parts table
      if (!await CreatePartsAsync(connectionFactory).ConfigureAwait(false))
      {
        return false;
      }

      // the parts search table
      if (!await CreatePartsSearchAsync(connectionFactory).ConfigureAwait(false))
      {
        return false;
      }

      // the words parts table
      if (!await CreateWordsPartsAsync(connectionFactory).ConfigureAwait(false))
      {
        return false;
      }

      // the words parts table
      if (!await CreateCountsAsync(connectionFactory).ConfigureAwait(false))
      {
        return false;
      }
      return true;
    }

    /// <summary>
    /// All the words
    /// </summary>
    /// <param name="connectionFactory"></param>
    /// <returns></returns>
    private async Task<bool> CreateWordsAsync(IConnectionFactory connectionFactory)
    {
      if (!await
        ExecuteNonQueryAsync($"CREATE TABLE {Tables.Words} (id integer PRIMARY KEY, word TEXT UNIQUE)", connectionFactory)
          .ConfigureAwait(false))
      {
        return false;
      }

      // Note that we do not need to add an index won the WORD
      // this is because the UNIQUE CONSTRAINT is an index.

      return true;
    }

    /// <summary>
    /// All the word ids in a file id
    /// </summary>
    /// <param name="connectionFactory"></param>
    /// <returns></returns>
    private async Task<bool> CreateFilesWordsAsync(IConnectionFactory connectionFactory)
    {
      if (!await
        ExecuteNonQueryAsync($"CREATE TABLE {Tables.FilesWords} (wordid integer, fileid integer)", connectionFactory)
          .ConfigureAwait(false))
      {
        return false;
      }

      // there can only be one word and one id.
      if (
        !await
          ExecuteNonQueryAsync($"CREATE UNIQUE INDEX index_{Tables.FilesWords}_wordid_fileid ON {Tables.FilesWords}(wordid, fileid);", connectionFactory).ConfigureAwait(false))
      {
        return false;
      }

      if (
        !await
          ExecuteNonQueryAsync($"CREATE INDEX index_{Tables.FilesWords}_fileid ON {Tables.FilesWords}(fileid);", connectionFactory).ConfigureAwait(false))
      {
        return false;
      }

      return true;
    }

    private async Task<bool> CreateFilesAsync(IConnectionFactory connectionFactory)
    {
      if (!await
        ExecuteNonQueryAsync($"CREATE TABLE {Tables.Files} (id integer PRIMARY KEY, folderid integer, name varchar(260))", connectionFactory)
          .ConfigureAwait(false))
      {
        return false;
      }

      if (
        !await
          ExecuteNonQueryAsync($"CREATE UNIQUE INDEX index_{Tables.Files}_folderid_name ON {Tables.Files}(folderid, name);", connectionFactory).ConfigureAwait(false))
      {
        return false;
      }

      if (
        !await
          ExecuteNonQueryAsync($"CREATE INDEX index_{Tables.Files}_folderid ON {Tables.Files}(folderid);", connectionFactory).ConfigureAwait(false))
      {
        return false;
      }
      return true;
    }

    /// <summary>
    /// Create the updates table.
    /// </summary>
    /// <param name="connectionFactory"></param>
    /// <returns></returns>
    private async Task<bool> CreateFoldersUpdateAsync(IConnectionFactory connectionFactory)
    {
      if (!await
        ExecuteNonQueryAsync($"CREATE TABLE {Tables.FolderUpdates} (folderid integer, type integer, ticks integer)", connectionFactory)
          .ConfigureAwait(false))
      {
        return false;
      }

      // index to get the last 'x' updated folders.
      if (
        !await
          ExecuteNonQueryAsync($"CREATE INDEX index_{Tables.FolderUpdates}_ticks ON {Tables.FolderUpdates}(ticks);", connectionFactory).ConfigureAwait(false))
      {
        return false;
      }

      // the folderid index so we can add/remove folders once processed.
      if (
        !await
          ExecuteNonQueryAsync($"CREATE INDEX index_{Tables.FolderUpdates}_folderid ON {Tables.FolderUpdates}(folderid);", connectionFactory).ConfigureAwait(false))
      {
        return false;
      }
      return true;
    }

    /// <summary>
    /// Create the counts table.
    /// </summary>
    /// <param name="connectionFactory"></param>
    /// <returns></returns>
    private async Task<bool> CreateCountsAsync(IConnectionFactory connectionFactory)
    {
      if (!await
        ExecuteNonQueryAsync($"CREATE TABLE {Tables.Counts} (type integer, count integer)", connectionFactory)
          .ConfigureAwait(false))
      {
        return false;
      }

      if (
        !await
          ExecuteNonQueryAsync($"CREATE UNIQUE INDEX index_{Tables.Counts}_type ON {Tables.Counts}(type);", connectionFactory).ConfigureAwait(false))
      {
        return false;
      }

      // done 
      return true;
    }

    /// <summary>
    /// Table to link a part of work to a word, (and from a word to a file).
    /// </summary>
    /// <param name="connectionFactory"></param>
    /// <returns></returns>
    private async Task<bool> CreateWordsPartsAsync(IConnectionFactory connectionFactory)
    {
      if (!await
        ExecuteNonQueryAsync($"CREATE TABLE {Tables.WordsParts} (wordid integer, partid integer, UNIQUE(wordid, partid))", connectionFactory)
          .ConfigureAwait(false))
      {
        return false;
      }

      // find the word if that matches the part id
      // the part is not unique
      if (
        !await
          ExecuteNonQueryAsync($"CREATE INDEX index_{Tables.WordsParts}_partid ON {Tables.WordsParts}(partid);", connectionFactory).ConfigureAwait(false))
      {
        return false;
      }

      // find all the parts that matche  the word.
      // the word id is not unique
      if (
        !await
          ExecuteNonQueryAsync($"CREATE INDEX index_{Tables.WordsParts}_wordid ON {Tables.WordsParts}(wordid);", connectionFactory).ConfigureAwait(false))
      {
        return false;
      }
      return true;
    }

    /// <summary>
    /// Create all the word parts table.
    /// each part is unique...
    /// </summary>
    /// <param name="connectionFactory"></param>
    /// <returns></returns>
    private async Task<bool> CreatePartsAsync(IConnectionFactory connectionFactory)
    {
      if (!await
        ExecuteNonQueryAsync($"CREATE TABLE {Tables.Parts} (id integer PRIMARY KEY, part TEXT UNIQUE)", connectionFactory)
          .ConfigureAwait(false))
      {
        return false;
      }
      
      // no need to create an index on text ... it is unique.
      // and id is also unique

      return true;
    }

    /// <summary>
    /// Create all the word parts table.
    /// each part is unique...
    /// </summary>
    /// <param name="connectionFactory"></param>
    /// <returns></returns>
    private async Task<bool> CreatePartsSearchAsync(IConnectionFactory connectionFactory)
    {
      if (!await
        ExecuteNonQueryAsync($"CREATE VIRTUAL TABLE {Tables.PartsSearch} USING fts4( content='Parts', part);", connectionFactory)
          .ConfigureAwait(false))
      {
        return false;
      }

      // no need to create an index on text ... it is unique.
      // and id is also unique

      return true;
    }

    /// <summary>
    /// Create the updates table.
    /// </summary>
    /// <param name="connectionFactory"></param>
    /// <returns></returns>
    private async Task<bool> CreateFilesUpdateAsync(IConnectionFactory connectionFactory)
    {
      if (!await
        ExecuteNonQueryAsync($"CREATE TABLE {Tables.FileUpdates} (fileid integer, type integer, ticks integer)", connectionFactory)
          .ConfigureAwait(false))
      {
        return false;
      }

      // index to get the last 'x' updated files.
      if (
        !await
          ExecuteNonQueryAsync($"CREATE INDEX index_{Tables.FileUpdates}_ticks ON {Tables.FileUpdates}(ticks);", connectionFactory).ConfigureAwait(false))
      {
        return false;
      }

      // the files index so we can add/remove files once processed.
      if (
        !await
          ExecuteNonQueryAsync($"CREATE INDEX index_{Tables.FileUpdates}_fileid ON {Tables.FileUpdates}(fileid);", connectionFactory).ConfigureAwait(false))
      {
        return false;
      }
      return true;
    }

    /// <summary>
    /// Create the folders table
    /// </summary>
    /// <param name="connectionFactory"></param>
    /// <returns></returns>
    private async Task<bool> CreateFoldersAsync(IConnectionFactory connectionFactory)
    {
      if (!await
        ExecuteNonQueryAsync($"CREATE TABLE {Tables.Folders} (id integer PRIMARY KEY, path varchar(260) UNIQUE)", connectionFactory)
          .ConfigureAwait(false))
      {
        return false;
      }

      return true;
    }

    /// <summary>
    /// Create the configuration table
    /// </summary>
    /// <param name="connectionFactory"></param>
    /// <returns></returns>
    private async Task<bool> CreateConfigAsync(IConnectionFactory connectionFactory)
    {
      if (!await
        ExecuteNonQueryAsync($"CREATE TABLE {Tables.Config} (name varchar(20) PRIMARY KEY, value varchar(255))", connectionFactory)
          .ConfigureAwait(false))
      {
        return false;
      }

      // no need for indexes as the name is unique.

      return true;
    }

    /// <summary>
    /// Check if the table exists.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="connectionFactory"></param>
    /// <returns></returns>
    protected bool TableExists(string name, IConnectionFactory connectionFactory)
    {
      var sql = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{name}';";
      using(var command = connectionFactory.CreateCommand(sql))
      using (var reader = connectionFactory.ExecuteReadAsync(command, default(CancellationToken)).GetAwaiter().GetResult())
      {
        try
        {
          while (reader.Read())
          {
            return true;
          }
        }
        finally
        {
          reader.Close();
        }
      }
      return false;
    }

    protected async Task Update(IConnectionFactory connectionFactory, CancellationToken token)
    {
      // if the config table does not exis, then we have to asume it is brand new.
      if (TableExists(Tables.Config, connectionFactory))
      {
        return;
      }
      await CreateDatabase(connectionFactory).ConfigureAwait(false);
    }

    #region Periodic maintenance
    /// <summary>
    /// Fix all the parts words to make sure that the search table is up to date.
    /// @see https://www.sqlite.org/fts3.html#rebuild
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task MaintenanceFtsRebuildAsync(CancellationToken token)
    {
      // get a factory item
      var factory = await BeginWrite(token).ConfigureAwait(false);
      try
      {
        const string configMaintenanceRebuild = "maintenance.rebuild";
        var lastRebuild = await Config
          .GetConfigValueAsync(configMaintenanceRebuild, DateTime.MinValue, factory, token)
          .ConfigureAwait(false);

        // between 22 and 26 hours to prevent running at the same time as others.
        var randomHours = (new Random(DateTime.UtcNow.Millisecond)).Next(22, 26);
        if ((DateTime.UtcNow - lastRebuild).TotalHours > randomHours)
        {
          // https://www.sqlite.org/fts3.html#*fts4rebuidcmd
          _logger.Information("Maintenance rebuilding virtual table");
          await ExecuteNonQueryAsync($"INSERT INTO {Tables.PartsSearch}({Tables.PartsSearch}) VALUES ('rebuild')", factory, token).ConfigureAwait(false);

          // set the update time.
          await Config.SetConfigValueAsync(configMaintenanceRebuild, DateTime.UtcNow, factory, token).ConfigureAwait(false);
        }
        Commit(factory);
      }
      catch
      {
        Rollback(factory);

        // will be logged by the caller
        throw;
      }
    }

    /// <summary>
    /// Fix all the parts words to make sure that the search table is up to date.
    /// @see https://www.sqlite.org/fts3.html#rebuild
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task MaintenanceFtsOptimizeAsync(CancellationToken token)
    {
      // get a factory item
      var factory = await BeginWrite(token).ConfigureAwait(false);
      try
      {
        const string configMaintenanceOptimize = "maintenance.optimize";
        var lastOptimize = await Config
          .GetConfigValueAsync(configMaintenanceOptimize, DateTime.MinValue, factory, token)
          .ConfigureAwait(false);

        // between 5 and 7 hours to prevent running at the same time as others.
        var randomHours = (new Random(DateTime.UtcNow.Millisecond)).Next(5, 7);
        if ((DateTime.UtcNow - lastOptimize).TotalHours > randomHours)
        {
          // https://www.sqlite.org/fts3.html#optimize
          _logger.Information("Maintenance optimizing virtual table");
          await ExecuteNonQueryAsync($"INSERT INTO {Tables.PartsSearch}({Tables.PartsSearch}) VALUES ('optimize')",
            factory, token).ConfigureAwait(false);

          // set the update time.
          await Config.SetConfigValueAsync(configMaintenanceOptimize, DateTime.UtcNow, factory, token)
            .ConfigureAwait(false);
        }
        Commit(factory);
      }
      catch
      {
        Rollback(factory);
        // the caller will log
        throw;
      }
    }

    /// <summary>
    /// Fix all the parts words to make sure that the search table is up to date.
    /// @see https://www.sqlite.org/lang_vacuum.html
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task MaintenanceVacuumAsync(CancellationToken token)
    {
      // get a factory item ... but we do not want a transaction.
      var factory = await BeginWrite(false, Timeout.Infinite, token).ConfigureAwait(false);
      try
      {
        const string configVacuum = "maintenance.vacuum";
        var lastVacuum = await Config.GetConfigValueAsync(configVacuum, DateTime.MinValue, factory, token)
          .ConfigureAwait(false);

        // between 24 and 28 hours to prevent running at the same time as others.
        var randomHours = (new Random(DateTime.UtcNow.Millisecond)).Next(24, 28);
        if ((DateTime.UtcNow - lastVacuum).TotalHours > randomHours)
        {
          // https://www.sqlite.org/lang_vacuum.html
          _logger.Information("Maintenance vacuum database");
          await ExecuteNonQueryAsync("VACUUM;",factory, token).ConfigureAwait(false);

          // set the update time.
          await Config.SetConfigValueAsync(configVacuum, DateTime.UtcNow, factory, token).ConfigureAwait(false);
        }
        Commit(factory);
      }
      catch
      {
        Rollback(factory);
        // the caller will log
        throw;
      }
    }

    /// <inheritdoc />
    public async Task MaintenanceAsync(CancellationToken token)
    {
      try
      {
        // vacuum
        await MaintenanceVacuumAsync(token).ConfigureAwait(false);

        // fts rebuild
        await MaintenanceFtsRebuildAsync(token).ConfigureAwait(false);

        // fts optimize
        await MaintenanceFtsOptimizeAsync(token).ConfigureAwait(false);
      }
      catch (OperationCanceledException e)
      {
        _logger.Warning("Received cancellation request durring Persister Maintenance.");
        // is it my token?
        if (e.CancellationToken != token)
        {
          _logger.Exception(e);
        }
        throw;
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        throw;
      }
    }
    #endregion
  }
}