using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal partial class Persister 
  {
    public async Task<bool> AddOrUpdateFolderAsync(DirectoryInfo directory )
    {
      // only valid paths are added.
      if (!directory.Exists)
      {
        return false;
      }
      // we first look for it, and, if we find it then there is nothing to do.
      var id = await FindFolderAsycn(directory).ConfigureAwait(false);

      // does it exist already?
      if (-1 != id)
      {
        return true;
      }

      var sql = $"insert into {TableFolders} (path) values (@path)";
      using (var cmd = CreateCommand(sql))
      {
        cmd.Parameters.Add("@path", DbType.String );
        cmd.Parameters["@path"].Value = directory.FullName;
        try
        {
          var rowsAffected = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
          return rowsAffected > 0;
        }
        catch (Exception ex)
        {
          _logger.Exception(ex);
          return false;
        }
      }
    }

    /// <summary>
    /// Look for a folder id given the directory path.
    /// </summary>
    /// <param name="directory"></param>
    /// <returns></returns>
    private async Task<long> FindFolderAsycn(FileSystemInfo directory)
    {
      var sql = $"SELECT rowid FROM {TableFolders} WHERE path=@path";
      using (var cmd = CreateCommand(sql))
      {
        cmd.Parameters.Add("@path", DbType.String);
        cmd.Parameters["@path"].Value = directory.FullName;

        var reader = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
        return (long?) reader ?? -1;
      }
    }

    public Task<bool> DeleteFolderAsync(int folderId)
    {
      throw new NotImplementedException();
    }

    public Task<bool> DeleteFolderAsync(string path)
    {
      throw new NotImplementedException();
    }
  }
}
