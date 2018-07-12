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
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;
using myoddweb.desktopsearch.parser.IO;

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
    private FileSystemEventsParser _eventsParser;

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
    /// Start parsing.
    /// </summary>
    public void Start( CancellationToken token )
    {
      var thread = new Thread(async () => await WorkAsync(token).ConfigureAwait(false));
      thread.Start();
      _logger.Information("Parser started");
    }

    /// <summary>
    /// Get all the paths we want to ignore.
    /// </summary>
    /// <returns></returns>
    private IReadOnlyCollection<DirectoryInfo> GetIgnorePaths()
    {
      var paths = new List<DirectoryInfo>();

      // get all the paths we want to ignore.
      foreach (var path in _config.Paths.IgnoredPaths)
      {
        try
        {
          var expendedVariable = Environment.ExpandEnvironmentVariables(path);
          paths.Add(new DirectoryInfo(expendedVariable ));
        }
        catch (Exception e)
        {
          _logger.Exception(e);
        }
      }
      return paths;
    }

    /// <summary>
    /// Get all the start paths so we can monitor them.
    /// As well as parse them all.
    /// </summary>
    /// <returns></returns>
    private IReadOnlyCollection<DirectoryInfo> GetStartPaths()
    {
      var drvs = DriveInfo.GetDrives();
      var paths = new List<DirectoryInfo>();
      foreach (var drv in drvs)
      {
        switch (drv.DriveType)
        {
          case DriveType.Fixed:
            if (_config.Paths.ParseFixedDrives)
            {
              paths.Add(new DirectoryInfo(drv.Name));
            }
            break;

          case DriveType.Removable:
            if (_config.Paths.ParseRemovableDrives)
            {
              paths.Add(new DirectoryInfo(drv.Name));
            }
            break;
        }
      }

      // then we try and add the folders as given by the user
      // but if the ones given by the user is a child of the ones 
      // we already have, then there is no point in adding it.
      foreach (var path in _config.Paths.Paths )
      {
        if (!Helper.File.IsSubDirectory(paths, new DirectoryInfo(path)))
        {
          paths.Add( new DirectoryInfo(path));
        }
      }

      // This is the list of all the paths we want to parse.
      return paths;
    }

    /// <summary>
    /// Do all the parsing work,
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> WorkAsync(CancellationToken token)
    {
      // get all the paths we will be working with.
      var paths = GetStartPaths();
      var ignorePaths = GetIgnorePaths();

      // first we get a full list of files/directories.
      if (!await ParseAllDirectoriesAsync(paths, ignorePaths, token).ConfigureAwait(false))
      {
        return false;
      }

      // we then watch for files/folder changes.
      if( !StartWatchers(paths, token ))
      {
        return false;
      }

      // finally start the file parser.
      StartFileSystemEventParser(ignorePaths, token);

      return true;
    }

    /// <summary>
    /// Start the file event parser that will process files changes,
    /// </summary>
    /// <param name="ignorePaths"></param>
    /// <param name="token"></param>
    private void StartFileSystemEventParser( IReadOnlyCollection<DirectoryInfo> ignorePaths, CancellationToken token )
    {
      _eventsParser = new FileSystemEventsParser(ignorePaths, _config.Timers.EventsParserMs, _logger);
      _eventsParser.Start( token );
    }

    /// <summary>
    /// Stop the file event parser.
    /// </summary>
    private void StopFileSystemEventParser()
    {
      _eventsParser?.Stop();
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
        fileWatcher.Error += OnFolderError;
        fileWatcher.Changed += OnFileTouched;
        fileWatcher.Renamed += OnFileTouched;
        fileWatcher.Created += OnFileTouched;
        fileWatcher.Deleted += OnFileTouched;

        var directoryWatcher = new DirectoriesWatcher(path, _logger);
        directoryWatcher.Changed += OnDirectoryTouched;
        directoryWatcher.Renamed += OnDirectoryTouched;
        directoryWatcher.Created += OnDirectoryTouched;
        directoryWatcher.Deleted += OnDirectoryTouched;

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
    private void OnFolderError( ErrorEventArgs e)
    {
      // the watcher raised an error
      _logger.Error( $"File watcher error: {e.GetException().Message}");
    }

    /// <summary>
    /// When a file was changed
    /// </summary>
    /// <param name="e"></param>
    private void OnFileTouched(FileSystemEventArgs e)
    {
      // It is posible that the event parser has not started yet.
      _eventsParser?.AddFile(e);
    }

    /// <summary>
    /// When a directory has been changed.
    /// </summary>
    /// <param name="e"></param>
    private void OnDirectoryTouched(FileSystemEventArgs e)
    {
      // It is posible that the event parser has not started yet.
      _eventsParser?.AddDirectory(e);
    }

    /// <summary>
    /// Parse all the directories.
    /// </summary>
    /// <param name="paths"></param>
    /// <param name="ignorePaths"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> ParseAllDirectoriesAsync(
      IEnumerable<DirectoryInfo> paths,
      IReadOnlyCollection<DirectoryInfo> ignorePaths, 
      CancellationToken token)
    {
      try
      {
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var totalDirectories = 0;
        foreach (var path in paths)
        {
          var directoriesParser = new DirectoriesParser(path, ignorePaths, _logger, _directory);
          await directoriesParser.SearchAsync(token).ConfigureAwait(false);

          // process all the files.
          if (!await ParseDirectoryAsync(directoriesParser.Directories, token).ConfigureAwait(false))
          {
            stopwatch.Stop();
            _logger.Warning($"Parsing was cancelled (Elapsed:{stopwatch.Elapsed:g})");
            return false;
          }

          // get the number of directories parsed.
          totalDirectories += directoriesParser.Directories.Count;
        }

        // stop the watch and log how many items we found.
        stopwatch.Stop();
        _logger.Information($"Parsed {totalDirectories} directories (Elapsed:{stopwatch.Elapsed:g})");
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
    private async Task<bool> ParseDirectoryAsync(IEnumerable<DirectoryInfo> directories, CancellationToken token)
    {
      // add the folder to the list.
      if (!await _perister.AddOrUpdateFoldersAsync(directories, token).ConfigureAwait(false))
      {
        return false;
      }

      // we always parse sub directories
      return true;
    }

    /// <summary>
    /// Stop parsing
    /// </summary>
    public void Stop()
    {
      StopWatchers();
      StopFileSystemEventParser();
    }
  }
}
