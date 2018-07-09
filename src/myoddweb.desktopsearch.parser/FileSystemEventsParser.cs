using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using myoddweb.desktopsearch.interfaces.Logging;

namespace myoddweb.desktopsearch.parser
{
  internal class FileSystemEventsParser : FileHelper
  {
    /// <summary>
    /// The logger that we will be using to log messages.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The list of current/unprocessed events.
    /// </summary>
    private readonly List<FileSystemEventArgs> _currentEvents = new List<FileSystemEventArgs>();

    /// <summary>
    /// How often we will be remobing compledted tasks.
    /// </summary>
    private const int FileSystemEventsTimeOutInMs = 10000;

    /// <summary>
    /// The cancellation source
    /// </summary>
    private CancellationTokenSource _source;

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

    public FileSystemEventsParser(ILogger logger) : base(logger)
    {
      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Task Cleanup Timer
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

        _tasksTimer = new System.Timers.Timer(FileSystemEventsTimeOutInMs)
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
      // stop the timer
      StopFileSystemEventsTimer();

      // clean up the tasks.
      List<FileSystemEventArgs> events = null;
      lock (_lock)
      {
        if (_currentEvents.Count > 0)
        {
          // the events.
          events = _currentEvents.Select(s => s).ToList();

          // clear the current values within the lock
          _currentEvents.Clear();
        }

        // remove the completed events.
        _tasks.RemoveAll(t => t.IsCompleted);
      }

      if (null != events)
      {
        _tasks.Add(Task.Run(() => ProcessEvents( events ), _source.Token));
      }

      // restart the timer.
      StartFileSystemEventsTimer();
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

    private void ProcessEvents(IEnumerable<FileSystemEventArgs> events )
    {
      foreach (var e in events)
      {
        var file = new FileInfo(e.FullPath);
 
        if (e is RenamedEventArgs r)
        {
          if (IsDirectory(file))
          {
            if (!CanReadDirectory( new DirectoryInfo(file.FullName)))
            {
              continue;
            }
            _logger.Verbose($"Directory: {r.OldFullPath} to {r.FullPath}");
          }
          else
          {
            if (!CanReadFile(file))
            {
              continue;
            }
            _logger.Verbose($"File: {r.OldFullPath} to {r.FullPath}");
          }
        }
        else
        {
          if (IsDirectory(file))
          {
            if (!CanReadDirectory(new DirectoryInfo(file.FullName)))
            {
              continue;
            }
            _logger.Verbose($"Directory: {e.FullPath} ({e.ChangeType})");
          }
          else
          {
            if (!CanReadFile(file))
            {
              continue;
            }
            _logger.Verbose($"File: {e.FullPath} ({e.ChangeType})");
          }
        }
      }
    }

    /// <summary>
    /// Start watching for the folder changes.
    /// </summary>
    public void Start()
    {
      // stop what might have already started.
      Stop();

      // start the source.
      _source = new CancellationTokenSource();

      StartFileSystemEventsTimer();
    }

    /// <summary>
    /// Stop the folder monitoring.
    /// </summary>
    public void Stop()
    {
      // cancel whatever we might be busy with.
      _source?.Cancel();
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
            Task.WaitAll(_tasks.ToArray(), _source?.Token ?? new CancellationToken());
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
    /// Add a file event to the list.
    /// </summary>
    /// <param name="fileSystemEventArgs"></param>
    public void Add(FileSystemEventArgs fileSystemEventArgs)
    {
      lock (_lock)
      {
        _currentEvents.Add(fileSystemEventArgs);
      }
    }
  }
}
