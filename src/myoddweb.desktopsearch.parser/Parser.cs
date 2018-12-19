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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.helper;
using myoddweb.desktopsearch.interfaces.Enums;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Persisters;
using myoddweb.desktopsearch.parser.IO;
using ILogger = myoddweb.desktopsearch.interfaces.Logging.ILogger;

namespace myoddweb.desktopsearch.parser
{
  public class Parser
  {
    #region Member variables
    /// <summary>
    /// The currently running task
    /// </summary>
    private Task _runningTask;

    /// <summary>
    /// The file watchers
    /// </summary>
    private readonly List<Watcher> _watchers = new List<Watcher>();

    /// <summary>
    /// The logger that we will be using to log messages.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The directory parser we will be using.
    /// </summary>
    private readonly IDirectory _directory;

    /// <summary>
    /// The persister.
    /// </summary>
    private readonly IPersister _persister;

    /// <summary>
    /// The system configuration
    /// </summary>
    private readonly interfaces.Configs.IConfig _config;
    #endregion

    public Parser(interfaces.Configs.IConfig config, IPersister persister, ILogger logger, IDirectory directory )
    {
      // set the config values.
      _config = config ?? throw new ArgumentNullException(nameof(config));

      // set the persister.
      _persister = persister ?? throw new ArgumentNullException(nameof(persister));

      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      // save the directory parser
      _directory = directory ?? throw new ArgumentNullException(nameof(directory));
    }

    /// <summary>
    /// Do all the parsing work,
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> WorkAsync(CancellationToken token)
    {
      // get all the paths we will be working with.
      var paths = helper.IO.Paths.GetStartPaths( _config.Paths );

      // first we get a full list of files/directories.
      if (!await ParseAllDirectoriesAsync(paths, token).ConfigureAwait(false))
      {
        return false;
      }

      // we then watch for files/folder changes.
      if (!StartWatchers(paths, token))
      {
        return false;
      }
      return true;
    }

    /// <summary>
    /// Parse all the directories.
    /// </summary>
    /// <param name="paths"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> ParseAllDirectoriesAsync( IEnumerable<DirectoryInfo> paths, CancellationToken token)
    {
      try
      {
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        // all the tasks
        var tasks = new List<Task<long>>();

        foreach (var path in paths)
        {
          // get out if needed.
          token.ThrowIfCancellationRequested();

          // parse that one directory
          tasks.Add( ParseDirectoriesAsync( path, token ) );
        }

        // wait for everything to complete
        var totalDirectories = await Wait.WhenAll( tasks, _logger, token ).ConfigureAwait(false);

        // only now, that everything is done
        // we can set the last time we checked flag
        await SetLastUpdatedTimeAsync( token ).ConfigureAwait( false );

        // stop the watch and log how many items we found.
        stopwatch.Stop();
        _logger.Information($"Parsed {totalDirectories.Sum()} directories (Time Elapsed: {stopwatch.Elapsed:g})");
        return true;
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Parser all path");
        throw;
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        return false;
      }
    }

    /// <summary>
    /// Parse this path and sub directories.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<long> ParseDirectoriesAsync(DirectoryInfo path, CancellationToken token)
    {
      // get all the directories.
      var directories = await _directory.ParseDirectoriesAsync(path, token).ConfigureAwait(false);
      if (directories == null)
      {
        return 0;
      }

      // process all the files.
      if (!await PersistDirectoriesAsync(path, directories, token).ConfigureAwait(false))
      {
        return 0;
      }

      // get the number of directories parsed.
      return directories.Count;
    }


    /// <summary>
    /// Get the last access time from the config.
    /// </summary>
    /// <param name="token"></param>
    private async Task<DateTime> GetLastUpdatedTimeAsync(CancellationToken token)
    {
      var transaction = await _persister.BeginWrite(token).ConfigureAwait(false);
      if (null == transaction)
      {
        //  we probably cancelled.
        throw new Exception("Unable to get transaction!");
      }

      try
      {
        // finally we can set the last time we checked the entire data tree.
        var lastAccessTimeUtc = await _persister.Config.GetConfigValueAsync("LastAccessTimeUtc", DateTime.MinValue, transaction, token).ConfigureAwait(false);

        // we can commit our code.
        _persister.Commit(transaction);

        return lastAccessTimeUtc;
      }
      catch (Exception)
      {
        _persister.Rollback(transaction);
        throw;
      }
    }

