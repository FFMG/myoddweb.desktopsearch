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
using System.Diagnostics;
using System.IO;
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
    private readonly IPersister _perister;

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
      _perister = persister ?? throw new ArgumentNullException(nameof(persister));

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
          if (!await PersistDirectories(directories, token).ConfigureAwait(false))
          {
            stopwatch.Stop();
            _logger.Warning($"Parsing was cancelled (Time Elapsed: {stopwatch.Elapsed:g})");
            return false;
          }

          // get the number of directories parsed.
          totalDirectories += directories.Count;
        }

        // stop the watch and log how many items we found.
        stopwatch.Stop();
        _logger.Information($"Parsed {totalDirectories} directories (Time Elapsed: {stopwatch.Elapsed:g})");
        return true;
      }
      catch (OperationCanceledException)
      {
        return false;
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        return false;
      }
    }

    /// <summary>
    /// Check if we want to parse this directory or not.
    /// </summary>
    /// <param name="directories"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> PersistDirectories(IReadOnlyList<DirectoryInfo> directories, CancellationToken token)
    {
      // get a transaction
      var transaction = await _perister.BeginTransactionAsync(token).ConfigureAwait(false);
      try
      {
        // add the folder to the list.
        if (!await _perister.AddOrUpdateDirectoriesAsync(directories, transaction, token).ConfigureAwait(false))
        {
          _perister.Rollback(transaction);
          return false;
        }

        // we can commit our code.
        _perister.Commit(transaction);

        // all done
        return true;
      }
      catch (Exception e)
      {
        _perister.Rollback(transaction);
        _logger.Exception(e);
      }

      // if we are here
      return false;
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
        var fileWatcher = new FilesWatcher(path, _logger, new FilesSystemEventsParser(_perister, _directory, _config.Timers.EventsParserMs, _logger) );

        // and directory watcher.
        var directoryWatcher = new DirectoriesWatcher(path, _logger, new DirectoriesSystemEventsParser(_perister, _directory, _config.Timers.EventsParserMs, _logger) );

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
