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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using myoddweb.desktopsearch.interfaces.Logging;

namespace myoddweb.desktopsearch.parser.IO
{
  /// <summary>
  /// This file processes the file events at regular intervales.
  /// We use timers to ensure that we are not blocking the normal file usage.
  /// This means we have to be a bit carefule of files that might have been deleted/changed and so on.
  /// </summary>
  internal class FileSystemEventsParser
  {
    #region Member variables
    /// <summary>
    /// The logger that we will be using to log messages.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The list of current/unprocessed events.
    /// </summary>
    private readonly List<FileSystemEventArgs> _currentFileEvents = new List<FileSystemEventArgs>();

    /// <summary>
    /// The list of current/unprocessed events.
    /// </summary>
    private readonly List<FileSystemEventArgs> _currentDirectoryEvents = new List<FileSystemEventArgs>();

    /// <summary>
    /// The cancellation source
    /// </summary>
    private CancellationToken _token;

    /// <summary>
    /// When we register a token
    /// </summary>
    private CancellationTokenRegistration _cancellationTokenRegistration;

    /// <summary>
    /// All the tasks currently running
    /// </summary>
    private readonly List<Task> _tasks = new List<Task>();

    /// <summary>
    /// The folders we are ignoring.
    /// </summary>
    private readonly IEnumerable<DirectoryInfo> _ignorePaths;

    /// <summary>
    /// The lock so we can add/remove data
    /// </summary>
    private readonly object _lock = new object();

    /// <summary>
    /// The timer so we can clear some completed taks.
    /// </summary>
    private System.Timers.Timer _tasksTimer;

    /// <summary>
    /// How often we will be remobing compledted tasks.
    /// </summary>
    private readonly int _eventsTimeOutInMs;
    #endregion