    /// <summary>
    /// Update the config to set the last access time to now.
    /// </summary>
    /// <param name="token"></param>
    private async Task SetLastUpdatedTimeAsync(CancellationToken token)
    {
      var transaction = await _persister.BeginWrite(token).ConfigureAwait(false);
      if (null == transaction)
      {
        //  we probably cancelled.
        throw new Exception("Unable to get transaction!");
      }

      try
      {
        // finally we can set the last time we checked the entire data tree.
        await _persister.Config.SetConfigValueAsync("LastAccessTimeUtc", DateTime.UtcNow, transaction, token).ConfigureAwait(false);

        // we can commit our code.
        _persister.Commit(transaction);
      }
      catch (Exception)
      {
        _persister.Rollback(transaction);
        throw;
      }
    }

    /// <summary>
    /// Check if we want to parse this directory or not.
    /// </summary>
    /// <param name="parent"></param>
    /// <param name="directories"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> PersistDirectoriesAsync(FileSystemInfo parent, IEnumerable<DirectoryInfo> directories, CancellationToken token)
    {
      // start the stopwatch
      var stopwatch = new Stopwatch();
      stopwatch.Start();

      // get the last time we did this
      var lastAccessTimeUtc = await GetLastUpdatedTimeAsync(token).ConfigureAwait(false);

      // get all the changed or updated files.
      var transaction = await _persister.BeginWrite(token).ConfigureAwait(false);
      if (null == transaction)
      {
        throw new Exception("Unable to get transaction!");
      }

      // get the list of directories that have changed.
      var changedDirectories = new List<DirectoryInfo>();

      // all the newly created directories
      var createdDirectories = new List<DirectoryInfo>();

      foreach (var directory in directories)
      {
        try
        {
          // try and persist this one directory.
          switch (await GetDirectoryUpdateType(directory, lastAccessTimeUtc, token).ConfigureAwait(false))
          {
            case UpdateType.None:
              //  ignore
              break;

            case UpdateType.Created:
              createdDirectories.Add(directory);
              break;

            case UpdateType.Changed:
              changedDirectories.Add(directory);
              break;

            //
            case UpdateType.Deleted:
              throw new ArgumentException( $"How can a newly parsed directory, {directory.FullName} be deleted??");

            default:
              throw new ArgumentOutOfRangeException();
          }
        }
        catch (Exception)
        {
          _persister.Rollback(transaction);
          throw;
        }
      }

      // commit the transaction one last time
      // if we have not done it already.
      _persister.Commit(transaction);

      // update the changed directorues
      await PersistChangedDirectories(changedDirectories, token).ConfigureAwait(false);

      // update the changed directorues
      await PersistCreatedDirectories(createdDirectories, token).ConfigureAwait(false);

      // log that we are done.
      var sb = new StringBuilder();
      sb.AppendLine($"Finishing directory parsing: {parent.FullName}");
      sb.AppendLine($"  Created: {createdDirectories.Count} directories");
      sb.Append($"  Changed: {changedDirectories.Count} directories");
      _logger.Verbose(sb.ToString());

      // stop the watch and log how many items we found.
      stopwatch.Stop();
      _logger.Information($"Completed directory parsing: {parent.FullName} (Time Elapsed: {stopwatch.Elapsed:g})");

      // all done
      return true;
    }

    /// <summary>
    /// Get the type of update that we need to do for that directory.
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="lastAccessTimeUtc"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<UpdateType> GetDirectoryUpdateType(DirectoryInfo directory, DateTime lastAccessTimeUtc, CancellationToken token)
    {
      // if the directory exist... and it was flaged as newer than the last time
      // we checked the directory, then all we need to do is touch it.
      if (await _persister.Folders.DirectoryExistsAsync(directory, token).ConfigureAwait(false))
      {
        if (directory.LastAccessTimeUtc < lastAccessTimeUtc)
        {
          // the file has not changed since the last time
          return UpdateType.None;
        }
        return UpdateType.Changed;
      }

      // this is a brand new file.
      return UpdateType.Created;
    }

