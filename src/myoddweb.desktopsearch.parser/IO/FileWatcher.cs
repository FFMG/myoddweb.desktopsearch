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
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using myoddweb.desktopsearch.interfaces.Logging;

namespace myoddweb.desktopsearch.parser.IO
{
  public delegate void FileEventHandler( FileSystemEventArgs e);

  public delegate void RenamedEventHandler(RenamedEventArgs e);

  public delegate void ErrorEventHandler( ErrorEventArgs e);

  internal class FileWatcher
  {
    /// <summary>
    /// How often we will be remobing compledted tasks.
    /// </summary>
    private const int TaskTimerTimeOutInMs = 30000;

    #region Member variables
    /// <summary>
    /// The logger that we will be using to log messages.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The actual file watcher.
    /// </summary>
    private FileSystemWatcher _watcher;

    /// <summary>
    /// The folder we are watching
    /// </summary>
    private readonly string _folder;

    /// <summary>
    /// All the tasks currently running
    /// </summary>
    private readonly List<Task> _tasks = new List<Task>();

    /// <summary>
    /// The lock so we can add/remove data
    /// </summary>
    private readonly object _lock = new object();

    /// <summary>
    /// The timer so we can clear some completed taks.
    /// </summary>
    private System.Timers.Timer _tasksTimer;

    /// <summary>
    /// The cancellation source
    /// </summary>
    private CancellationToken _token;

    /// <summary>
    /// When we register a token
    /// </summary>
    private CancellationTokenRegistration _cancellationTokenRegistration;
    #endregion

    #region Events handler
    /// <summary>
    ///     Occurs when a file or directory in the specified System.IO.FileSystemWatcher.Path
    ///     is deleted.
    /// </summary>
    public event FileEventHandler Deleted = delegate { };

    /// <summary>
    ///     Occurs when a file or directory in the specified System.IO.FileSystemWatcher.Path
    ///     is created.
    /// </summary>
    public event FileEventHandler Created = delegate { };

    /// <summary>
    ///     Occurs when a file or directory in the specified System.IO.FileSystemWatcher.Path
    ///     is changed.
    /// </summary>
    public event FileEventHandler Changed = delegate { };

    /// <summary>
    ///     Occurs when a file or directory in the specified System.IO.FileSystemWatcher.Path
    ///     is renamed.
    /// </summary>
    public event RenamedEventHandler Renamed = delegate { };

    /// <summary>
    ///     Occurs when the instance of System.IO.FileSystemWatcher is unable to continue
    ///     monitoring changes or when the internal buffer overflows.
    /// </summary>
    public event ErrorEventHandler Error = delegate { };
    #endregion

    /// <summary>
    /// Constructor to prepare the file watcher.
    /// </summary>
    /// <param name="folder"></param>
    /// <param name="logger"></param>
    public FileWatcher( string folder, ILogger logger )
    {
      // the folder being watched.
      _folder = folder ?? throw new ArgumentNullException(nameof(folder));

      // save the logger.
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Task Cleanup Timer
    private void StartTasksCleanupTimer()
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

        _tasksTimer = new System.Timers.Timer(TaskTimerTimeOutInMs)
        {
          AutoReset = false,
          Enabled = true
        };
        _tasksTimer.Elapsed += TaskCleanup;
      }
    }

    /// <summary>
    /// Cleanup all the completed tasks
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void TaskCleanup(object sender, ElapsedEventArgs e)
    {
      // stop the timer
      StopTasksCleanupTimer();

      // clean up the tasks.
      lock (_lock)
      {
        _tasks.RemoveAll(t => t.IsCompleted);
      }

      if (!_token.IsCancellationRequested)
      {
        // restart the timer.
        StartTasksCleanupTimer();
      }
      else
      {
        Stop();
      }
    }

    /// <summary>
    /// Stop the heartbeats.
    /// </summary>
    private void StopTasksCleanupTimer()
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

    #region Folder Event
    private void OnFolderDeleted(object sender, FileSystemEventArgs e)
    {
      lock (_lock)
      {
        _tasks.Add(Task.Run(() => Deleted( e), _token));
      }
    }

    private void OnFolderCreated(object sender, FileSystemEventArgs e)
    {
      lock (_lock)
      {
        _tasks.Add(Task.Run(() => Created( e), _token));
      }
    }

    private void OnFolderRenamed(object sender, RenamedEventArgs e)
    {
      lock (_lock)
      {
        _tasks.Add(Task.Run(() => Renamed( e), _token));
      }
    }

    private void OnFolderChanged(object sender, FileSystemEventArgs e)
    {
      lock (_lock)
      {
        _tasks.Add(Task.Run(() => Changed( e), _token));
      }
    }

    private void OnFolderError(object sender, ErrorEventArgs e)
    {
      try
      {
        // stop everything 
        Stop();

        // we cannot use the tasks here
        // so just show there was an error
        Error(e);
      }
      finally 
      {
        // restart everything
        Start(_token);
      }
    }
    #endregion 

    /// <summary>
    /// Stop the folder monitoring.
    /// </summary>
    public void Stop()
    {
      // stop the cleanup timer
      // we don't need it anymore.
      StopTasksCleanupTimer();

      if (_watcher == null)
      {
        return;
      }

      _watcher.EnableRaisingEvents = false;
      _watcher.Dispose();
      _watcher = null;

      lock (_lock)
      {
        //  cancel all the tasks.
        _tasks.RemoveAll(t => t.IsCompleted);

        try
        {
          // wait for them all to finish
          if (_tasks.Count > 0)
          {
            // Log that we are stopping the tasks.
            _logger.Verbose($"Waiting for {_tasks.Count} tasks to complete in the File watcher.");

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
    /// Start watching for the folder changes.
    /// </summary>
    public void Start( CancellationToken token )
    {
      // stop what might have already started.
      Stop();

      _token = token;

      // it is posible that we are trying to (re)start an event that was
      // cancelled at some point.
      if (_token.IsCancellationRequested)
      {
        return;
      }

      // register the token cancellation
      _cancellationTokenRegistration = _token.Register(TokenCancellation);

      // start the file watcher
      _watcher = new FileSystemWatcher
      {
        Path = _folder,
        NotifyFilter = NotifyFilters.LastWrite,
        Filter = "*.*",
        IncludeSubdirectories = true,
        EnableRaisingEvents = true,
        InternalBufferSize = 64 * 1024
      };

      _watcher.Error += OnFolderError;
      _watcher.Renamed += OnFolderRenamed;

      _watcher.Changed += OnFolderChanged;
      _watcher.Created += OnFolderCreated;
      _watcher.Deleted += OnFolderDeleted;

      _watcher.EnableRaisingEvents = true;

      // we can now start monitoring for tasks.
      // so we can remove the completed ones from time to time.
      StartTasksCleanupTimer();
    }

    /// <summary>
    /// Called when the token has been cancelled.
    /// </summary>
    private void TokenCancellation()
    {
      _logger.Verbose( $"Stopping File watcher : {_folder}" );
      Stop();
      _logger.Verbose($"Done File watcher : {_folder}");
    }
  }
}