    public FileSystemEventsParser(IReadOnlyCollection<DirectoryInfo> ignorePaths, int eventsParserMs, ILogger logger)
    {
      // the paths we want to ignore.
      _ignorePaths = ignorePaths ?? throw new ArgumentNullException(nameof(ignorePaths));

      if (eventsParserMs <= 0)
      {
        throw new ArgumentException( $"The event timeout, ({eventsParserMs}), cannot be zero or negative.");
      }
      _eventsTimeOutInMs = eventsParserMs;

      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region File Events Process Timer
    private void StartFileSystemEventsTimer()
    {
      if (null != _tasksTimer)
      {
        return;
      }

      lock (_lock)
      {
        if (_tasksTimer != null)
        {
          return;
        }

        _tasksTimer = new System.Timers.Timer(_eventsTimeOutInMs)
        {
          AutoReset = false,
          Enabled = true
        };
        _tasksTimer.Elapsed += FileSystemEventsProcess;
      }
    }

    /// <summary>
    /// Cleanup all the completed tasks
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void FileSystemEventsProcess(object sender, ElapsedEventArgs e)
    {
      try
      {
        // stop the timer
        StopFileSystemEventsTimer();

        // clean up the tasks.
        List<FileSystemEventArgs> fileEvents = null;
        List<FileSystemEventArgs> directoryEvents = null;
        lock (_lock)
        {
          if (_currentFileEvents.Count > 0)
          {
            // the events.
            fileEvents = _currentFileEvents.Select(s => s).ToList();

            // clear the current values within the lock
            _currentFileEvents.Clear();
          }

          if (_currentDirectoryEvents.Count > 0)
          {
            // the events.
            directoryEvents = _currentDirectoryEvents.Select(s => s).ToList();

            // clear the current values within the lock
            _currentDirectoryEvents.Clear();
          }

          // remove the completed events.
          _tasks.RemoveAll(t => t.IsCompleted);
        }

        if (null != fileEvents)
        {
          _tasks.Add(Task.Run(() => ProcessFileEvents(fileEvents), _token));
        }

        if (null != directoryEvents)
        {
          _tasks.Add(Task.Run(() => ProcessDirectoryEvents(directoryEvents), _token));
        }
      }
      catch (Exception exception)
      {
        _logger.Exception( exception);
      }
      finally
      {
        if (!_token.IsCancellationRequested)
        {
          // restart the timer.
          StartFileSystemEventsTimer();
        }
      }
    }

    /// <summary>
    /// Stop the heartbeats.
    /// </summary>
    private void StopFileSystemEventsTimer()
    {
      if (_tasksTimer == null)
      {
        return;
      }

      lock (_lock)
      {
        if (_tasksTimer == null)
        {
          return;
        }

        _tasksTimer.Enabled = false;
        _tasksTimer.Stop();
        _tasksTimer.Dispose();
        _tasksTimer = null;
      }
    }
    #endregion

    #region Watcher Change Types
    private bool Is(WatcherChangeTypes types, WatcherChangeTypes type)
    {
      return (types & type) == type;
    }

    private bool IsDeleted(WatcherChangeTypes types)
    {
      return Is( types, WatcherChangeTypes.Deleted);
    }
    #endregion

    #region Process File events
    /// <summary>
    /// Process a file/directory that was renamed.
    /// </summary>
    /// <param name="file"></param>
    /// <param name="renameEvent"></param>
    /// <returns></returns>
    private void ProcessRenameFileInfo(FileInfo file, RenamedEventArgs renameEvent)
    {
      if (null == file)
      {
        throw new ArgumentNullException(nameof(file));
      }

      // can we process this?
      if (!CanProcessFile(file, renameEvent.ChangeType))
      {
        return;
      }

      _logger.Verbose($"File: {renameEvent.OldFullPath} to {renameEvent.FullPath}");
    }

    /// <summary>
    /// Parse a normal file event
    /// </summary>
    /// <param name="file"></param>
    /// <param name="fileEvent"></param>
    /// <returns></returns>
    private void ProcessFileInfo(FileInfo file, FileSystemEventArgs fileEvent)
    {
      if (null == file)
      {
        throw new ArgumentNullException(nameof(file));
      }

      // can we process this?
      if (!CanProcessFile(file, fileEvent.ChangeType))
      {
        return;
      }

      // the given file is going to be processed.
      _logger.Verbose($"File: {fileEvent.FullPath} ({fileEvent.ChangeType})");
    }

    /// <summary>
    /// Check if we can process this file or not.
    /// </summary>
    /// <param name="file"></param>
    /// <param name="types"></param>
    /// <returns></returns>
    private bool CanProcessFile(FileInfo file, WatcherChangeTypes types)
    {
      // it is a file that we can read?
      // we do not check delted files as they are ... deleted.
      if (!IsDeleted(types) && !Helper.File.CanReadFile(file))
      {
        return false;
      }

      // do we monitor this directory?
      if (Helper.File.IsSubDirectory(_ignorePaths, file.Directory))
      {
        return false;
      }
      return true;
    }

    /// <summary>
    /// This function is called when processing a list of file events.
    /// It should be non-blocking.
    /// </summary>
    /// <param name="events"></param>
    private void ProcessFileEvents(IEnumerable<FileSystemEventArgs> events)
    {
      foreach (var e in events)
      {
        var file = new FileInfo(e.FullPath);
        if (e is RenamedEventArgs renameEvent)
        {
          ProcessRenameFileInfo(file, renameEvent);
        }
        else
        {
          ProcessFileInfo(file, e);
        }
      }
    }
    #endregion

    #region Process Directory events
    /// <summary>
    /// Process a file/directory that was renamed.
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="renameEvent"></param>
    /// <returns></returns>
    private void ProcessRenameDirectoryInfo(DirectoryInfo directory, RenamedEventArgs renameEvent)
    {
      if (null == directory)
      {
        throw new ArgumentNullException(nameof(directory));
      }

      if (!CanProcessDirectory(directory, renameEvent.ChangeType ))
      {
        return;
      }

      _logger.Verbose($"Directory: {renameEvent.OldFullPath} to {renameEvent.FullPath}");
    }

    /// <summary>
    /// Parse a normal file event
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="fileEvent"></param>
    /// <returns></returns>
    private void ProcessDirectoryInfo(DirectoryInfo directory, FileSystemEventArgs fileEvent)
    {
      // it is a directory
      if( !CanProcessDirectory(directory, fileEvent.ChangeType ))
      {
        return;
      }
      _logger.Verbose($"Directory: {fileEvent.FullPath} ({fileEvent.ChangeType})");
    }

    /// <summary>
    /// Check if we can process this directory or not.
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="types"></param>
    /// <returns></returns>
    private bool CanProcessDirectory(DirectoryInfo directory, WatcherChangeTypes types )
    {
      // it is a file that we can read?
      // it is a file that we can read?
      if (!IsDeleted(types) && !Helper.File.CanReadDirectory(directory))
      {
        return false;
      }

      // do we monitor this directory?
      if (Helper.File.IsSubDirectory(_ignorePaths, directory))
      {
        return false;
      }

      return true;
    }

    /// <summary>
    /// This function is called when processing a list of file events.
    /// It should be non-blocking.
    /// </summary>
    /// <param name="events"></param>
    private void ProcessDirectoryEvents(IEnumerable<FileSystemEventArgs> events)
    {
      foreach (var e in events)
      {
        var directory = new DirectoryInfo(e.FullPath);
        if (e is RenamedEventArgs renameEvent)
        {
          ProcessRenameDirectoryInfo(directory, renameEvent);
        }
        else
        {
          ProcessDirectoryInfo(directory, e);
        }
      }
    }
    #endregion

    /// <summary>
    /// Start watching for the folder changes.
    /// </summary>
    public void Start( CancellationToken token )
    {
      // stop what might have already started.
      Stop();

      // start the source.
      _token = token;

      // register the token cancellation
      _cancellationTokenRegistration = _token.Register(TokenCancellation);

      StartFileSystemEventsTimer();
    }
    
    /// <summary>
    /// Stop the folder monitoring.
    /// </summary>
    public void Stop()
    {
      // stop the cleanup timer
      // we don't need it anymore.
      StopFileSystemEventsTimer();

      lock (_lock)
      {
        //  cancel all the tasks.
        _tasks.RemoveAll(t => t.IsCompleted);

        // wait for them all to finish
        try
        {
          if (_tasks.Count > 0)
          {
            // Log that we are stopping the tasks.
            _logger.Verbose($"Waiting for {_tasks.Count} tasks to complete in the File System Events Timer.");

            // then wait...
            Task.WaitAll(_tasks.ToArray(), _token);

            // done 
            _logger.Verbose("Done.");
          }
        }
        catch (OperationCanceledException e)
        {
          // ignore the cancelled exceptions.
          if (e.CancellationToken != _token)
          {
            throw;
          }
        }
        finally
        {
          _cancellationTokenRegistration.Dispose();
          _tasks.Clear();
        }
      }
    }

    /// <summary>
    /// Called when the token has been cancelled.
    /// </summary>
    private void TokenCancellation()
    {
      _logger.Verbose("Stopping Events parser");
      Stop();
      _logger.Verbose("Done events parser");
    }

    /// <summary>
    /// Add a file event to the list.
    /// </summary>
    /// <param name="fileSystemEventArgs"></param>
    public void AddFile(FileSystemEventArgs fileSystemEventArgs)
    {
      lock (_lock)
      {
        _currentFileEvents.Add(fileSystemEventArgs);
      }
    }

    /// <summary>
    /// Add a file event to the list.
    /// </summary>
    /// <param name="fileSystemEventArgs"></param>
    public void AddDirectory(FileSystemEventArgs fileSystemEventArgs)
    {
      lock (_lock)
      {
        _currentDirectoryEvents.Add(fileSystemEventArgs);
      }
    }
  }
}
