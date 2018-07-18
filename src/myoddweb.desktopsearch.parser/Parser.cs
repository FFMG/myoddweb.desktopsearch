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
    /// The file watchers
    /// </summary>
    private readonly List<Watcher> _watchers = new List<Watcher>();

    /// <summary>
    /// The files event parser.
    /// </summary>
    private DirectoriesSystemEventsParser _directoriesEventsParser;

    /// <summary>
    /// The files event parser.
    /// </summary>
    private FilesSystemEventsParser _filesEventsParser;

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
      if( !StartWatchers(paths, token ))
      {
        return false;
      }

      // get the ignore path.
      var ignorePaths = helper.IO.Paths.GetIgnorePaths(_config.Paths, _logger);

      // finally start the file parser.
      StartSystemEventParsers(ignorePaths, token);

      return true;
    }

    /// <summary>
    /// Start the file event parser that will process files changes,
    /// </summary>
    /// <param name="ignorePaths"></param>
    /// <param name="token"></param>
    private void StartSystemEventParsers( IReadOnlyCollection<DirectoryInfo> ignorePaths, CancellationToken token )
    {
      _filesEventsParser = new FilesSystemEventsParser( _perister, ignorePaths, _config.Timers.EventsParserMs, _logger);
      _filesEventsParser.Start( token );

      _directoriesEventsParser = new DirectoriesSystemEventsParser( _perister, ignorePaths, _config.Timers.EventsParserMs, _logger);
      _directoriesEventsParser.Start(token);
    }

    /// <summary>
    /// Stop the file event parser.
    /// </summary>
    private void StopSystemEventParsers()
    {
      _filesEventsParser?.Stop();
      _directoriesEventsParser?.Stop();
    }

    /// <summary>
    /// Stop the file watcher
    /// </summary>
    private void StopWatchers()
    {
      // are we watching?
      foreach (var watcher in _watchers)
      {
        watcher?.Stop();
      }
      _watchers.Clear();
    }

    /// <summary>
    /// Start to watch all the folders and sub folders.
    /// </summary>
    /// <param name="paths"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private bool StartWatchers( IEnumerable<DirectoryInfo> paths, CancellationToken token )
    {
      foreach (var path in paths)
      {
        var fileWatcher = new FilesWatcher(path, _logger);
        fileWatcher.ErrorAsync += OnFolderErrorAsync;
        fileWatcher.ChangedAsync += OnFileTouchedAsync;
        fileWatcher.RenamedAsync += OnFileTouchedAsync;
        fileWatcher.CreatedAsync += OnFileTouchedAsync;
        fileWatcher.DeletedAsync += OnFileTouchedAsync;

        var directoryWatcher = new DirectoriesWatcher(path, _logger);
        directoryWatcher.ErrorAsync += OnFolderErrorAsync;
        directoryWatcher.ChangedAsync += OnDirectoryTouchedAsync;
        directoryWatcher.RenamedAsync += OnDirectoryTouchedAsync;
        directoryWatcher.CreatedAsync += OnDirectoryTouchedAsync;
        directoryWatcher.DeletedAsync += OnDirectoryTouchedAsync;

        fileWatcher.Start(token);
        directoryWatcher.Start(token);

        _watchers.Add(fileWatcher);
        _watchers.Add(directoryWatcher);
      }
      return true;
    }

    /// <summary>
    /// When the file watcher errors out.
    /// </summary>
    /// <param name="e"></param>
    /// <param name="token"></param>
    private Task OnFolderErrorAsync( ErrorEventArgs e, CancellationToken token)
    {
      // the watcher raised an error
      _logger.Error( $"File watcher error: {e.GetException().Message}");
      return Task.FromResult<object>(null);
    }

    /// <summary>
    /// When a file was changed
    /// </summary>
    /// <param name="e"></param>
    /// <param name="token"></param>
    private Task OnFileTouchedAsync(FileSystemEventArgs e, CancellationToken token)
    {
      // It is posible that the event parser has not started yet.
      _filesEventsParser?.Add(e);
      return Task.FromResult<object>(null);
    }

    /// <summary>
    /// When a directory has been changed.
    /// </summary>
    /// <param name="e"></param>
    /// <param name="token"></param>
    private Task OnDirectoryTouchedAsync(FileSystemEventArgs e, CancellationToken token )
    {
      // It is posible that the event parser has not started yet.
      _directoriesEventsParser?.Add(e);
      return Task.FromResult<object>(null);
    }

    /// <summary>
    /// Parse all the directories.
    /// </summary>
    /// <param name="paths"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> ParseAllDirectoriesAsync(
      IEnumerable<DirectoryInfo> paths,
      CancellationToken token)
    {
      try
      {
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var totalDirectories = 0;
        foreach (var path in paths)
        {
          // check if we need to break out
          if (token.IsCancellationRequested)
          {
            break;
          }

          // get all the directories.
          var directories = await _directory.ParseDirectoriesAsync(path, token).ConfigureAwait(false);
          if (directories == null)
          {
            continue;
          }

          // process all the files.
          if (!await ParseDirectoryAsync(directories, token).ConfigureAwait(false))
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
    private async Task<bool> ParseDirectoryAsync(IReadOnlyList<DirectoryInfo> directories, CancellationToken token)
    {
      // get a transaction
      var transaction = _perister.BeginTransaction();
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

    /// <summary>
    /// Start parsing.
    /// </summary>
    public void Start(CancellationToken token)
    {
      var thread = new Thread(async () => await WorkAsync(token).ConfigureAwait(false));
      thread.Start();
      _logger.Information("Parser started");
    }

    /// <summary>
    /// Stop parsing
    /// </summary>
    public void Stop()
    {
      StopWatchers();
      StopSystemEventParsers();
    }
  }
}
