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
  public delegate void FileEventHandler(FileSystemEventArgs e);

  public delegate void RenamedEventHandler(RenamedEventArgs e);

  public delegate void ErrorEventHandler(ErrorEventArgs e);

  internal abstract class Watcher
  {
    /// <summary>
    /// How often we will be remobing compledted tasks.
    /// </summary>
    private const int TaskTimerTimeOutInMs = 30000;

    #region Member variables
    /// <summary>
    /// The type of folders we are watching
    /// </summary>
    private readonly WatcherTypes _watcherTypes;

    /// <summary>
    /// The logger that we will be using to log messages.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// The actual file watcher.
    /// </summary>
    private FileSystemWatcher _fileWatcher;

    /// <summary>
    /// The actual file watcher.
    /// </summary>
    private FileSystemWatcher _directoryWatcher;

    /// <summary>
    /// The folder we are watching
    /// </summary>
    protected DirectoryInfo Folder { get; }

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
    /// Occurs when a file is deleted.
    /// </summary>
    public event FileEventHandler Deleted = delegate { };

    /// <summary>
    ///  Occurs when a file is created.
    /// </summary>
    public event FileEventHandler Created = delegate { };

    /// <summary>
    /// Occurs when a file is changed.
    /// </summary>
    public event FileEventHandler Changed = delegate { };

    /// <summary>
    /// Occurs when a file is renamed.
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
    /// <param name="watcherTypes"></param>
    /// <param name="folder"></param>
    /// <param name="logger"></param>
    protected Watcher ( WatcherTypes watcherTypes, DirectoryInfo folder, ILogger logger)
    {
      // save the type of folders we are watching.
      _watcherTypes = watcherTypes;

      // the folder being watched.
      Folder = folder ?? throw new ArgumentNullException(nameof(folder));

      // save the logger.
      Logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
        if (sender == _fileWatcher || sender == _directoryWatcher )
        {
          _tasks.Add(Task.Run(() => Deleted(e), _token));
        }
      }
    }

    private void OnFolderCreated(object sender, FileSystemEventArgs e)
    {
      lock (_lock)
      {
        if (sender == _fileWatcher || sender == _directoryWatcher)
        {
          _tasks.Add(Task.Run(() => Created(e), _token));
        }
      }
    }

    private void OnFolderRenamed(object sender, RenamedEventArgs e)
    {
      lock (_lock)
      {
        if (sender == _fileWatcher || sender == _directoryWatcher)
        {
          _tasks.Add(Task.Run(() => Renamed(e), _token));
        }
      }
    }

    private void OnFolderChanged(object sender, FileSystemEventArgs e)
    {
      lock (_lock)
      {
        if (sender == _fileWatcher || sender == _directoryWatcher)
        {
          _tasks.Add(Task.Run(() => Changed(e), _token));
        }
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

      if (_fileWatcher != null)
      {
        _fileWatcher.EnableRaisingEvents = false;
        _fileWatcher.Dispose();
        _fileWatcher = null;
      }

      if (_directoryWatcher != null)
      {
        _directoryWatcher.EnableRaisingEvents = false;
        _directoryWatcher.Dispose();
        _directoryWatcher = null;
      }
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
            Logger.Verbose($"Waiting for {_tasks.Count} tasks to complete in the Watcher.");

            Task.WaitAll(_tasks.ToArray(), _token);

            // done 
            Logger.Verbose("Done.");
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
    public void Start(CancellationToken token)
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
      StartFilesWatcher();

      // start the directory watcher.
      StartDirectoriesWatcher();

      // we can now start monitoring for tasks.
      // so we can remove the completed ones from time to time.
      StartTasksCleanupTimer();
    }

    private void StartDirectoriesWatcher()
    {
      if ((_watcherTypes & WatcherTypes.Directories) != WatcherTypes.Directories)
      {
        return;
      }

      _directoryWatcher = new FileSystemWatcher
      {
        Path = Folder.FullName,
        NotifyFilter = NotifyFilters.DirectoryName,
        Filter = "*.*",
        IncludeSubdirectories = true,
        EnableRaisingEvents = true,
        InternalBufferSize = 64 * 1024
      };

      _directoryWatcher.Error += OnFolderError;
      _directoryWatcher.Renamed += OnFolderRenamed;
      _directoryWatcher.Changed += OnFolderChanged;
      _directoryWatcher.Created += OnFolderCreated;
      _directoryWatcher.Deleted += OnFolderDeleted;
    }

    private void StartFilesWatcher()
    {
      if ((_watcherTypes & WatcherTypes.Files) != WatcherTypes.Files)
      {
        return;
      }

      _fileWatcher = new FileSystemWatcher
      {
        Path = Folder.FullName,
        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
        Filter = "*.*",
        IncludeSubdirectories = true,
        EnableRaisingEvents = true,
        InternalBufferSize = 64 * 1024
      };

      _fileWatcher.Error += OnFolderError;
      _fileWatcher.Renamed += OnFolderRenamed;
      _fileWatcher.Changed += OnFolderChanged;
      _fileWatcher.Created += OnFolderCreated;
      _fileWatcher.Deleted += OnFolderDeleted;
    }

    /// <summary>
    /// Called when the token has been cancelled.
    /// </summary>
    private void TokenCancellation()
    {
      OnCancelling();
      Stop();
      OnCancelled();
    }

    protected abstract void OnCancelling();

    protected abstract void OnCancelled();
  }
}
