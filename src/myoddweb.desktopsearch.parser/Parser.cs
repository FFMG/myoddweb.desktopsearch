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
  public class Parser : IParser
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

    private object _taskLock = new object();
    #endregion

    public Parser(interfaces.Configs.IConfig config, IPersister persister, ILogger logger, IDirectory directory)
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

    /// <inheritdoc />
    public async Task MaintenanceAsync(CancellationToken token)
    {
      lock (_taskLock)
      {
        // are we still running the start?
        if (!_runningTask?.IsCompleted ?? true)
        {
          return;
        }
      }

      var factory = await _persister.BeginWrite(token).ConfigureAwait(false);
      try
      {
        const string configParse = "maintenance.parse";
        var lastParse = await _persister.Config.GetConfigValueAsync(configParse, DateTime.MinValue, factory, token).ConfigureAwait(false);

        // between 3 and 5 hours to prevent running at the same time as others.
        var randomHours = (new Random(DateTime.UtcNow.Millisecond)).Next(3, 5);
        if ((DateTime.UtcNow - lastParse).TotalHours > randomHours)
        {
          //
          //  Do maintenance work
          //
          _logger.Information("Maintenance files/folders parser");

          // save the date and commit ... because the parser needs its own factory.
          await _persister.Config.SetConfigValueAsync(configParse, DateTime.UtcNow, factory, token).ConfigureAwait(false);
        }
        _persister.Commit(factory);
      }
      catch (Exception e)
      {
        _persister.Rollback(factory);
        _logger.Exception("There was an exception re-parsing the directories.", e);
        throw;
      }
    }

    /// <inheritdoc />
    public Task WorkAsync(CancellationToken token)
    {
      lock (_taskLock)
      {
        // are we still running the start?
        if (!_runningTask?.IsCompleted ?? true)
        {
          return Task.CompletedTask;
        }
      }

      try
      {
        lock (_taskLock)
        {
          // we can re-parse everthing
          _runningTask = ParseAllDirectoriesAsync(token);
        }
        return _runningTask;
      }
      catch (OperationCanceledException )
      {
        _logger.Warning("Files/Folder parser processor: Received cancellation request - Work");
        throw;
      }
      catch (Exception e)
      {
        _logger.Exception(  "There was an exception re-parsing the directories.", e );
        throw;
      }
    }

    /// <summary>
    /// Parse all the directories.
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> ParseAllDirectoriesAsync(CancellationToken token)
    {
      try
      {
        // get all the paths we will be working with.
        var paths = helper.IO.Paths.GetStartPaths(_config.Paths);

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        // get the last time we checked access time.
        var lastAccessTimeUtc = await GetLastUpdatedTimeAsync(token).ConfigureAwait(false);

        // all the tasks
        var tasks = new List<Task<long>>();

        foreach (var path in paths)
        {
          // get out if needed.
          token.ThrowIfCancellationRequested();

          // parse that one directory
          tasks.Add(ParseDirectoriesAsync(path, lastAccessTimeUtc, token));
        }

        // wait for everything to complete
        var totalDirectories = await Wait.WhenAll(tasks, _logger, token).ConfigureAwait(false);

        // only now, that everything is done
        // we can set the last time we checked flag
        await SetLastUpdatedTimeAsync(token).ConfigureAwait(false);

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
    /// <param name="lastAccessTimeUtc">The last time we checked the access time.</param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<long> ParseDirectoriesAsync(DirectoryInfo path, DateTime lastAccessTimeUtc, CancellationToken token)
    {
      // get all the directories.
      var directories = await _directory.ParseDirectoriesAsync(path, token).ConfigureAwait(false);
      if (directories == null)
      {
        return 0;
      }

      // process all the files.
      if (!await PersistDirectoriesAsync(path, directories,lastAccessTimeUtc, token).ConfigureAwait(false))
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
      var transaction = await _persister.BeginRead(token).ConfigureAwait(false);
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
    /// <param name="lastAccessTimeUtc">The last time we checked the accessed time.</param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> PersistDirectoriesAsync(
      FileSystemInfo parent, 
      IEnumerable<DirectoryInfo> directories, 
      DateTime lastAccessTimeUtc,
      CancellationToken token
    )
    {
      // start the stopwatch
      var stopwatch = new Stopwatch();
      stopwatch.Start();

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
              throw new ArgumentException($"How can a newly parsed directory, {directory.FullName} be deleted??");

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

      // process the directories.
      await ProcessCreatedDirectories(createdDirectories, token).ConfigureAwait(false);

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

    public static IEnumerable<IList<T>> SplitDirectories<T>(List<T> locations, int nSize)
    {
      for (var i = 0; i < locations.Count; i += nSize)
      {
        yield return locations.GetRange(i, Math.Min(nSize, locations.Count - i));
      }
    }

    /// <summary>
    /// Persist all the directories and files.
    /// </summary>
    /// <param name="directories"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task ProcessCreatedDirectories(List<DirectoryInfo> directories, CancellationToken token)
    {
      // do we have anything to do??
      if (!directories.Any())
      {
        return;
      }

      var listSize = (int)(Math.Ceiling(directories.Count * 0.05)) + 1; // 5% ... make sure at least one
      var listOfDirectories = SplitDirectories(directories, listSize);

      // parse all the directories at once.
      var tasks = listOfDirectories.Select(dirs => PersistCreatedDirectories(dirs, token)).ToArray();

      // and then wait for everything to complete
      await Wait.WhenAll(tasks, _logger, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Parse a directory and save all the values for that directory to the database.
    /// </summary>
    /// <param name="directories"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task PersistCreatedDirectories(IEnumerable<DirectoryInfo> directories, CancellationToken token)
    {
      // get all the changed or updated files.
      var transaction = await _persister.BeginWrite(token).ConfigureAwait(false);
      try
      {
        foreach (var directory in directories)
        {
          token.ThrowIfCancellationRequested();

          //  get the files in that directory.
          var files = await _directory.ParseDirectoryAsync(directory, token).ConfigureAwait(false);
          if (files == null || !files.Any())
          {
            continue;
          }

          // do all the directories at once.
          await _persister.Folders.AddOrUpdateDirectoryAsync(directory, token).ConfigureAwait(false);

          // then do the files.
          await _persister.Folders.Files.AddOrUpdateFilesAsync(files, token).ConfigureAwait(false);

          // all the folders have been processed.
          await _persister.Folders.FolderUpdates.MarkDirectoriesProcessedAsync(new[] { directory }, token).ConfigureAwait(false);
        }

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
    }

    #region Start Parsing
    /// <inheritdoc />
    public void Start(CancellationToken token)
    {
      lock (_taskLock)
      {
        if (_runningTask != null && !_runningTask.IsCompleted)
        {
          Wait.WaitAll(_runningTask, _logger, token);
          _runningTask = null;
        }

        _runningTask = ParseAllDirectoriesAsync(token).ContinueWith(
          (t) =>
          {
            if (!t.Result)
            {
              _logger.Warning("All directory parsing returned an error, watchers not starting.");
              return;
            }

            if (t.IsFaulted)
            {
              _logger.Exception("All directory parsing returned a faulted result, watchers not starting.", t.Exception);
              return;
            }

            if (t.IsCanceled)
            {
              _logger.Warning("All directory parsing task was cancelled, watchers not starting.");
              return;
            }

            try
            {
              // we then watch for files/folder changes.
              StartWatchers(token);
            }
            catch (Exception e)
            {
              _logger.Exception(e);
            }
          }, token);
      }

      _logger.Information("Parser started");
    }

    /// <summary>
    /// Start to watch all the folders and sub folders.
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    private void StartWatchers(CancellationToken token)
    {
      // get all the paths we will be working with.
      var paths = helper.IO.Paths.GetStartPaths(_config.Paths);

      _logger.Verbose( $"Starting the watchers in {paths.Count} path(s).");

      // get the ignore path
      foreach (var path in paths)
      {
        _logger.Verbose($"Creating the watcher in '{path.FullName}'.");

        try
        {
          var fsep = new FilesSystemEventsParser(_persister, _directory, _config.Timers.EventsParserMs, _config.Timers.EventsMaxWaitTransactionMs, _logger);
          var dsep = new DirectoriesSystemEventsParser(_persister, _directory, _config.Timers.EventsParserMs, _config.Timers.EventsMaxWaitTransactionMs, _logger);

          // the file watcher.
          var fileWatcher = new Watcher(path, _logger, fsep, dsep);
          fileWatcher.Start(token);

          _watchers.Add(fileWatcher);
          _logger.Verbose($"Started the watcher in '{path.FullName}'.");
        }
        catch (Exception e)
        {
          _logger.Exception( e );
        }
      }

      _logger.Verbose($"Started {_watchers.Count} watchers.");
    }
    #endregion

    #region Stop Parsing
    /// <inheritdoc />
    public void Stop()
    {
      // Stop the file watcher
      foreach (var watcher in _watchers)
      {
        watcher?.Stop();
      }
      _watchers.Clear();

      lock (_taskLock)
      {
        // wait for the main task itself to complete.
        if (_runningTask == null || _runningTask.IsCompleted)
        {
          return;
        }

        Wait.WaitAll(_runningTask, _logger);
        _runningTask = null;
      }
    }
    #endregion
  }
}
