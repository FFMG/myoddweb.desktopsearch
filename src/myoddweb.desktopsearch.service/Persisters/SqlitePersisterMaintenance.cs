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
        ExecuteNonQueryAsync($"CREATE TABLE {TableWords} (id integer PRIMARY KEY, word TEXT UNIQUE)", connectionFactory)
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
        ExecuteNonQueryAsync($"CREATE TABLE {TableFilesWords} (wordid integer, fileid integer)", connectionFactory)
          .ConfigureAwait(false))
      {
        return false;
      }

      // there can only be one word and one id.
      if (
        !await
          ExecuteNonQueryAsync($"CREATE UNIQUE INDEX index_{TableFilesWords}_wordid_fileid ON {TableFilesWords}(wordid, fileid);", connectionFactory).ConfigureAwait(false))
      {
        return false;
      }

      if (
        !await
          ExecuteNonQueryAsync($"CREATE INDEX index_{TableFilesWords}_fileid ON {TableFilesWords}(fileid);", connectionFactory).ConfigureAwait(false))
      {
        return false;
      }
      return true;
    }

    private async Task<bool> CreateFilesAsync(IConnectionFactory connectionFactory)
    {
      if (!await
        ExecuteNonQueryAsync($"CREATE TABLE {TableFiles} (id integer PRIMARY KEY, folderid integer, name varchar(260))", connectionFactory)
          .ConfigureAwait(false))
      {
        return false;
      }

      if (
        !await
          ExecuteNonQueryAsync($"CREATE INDEX index_{TableFiles}_folderid_name ON {TableFiles}(folderid, name);", connectionFactory).ConfigureAwait(false))
      {
        return false;
      }

      if (
        !await
          ExecuteNonQueryAsync($"CREATE INDEX index_{TableFiles}_folderid ON {TableFiles}(folderid);", connectionFactory).ConfigureAwait(false))
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
        ExecuteNonQueryAsync($"CREATE TABLE {TableFolderUpdates} (folderid integer, type integer, ticks integer)", connectionFactory)
          .ConfigureAwait(false))
      {
        return false;
      }

      // index to get the last 'x' updated folders.
      if (
        !await
          ExecuteNonQueryAsync($"CREATE INDEX index_{TableFolderUpdates}_ticks ON {TableFolderUpdates}(ticks);", connectionFactory).ConfigureAwait(false))
      {
        return false;
      }

      // the folderid index so we can add/remove folders once processed.
      if (
        !await
          ExecuteNonQueryAsync($"CREATE INDEX index_{TableFolderUpdates}_folderid ON {TableFolderUpdates}(folderid);", connectionFactory).ConfigureAwait(false))
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
        ExecuteNonQueryAsync($"CREATE TABLE {TableCounts} (type integer, count integer)", connectionFactory)
          .ConfigureAwait(false))
      {
        return false;
      }

      if (
        !await
          ExecuteNonQueryAsync($"CREATE UNIQUE INDEX index_{TableCounts}_type ON {TableCounts}(type);", connectionFactory).ConfigureAwait(false))
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
        ExecuteNonQueryAsync($"CREATE TABLE {TableWordsParts} (wordid integer, partid integer)", connectionFactory)
          .ConfigureAwait(false))
      {
        return false;
      }

      // find the word if that matches the part id
      // the part is not unique
      if (
        !await
          ExecuteNonQueryAsync($"CREATE INDEX index_{TableWordsParts}_partid ON {TableWordsParts}(partid);", connectionFactory).ConfigureAwait(false))
      {
        return false;
      }

      // find all the parts that matche  the word.
      // the word id is not unique
      if (
        !await
          ExecuteNonQueryAsync($"CREATE INDEX index_{TableWordsParts}_wordid ON {TableWordsParts}(wordid);", connectionFactory).ConfigureAwait(false))
      {
        return false;
      }

      // we need to be able to remove word + parts together, and they have to be unique.
      if (
        !await
          ExecuteNonQueryAsync($"CREATE UNIQUE INDEX index_{TableWordsParts}_wordid_partid ON {TableWordsParts}(wordid, partid);", connectionFactory).ConfigureAwait(false))
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
        ExecuteNonQueryAsync($"CREATE TABLE {TableParts} (id integer PRIMARY KEY, part TEXT UNIQUE)", connectionFactory)
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
        ExecuteNonQueryAsync($"CREATE TABLE {TableFileUpdates} (fileid integer, type integer, ticks integer)", connectionFactory)
          .ConfigureAwait(false))
      {
        return false;
      }

      // index to get the last 'x' updated files.
      if (
        !await
          ExecuteNonQueryAsync($"CREATE INDEX index_{TableFileUpdates}_ticks ON {TableFileUpdates}(ticks);", connectionFactory).ConfigureAwait(false))
      {
        return false;
      }

      // the files index so we can add/remove files once processed.
      if (
        !await
          ExecuteNonQueryAsync($"CREATE INDEX index_{TableFileUpdates}_fileid ON {TableFileUpdates}(fileid);", connectionFactory).ConfigureAwait(false))
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
        ExecuteNonQueryAsync($"CREATE TABLE {TableFolders} (id integer PRIMARY KEY, path varchar(260))", connectionFactory)
          .ConfigureAwait(false))
      {
        return false;
      }

      if ( 
        !await
          ExecuteNonQueryAsync($"CREATE INDEX index_{TableFolders}_path ON {TableFolders}(path);", connectionFactory).ConfigureAwait(false))
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
        ExecuteNonQueryAsync($"CREATE TABLE {TableConfig} (name varchar(20) PRIMARY KEY, value varchar(255))", connectionFactory)
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
      using (var command = connectionFactory.CreateCommand(sql))
      {
        var reader = connectionFactory.ExecuteReadAsync( command, default(CancellationToken)).Result;
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
      if (TableExists(TableConfig, connectionFactory))
      {
        return;
      }
      await CreateDatabase(connectionFactory).ConfigureAwait(false);
    }
  }
}