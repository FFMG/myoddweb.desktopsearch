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

using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal class NoopDbTransaction : DbTransaction
  {
    public override void Commit()
    {
      throw new System.NotImplementedException();
    }

    public override void Rollback()
    {
      throw new System.NotImplementedException();
    }

    protected override DbConnection DbConnection { get; }
    public override IsolationLevel IsolationLevel { get; }
  }

  internal class NoopPersister : IPersister
  {
    public Task<string> GetConfigValueAsync(string name, string defaultValue, IDbTransaction transaction)
    {
      return Task.FromResult(defaultValue);
    }

    public Task<bool> SetConfigValueAsync(string name, string value, IDbTransaction transaction)
    {
      return Task.FromResult(true);
    }

    public Task<long> RenameOrAddDirectoryAsync(DirectoryInfo directory, DirectoryInfo oldDirectory, IDbTransaction transaction,
      CancellationToken token)
    {
      return Task.FromResult<long>(-1);
    }

    public Task<bool> AddOrUpdateDirectoryAsync(DirectoryInfo directory, IDbTransaction transaction, CancellationToken token)
    {
      return Task.FromResult(true);
    }

    public Task<bool> AddOrUpdateDirectoriesAsync(IReadOnlyList<DirectoryInfo> directories, IDbTransaction transaction, CancellationToken token)
    {
      return Task.FromResult(true);
    }

    public Task<bool> DeleteDirectoryAsync(DirectoryInfo directory, IDbTransaction transaction, CancellationToken token)
    {
      return Task.FromResult(true);
    }

    public Task<bool> DeleteDirectoriesAsync(IReadOnlyList<DirectoryInfo> directories, IDbTransaction transaction, CancellationToken token)
    {
      return Task.FromResult(true);
    }

    public Task<bool> DirectoryExistsAsync(DirectoryInfo directory, IDbTransaction transaction, CancellationToken token)
    {
      return Task.FromResult(true);
    }

    public Task<DirectoryInfo> GetDirectoryAsync(long directoryId, IDbTransaction transaction, CancellationToken token)
    {
      return Task.FromResult<DirectoryInfo>(null);
    }

    public Task<bool> TouchDirectoryAsync(DirectoryInfo directory, UpdateType type, IDbTransaction transaction, CancellationToken token)
    {
      return Task.FromResult(true);
    }

    public Task<bool> TouchDirectoryAsync(long folderId, UpdateType type, IDbTransaction transaction, CancellationToken token)
    {
      return Task.FromResult(true);
    }

    public Task<bool> MarkDirectoryProcessedAsync(DirectoryInfo directory, IDbTransaction transaction, CancellationToken token)
    {
      return Task.FromResult(true);
    }

    public Task<bool> MarkDirectoryProcessedAsync(long folderId, IDbTransaction transaction, CancellationToken token)
    {
      return Task.FromResult(true);
    }

    public Task<bool> MarkDirectoriesProcessedAsync(IEnumerable<long> folderIds, IDbTransaction transaction, CancellationToken token)
    {
      return Task.FromResult(true);
    }

    public Task<List<PendingFolderUpdate>> GetPendingFolderUpdatesAsync(long limit, IDbTransaction transaction, CancellationToken token)
    {
      return Task.FromResult(new List<PendingFolderUpdate>());
    }

    public Task<bool> AddOrUpdateFileAsync(FileInfo file, IDbTransaction transaction, CancellationToken token)
    {
      return Task.FromResult(true);
    }

    public Task<bool> AddOrUpdateFilesAsync(IReadOnlyList<FileInfo> files, IDbTransaction transaction, CancellationToken token)
    {
      return Task.FromResult(true);
    }

    public Task<long> RenameOrAddFileAsync(FileInfo file, FileInfo oldFile, IDbTransaction transaction, CancellationToken token)
    {
      return Task.FromResult<long>(-1);
    }

    public Task<bool> DeleteFileAsync(FileInfo file, IDbTransaction transaction, CancellationToken token)
    {
      return Task.FromResult(true);
    }

    public Task<bool> DeleteFilesAsync(IReadOnlyList<FileInfo> files, IDbTransaction transaction, CancellationToken token)
    {
      return Task.FromResult(true);
    }

    public Task<bool> DeleteFilesAsync(DirectoryInfo directory, IDbTransaction transaction, CancellationToken token)
    {
      return Task.FromResult(true);
    }

    public Task<bool> FileExistsAsync(FileInfo file, IDbTransaction transaction, CancellationToken token)
    {
      return Task.FromResult(true);
    }

    public Task<List<FileInfo>> GetFilesAsync(long directoryId, IDbTransaction transaction, CancellationToken token)
    {
      return Task.FromResult( new List<FileInfo>());
    }

    public Task<FileInfo> GetFileAsync(long fileId, IDbTransaction transaction, CancellationToken token)
    {
      return Task.FromResult<FileInfo>(null);
    }

    public Task<bool> TouchFileAsync(FileInfo file, UpdateType type, IDbTransaction transaction, CancellationToken token)
    {
      return Task.FromResult(true);
    }

    public Task<bool> TouchFileAsync(long fileId, UpdateType type, IDbTransaction transaction, CancellationToken token)
    {
      return Task.FromResult(true);
    }

    public Task<bool> TouchFilesAsync(IEnumerable<long> fileIds, UpdateType type, IDbTransaction transaction, CancellationToken token)
    {
      return Task.FromResult(true);
    }

    public Task<bool> MarkFileProcessedAsync(FileInfo file, IDbTransaction transaction, CancellationToken token)
    {
      return Task.FromResult(true);
    }

    public Task<bool> MarkFileProcessedAsync(long fileId, IDbTransaction transaction, CancellationToken token)
    {
      return Task.FromResult(true);
    }

    public Task<bool> MarkFilesProcessedAsync(IEnumerable<long> fileIds, IDbTransaction transaction, CancellationToken token)
    {
      return Task.FromResult(true);
    }

    public Task<List<PendingFileUpdate>> GetPendingFileUpdatesAsync(long limit, IDbTransaction transaction, CancellationToken token)
    {
      return Task.FromResult(new List<PendingFileUpdate>());
    }

    public DbCommand CreateDbCommand(string sql, IDbTransaction transaction)
    {
      throw new System.NotImplementedException();
    }

    public Task<IDbTransaction> BeginTransactionAsync()
    {
      return Task.FromResult<IDbTransaction>(new NoopDbTransaction());
    }

    public bool Rollback(IDbTransaction transaction)
    {
      // when are we rolling back this noop event?
      throw new System.NotImplementedException();
    }

    public bool Commit(IDbTransaction transaction)
    {
      return true;
    }
  }
}
