using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace myoddweb.desktopsearch.parser
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
    private CancellationTokenSource _source;

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
    public FileWatcher( string folder )
    {
      _folder = folder;
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

      // restart the timer.
      StartTasksCleanupTimer();
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

    private void OnFolderDeleted(object sender, FileSystemEventArgs e)
    {
      lock (_lock)
      {
        _tasks.Add(Task.Run(() => Deleted( e), _source.Token));
      }
    }

    private void OnFolderCreated(object sender, FileSystemEventArgs e)
    {
      lock (_lock)
      {
        _tasks.Add(Task.Run(() => Created( e), _source.Token));
      }
    }

    private void OnFolderRenamed(object sender, RenamedEventArgs e)
    {
      lock (_lock)
      {
        _tasks.Add(Task.Run(() => Renamed( e), _source.Token));
      }
    }

    private void OnFolderChanged(object sender, FileSystemEventArgs e)
    {
      lock (_lock)
      {
        _tasks.Add(Task.Run(() => Changed( e), _source.Token));
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
        Start();
      }
    }

    /// <summary>
    /// Stop the folder monitoring.
    /// </summary>
    public void Stop()
    {
      if (_watcher == null)
      {
        return;
      }

      // cancel whatever we might be busy with.
      _source?.Cancel();

      // stop the cleanup timer
      // we don't need it anymore.
      StopTasksCleanupTimer();

      lock (_lock)
      {
        //  cancel all the tasks.
        _tasks.RemoveAll(t => t.IsCompleted);

        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _watcher = null;

        // wait for them all to finish
        try
        {
          if(_tasks.Count > 0 )
          { 
            Task.WaitAll(_tasks.ToArray(), _source?.Token ?? new CancellationToken() );
          }
        }
        catch (OperationCanceledException e)
        {
          // ignore the cancelled exceptions.
          if (e.CancellationToken != _source?.Token)
          {
            throw;
          }
        }

        _tasks.Clear();
        _source = null;
      }
    }

    /// <summary>
    /// Start watching for the folder changes.
    /// </summary>
    public void Start()
    {
      // stop what might have already started.
      Stop();

      _source = new CancellationTokenSource();

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
  }
}