    /// <summary>
    /// Process all the changed directories.
    /// </summary>
    /// <param name="directories"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task PersistChangedDirectories(IReadOnlyCollection<DirectoryInfo> directories, CancellationToken token)
    {
      // do we have anything to do??
      if (!directories.Any())
      {
        return;
      }

      // get all the changed or updated files.
      var transaction = await _persister.BeginWrite(token).ConfigureAwait(false);
      if (null == transaction)
      {
        throw new Exception("Unable to get transaction!");
      }

      try
      {
        // the file has changed.
        await _persister.Folders.FolderUpdates.TouchDirectoriesAsync(directories, UpdateType.Changed, token).ConfigureAwait(false);

        // all done
        _persister.Commit(transaction);
      }
      catch (OperationCanceledException)
      {
        _persister.Rollback(transaction);
        _logger.Warning("Received cancellation request - parsing changed directories parsing");
        throw;
      }
      catch (Exception e)
      {
        _persister.Rollback(transaction);
        _logger.Exception(e);
        throw;
      }
    }

    /// <summary>
    /// Persist all the directories and files.
    /// </summary>
    /// <param name="directories"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task PersistCreatedDirectories(IReadOnlyCollection<DirectoryInfo> directories, CancellationToken token)
    {
      // do we have anything to do??
      if (!directories.Any())
      {
        return;
      }

      // look for all the files in that directory.
      // so they can also be added.
      var tasks = directories.Select(directory => _directory.ParseDirectoryAsync(directory, token).ContinueWith(
        async (task) =>
        {
          if (task.IsFaulted)
          {
            if (null != task.Exception)
            {
              throw task.Exception;
            }
            return;
          }

          var files = task.Result;
          if (!task.Result.Any())
          {
            return;
          }
          // get all the changed or updated files.
          var transaction = await _persister.BeginWrite(token).ConfigureAwait(false);
          try
          {
            // do all the directories at once.
            await _persister.Folders.AddOrUpdateDirectoryAsync(directory, token).ConfigureAwait(false);

            // then do the files.
            await _persister.Folders.Files.AddOrUpdateFilesAsync(files, token).ConfigureAwait(false);

            // all the folders have been processed.
            await _persister.Folders.FolderUpdates.MarkDirectoriesProcessedAsync( new []{directory}, token).ConfigureAwait(false);

            // all done
            _persister.Commit(transaction);
          }
          catch (OperationCanceledException)
          {
            _persister.Rollback(transaction);
            _logger.Warning("Received cancellation request - parsing created directories parsing");
            throw;
          }
          catch (Exception e)
          {
            _persister.Rollback(transaction);
            _logger.Exception(e);
            throw;
          }
        }, token)).ToArray();

      // and then wait for everything to complete
      await Wait.WhenAll(tasks, _logger, token).ConfigureAwait( false );
    }

    #region Start Parsing
    /// <summary>
    /// Start parsing.
    /// </summary>
    public void Start(CancellationToken token)
    {
      if (_runningTask != null && !_runningTask.IsCompleted)
      {
        Wait.WaitAll(_runningTask, _logger, token );
        _runningTask = null;
      }
      _runningTask = WorkAsync(token);
      _logger.Information("Parser started");
    }

    /// <summary>
    /// Start to watch all the folders and sub folders.
    /// </summary>
    /// <param name="paths"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private bool StartWatchers(IEnumerable<DirectoryInfo> paths, CancellationToken token)
    {
      // get the ignore path
      foreach (var path in paths)
      {
        // the file watcher.
        var fileWatcher = new Watcher(path, _logger, 
          new FilesSystemEventsParser(_persister, _directory, _config.Timers.EventsParserMs, _logger),
          new DirectoriesSystemEventsParser(_persister, _directory, _config.Timers.EventsParserMs, _logger)
        );

        fileWatcher.Start(token);

        _watchers.Add(fileWatcher);
      }
      return true;
    }
    #endregion

    #region Stop Parsing
    /// <summary>
    /// Stop parsing
    /// </summary>
    public void Stop()
    {
      // Stop the file watcher
      foreach (var watcher in _watchers)
      {
        watcher?.Stop();
      }
      _watchers.Clear();

      // wait for the main task itself to complete.
      if (_runningTask != null && !_runningTask.IsCompleted)
      {
        Wait.WaitAll(_runningTask, _logger);
        _runningTask = null;
      }
    }
    #endregion
  }
}
