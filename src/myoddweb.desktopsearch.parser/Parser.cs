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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        var totalDirectories = 0;
        foreach (var path in paths)
        {
          // get out if needed.
          token.ThrowIfCancellationRequested();

          // get all the directories.
          var directories = await _directory.ParseDirectoriesAsync(path, token).ConfigureAwait(false);
          if (directories == null)
          {
            continue;
          }

          // process all the files.
          if (!await PersistDirectories(path, directories, token).ConfigureAwait(false))
          {
            stopwatch.Stop();
            _logger.Warning($"Parsing was cancelled (Time Elapsed: {stopwatch.Elapsed:g})");
            return false;
          }

          // get the number of directories parsed.
          totalDirectories += directories.Count;
        }

        // only now, that everything is done
        // we can set the last time we checked flag
        await SetLastUpdatedTimeAsync( token ).ConfigureAwait( false );

        // stop the watch and log how many items we found.
        stopwatch.Stop();
        _logger.Information($"Parsed {totalDirectories} directories (Time Elapsed: {stopwatch.Elapsed:g})");
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
    /// Update the config to set the last access time to now.
    /// </summary>
    /// <param name="token"></param>
    private async Task SetLastUpdatedTimeAsync(CancellationToken token)
    {
      var transaction = await _persister.Begin(token).ConfigureAwait(false);
      if (null == transaction)
      {
        //  we probably cancelled.
        throw new Exception("Unable to get transaction!");
      }

      try
      {
        // finally we can set the last time we checked the entire data tree.
        await _persister.SetConfigValueAsync("LastAccessTimeUtc", DateTime.UtcNow, transaction, token).ConfigureAwait(false);

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
    private async Task<bool> PersistDirectories(DirectoryInfo parent, IEnumerable<DirectoryInfo> directories, CancellationToken token)
    {
      _logger.Verbose($"Finishing directory parsing: {parent.FullName}");

      var transaction = await _persister.Begin(token).ConfigureAwait(false);
      if (null == transaction)
      {
        //  we probably cancelled.
        throw new Exception( "Unable to get transaction!" );
      }

      try
      {
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        // get the last time we did this
        var lastAccessTimeUtc = await _persister.GetConfigValueAsync("LastAccessTimeUtc", DateTime.MinValue, transaction, token ).ConfigureAwait(false);
        foreach (var directory in directories)
        {
          // if the directory exist... and it was flaged as newer than the last time
          // we checked the directory, then all we need to do is touch it.
          if (await _persister.DirectoryExistsAsync(directory, transaction, token).ConfigureAwait(false))
          {
            if (directory.LastAccessTimeUtc < lastAccessTimeUtc)
            {
              // the file has not changed since the last time
              continue;
            }

            // the file has changed.
            await _persister.TouchDirectoryAsync(directory, UpdateType.Changed, transaction, token).ConfigureAwait(false);
          }
          else
          {
            // the file is brand new ... so we need to add it to our list.
            // this will 'touch' it.
            await _persister.AddOrUpdateDirectoryAsync(directory, transaction, token).ConfigureAwait(false);

            // look for all the files in that directory.
            // so they can also be added.
            var files = await _directory.ParseDirectoryAsync(directory, token).ConfigureAwait(false);
            if (files != null)
            {
              await _persister.AddOrUpdateFilesAsync(files, transaction, token).ConfigureAwait(false);
            }

            // the folder has been parsed
            await _persister.MarkDirectoryProcessedAsync(directory, transaction, token).ConfigureAwait(false);
          }
        }

        // we can commit our code.
        _persister.Commit(transaction);

        // stop the watch and log how many items we found.
        stopwatch.Stop();
        _logger.Information($"Completed directory parsing: {parent.FullName} (Time Elapsed: {stopwatch.Elapsed:g})");

        // all done
        return true;
      }
      catch (Exception)
      {
        _persister.Rollback(transaction);
        throw;
      }
    }

    #region Start Parsing
    /// <summary>
    /// Start parsing.
    /// </summary>
    public void Start(CancellationToken token)
    {
      if (_runningTask != null && !_runningTask.IsCompleted)
      {
        Task.WaitAll(_runningTask);
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
        var fileWatcher = new FilesWatcher(path, _logger, new FilesSystemEventsParser(_persister, _directory, _config.Timers.EventsParserMs, _logger) );

        // and directory watcher.
        var directoryWatcher = new DirectoriesWatcher(path, _logger, new DirectoriesSystemEventsParser(_persister, _directory, _config.Timers.EventsParserMs, _logger) );

        fileWatcher.Start(token);
        directoryWatcher.Start(token);

        _watchers.Add(fileWatcher);
        _watchers.Add(directoryWatcher);
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
        Task.WaitAll(_runningTask);
        _runningTask = null;
      }
    }
    #endregion
  }
}
