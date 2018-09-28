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
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Logging;

namespace myoddweb.desktopsearch.parser.IO
{
  public delegate Task FileEventHandler(IFileSystemEvent e );

  public delegate Task ErrorEventHandler(Exception e, CancellationToken token);

  internal abstract class Watcher
  {
    /// <summary>
    /// How often we will be removing compledted tasks.
    /// </summary>
    private const int TaskTimerTimeOutInMs = 10000;

    /// <summary>
    /// The internal buffer size
    /// </summary>
#if DEBUG
    private const int InternalBufferSize =  8 * 1024;
#else
    private const int InternalBufferSize = 32 * 1024;
#endif

#region Attributes
    /// <summary>
    /// The logger that we will be using to log messages.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// The folder we are watching
    /// </summary>
    protected DirectoryInfo Folder { get; }

    /// <summary>
    /// The system event parser
    /// </summary>
    protected SystemEventsParser EventsParser { get; }
#endregion

#region Member variables
    /// <summary>
    /// The type of folders we are watching
    /// </summary>
    private readonly WatcherTypes _watcherTypes;

    /// <summary>
    /// The actual file watcher.
    /// </summary>
    private RecoveringWatcher _directoryWatcher;

    /// <summary>
    /// The file watcher
    /// </summary>
    private RecoveringWatcher _fileWatcher;

    /// <summary>
    /// All the tasks currently running
    /// </summary>
    private readonly List<Task> _tasks = new List<Task>();

    /// <summary>
    /// The lock so we can add/remove data
    /// </summary>
    private readonly object _lockTasks = new object();

    /// <summary>
    /// The timer lock
    /// </summary>
    private readonly object _lockTimer = new object();

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
    public event FileEventHandler DeletedAsync = delegate { return null; };

    /// <summary>
    ///  Occurs when a file is created.
    /// </summary>
    public event FileEventHandler CreatedAsync = delegate { return null; };

    /// <summary>
    /// Occurs when a file is changed.
    /// </summary>
    public event FileEventHandler ChangedAsync = delegate { return null; };

    /// <summary>
    /// Occurs when a file is renamed.
    /// </summary>
    public event FileEventHandler RenamedAsync = delegate { return null; };

    /// <summary>
    ///     Occurs when the instance of System.IO.FileSystemWatcher is unable to continue
    ///     monitoring changes or when the internal buffer overflows.
    /// </summary>
    public event ErrorEventHandler ErrorAsync = delegate { return null; };
#endregion

    /// <summary>
    /// Constructor to prepare the file watcher.
    /// </summary>
    /// <param name="watcherTypes"></param>
    /// <param name="folder"></param>
    /// <param name="logger"></param>
    /// <param name="parser"></param>
    protected Watcher ( WatcherTypes watcherTypes, DirectoryInfo folder, ILogger logger, SystemEventsParser parser )
    {
      // save the type of folders we are watching.
      _watcherTypes = watcherTypes;

      // the folder being watched.
      Folder = folder ?? throw new ArgumentNullException(nameof(folder));

      // save the logger.
      Logger = logger ?? throw new ArgumentNullException(nameof(logger));

      // save the event parser.
      EventsParser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    #region Task Cleanup Timer
    /// <summary>
    /// Start the cleanup timer
    /// </summary>
    private void StartTasksCleanupTimer()
    {
      if (null != _tasksTimer)
      {
        return;
      }

      lock (_lockTimer)
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
      lock (_lockTasks)
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

      lock (_lockTimer)
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

    #region File/Folder Event
    private void Deleted(IFileSystemEvent e )
    {
      lock (_lockTasks)
      {
        _tasks.Add(DeletedAsync(e));
      }
    }

    private void Created(IFileSystemEvent e)
    {
      lock (_lockTasks)
      {
        _tasks.Add(CreatedAsync(e));
      }
    }

    private void Renamed(IFileSystemEvent e)
    {
      lock (_lockTasks)
      {
        _tasks.Add(RenamedAsync(e));
      }
    }

    private void Changed(IFileSystemEvent e)
    {
      lock (_lockTasks)
      {
        _tasks.Add(ChangedAsync(e));
      }
    }

    private void Error(Exception e)
    {
      lock (_lockTasks)
      {
        _tasks.Add(ErrorAsync(e, _token));
      }
    }
#endregion

    /// <summary>
    /// Stop the folder monitoring.
    /// </summary>
    public void Stop()
    {
      EventsParser?.Stop();

      // stop the cleanup timer
      // we don't need it anymore.
      // we will be cleaning them up below.
      StopTasksCleanupTimer();

      // we can then cancel the watchers
      // we have to do it outside th locks because "Dispose" will flush out
      // the remaining events... and they will need a lock to do that
      // so we might as well get out as soon as posible.
      _fileWatcher?.Stop();

      // stop the directory watcher
      _directoryWatcher?.Stop();

      // stop the registration
      _cancellationTokenRegistration.Dispose();

      // stop allt the tasks.
      StopAndClearAllTasks();
    }

    /// <summary>
    /// Stop all the tasks and clear them all.
    /// </summary>
    private void StopAndClearAllTasks()
    {
      lock (_lockTasks)
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

            helper.Wait.WaitAll(_tasks, Logger, _token);

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

      // start the event parser.
      EventsParser?.Start( _token );

      // start the file watcher
      StartFilesWatcher();

      // start the directory watcher.
      StartDirectoriesWatcher();

      // we can now start monitoring for tasks.
      // so we can remove the completed ones from time to time.
      StartTasksCleanupTimer();
    }

    /// <summary>
    /// Start watching for directory changes
    /// This is slightly different from the files watching.
    /// </summary>
    private void StartDirectoriesWatcher()
    {
      if ((_watcherTypes & WatcherTypes.Directories) != WatcherTypes.Directories)
      {
        return;
      }

      _directoryWatcher = new RecoveringWatcher(Renamed, Changed, Created, Deleted, Error, Folder.FullName, true, InternalBufferSize, Logger);
      _directoryWatcher.Start(_token);
    }

    /// <summary>
    /// Start the files watcher.
    /// </summary>
    private void StartFilesWatcher()
    {
      if ((_watcherTypes & WatcherTypes.Files) != WatcherTypes.Files)
      {
        return;
      }

      // Start the file watcher.
      _fileWatcher = new RecoveringWatcher( Renamed, Changed, Created, Deleted, Error, Folder.FullName, false, InternalBufferSize, Logger );
      _fileWatcher.Start( _token );
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

    /// <summary>
    /// Called when we are cancelling the worker, (it has not being cancelled).
    /// </summary>
    protected abstract void OnCancelling();

    /// <summary>
    /// Called when we have cancelled the work.
    /// </summary>
    protected abstract void OnCancelled();
  }

  internal class RecoveringWatcher
  {
    /// <summary>
    /// The path we will be watching
    /// </summary>
    private readonly string _path;

    /// <summary>
    /// The File watcher internal bufer size
    /// </summary>
    private readonly int _internalBufferSize;

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The action called when a file/folder is renamed
    /// </summary>
    private readonly Action<IFileSystemEvent> _renamed;

    /// <summary>
    /// The action called when a file/folder is changed.
    /// </summary>
    private readonly Action<IFileSystemEvent> _changed;

    /// <summary>
    /// Action called when a file/folder is created
    /// </summary>
    private readonly Action<IFileSystemEvent> _created;

    /// <summary>
    /// Action called when a file/folder is deleted.
    /// </summary>
    private readonly Action<IFileSystemEvent> _deleted;

    /// <summary>
    /// Action called when there is an error.
    /// </summary>
    private readonly Action<Exception> _error;

    /// <summary>
    /// Our token source.
    /// </summary>
    private CancellationTokenSource _tokenSource;

    /// <summary>
    /// Monitor cancellations
    /// </summary>
    private CancellationTokenRegistration _register;
    
    /// <summary>
    /// The currently running task
    /// </summary>
    private Task _task;

    /// <summary>
    /// Are we watching files or folders?
    /// </summary>
    private readonly bool _watchFolder;

    public RecoveringWatcher
    (
      Action<IFileSystemEvent> renamed,
      Action<IFileSystemEvent> changed,
      Action<IFileSystemEvent> created,
      Action<IFileSystemEvent> deleted,
      Action<Exception> error,
      string path, 
      bool watchFolders,
      int internalBufferSize, 
      ILogger logger 
    )
    {
      // watch files or folders?
      _watchFolder = watchFolders;

      // set the actions.
      _renamed = renamed ?? throw new ArgumentNullException(nameof(renamed));
      _changed = changed ?? throw new ArgumentNullException(nameof(changed));
      _created = created ?? throw new ArgumentNullException(nameof(created));
      _deleted = deleted ?? throw new ArgumentNullException(nameof(deleted));

      _error = error ?? throw new ArgumentNullException(nameof(error));

      // the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      // the internal buffer size
      _internalBufferSize = internalBufferSize;

      // set the path
      _path = path ?? throw new ArgumentNullException(nameof(path));
    }

    /// <summary>
    /// Start the file watcher.
    /// </summary>
    public void Start( CancellationToken token )
    {
      // stop
      Stop();

      // register for stop
      _register = new CancellationTokenRegistration();
      if (token.CanBeCanceled)
      {
        _register = token.Register( Stop );
      }

      // create a new token source.
      _tokenSource = new CancellationTokenSource();

      // start the long running thread
      _task = Task.Run(() => MonitorPath(_tokenSource.Token), _tokenSource.Token );
    }

    /// <summary>
    /// Stop the file watching
    /// </summary>
    public void Stop()
    {
      // stop the register. 
      _register.Dispose();

      // ask to cancel
      _tokenSource?.Cancel();

      // if we have a task running...wait for it.
      if (_task != null)
      {
        // and wait for it.
        helper.Wait.WaitAll(_task, _logger);
      }
    }

    private void MonitorPath( CancellationToken token )
    {
      while (!token.IsCancellationRequested)
      {
        Exception lastException = null;
        FileSystemWatcher watcher = null;
        try
        {
          // create the watcher and start monitoring.
          watcher = _watchFolder ? MonitorFolderPath() : MonitorFilePath();

          // we call error ... but we do not care about the return value.
          watcher.Error += (sender, e) => lastException = e.GetException();

          // wait forever ... or until we cancel
          helper.Wait.Until(() => lastException != null || token.IsCancellationRequested);

          // are we here because of an exception?
          if (lastException != null)
          {
            _error(lastException);
            lastException = null;
          }
        }
        catch (OperationCanceledException e)
        {
          // is it our token?
          if (e.CancellationToken != token)
          {
            _logger.Exception(e);
          }

          // we are done.
          return;
        }
        catch (Exception e)
        {
          _logger.Exception(e);
          _error(e);
        }
        finally
        {
          // clean up everything
          if (null != watcher)
          {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
          }
        }
      }
    }

    private FileSystemWatcher MonitorFolderPath()
    {
      // create the file syste, watcher.
      var watcher = new FileSystemWatcher
      {
        Path = _path,
        NotifyFilter = NotifyFilters.DirectoryName,
        Filter = "*.*",
        IncludeSubdirectories = true,
        EnableRaisingEvents = true,
        InternalBufferSize = _internalBufferSize
      };

      watcher.Renamed += (sender, e) => _renamed(new DirectorySystemEvent(e, _logger));
      watcher.Changed += (sender, e) => _changed(new DirectorySystemEvent(e, _logger));
      watcher.Created += (sender, e) => _created(new DirectorySystemEvent(e, _logger));
      watcher.Deleted += (sender, e) => _deleted(new DirectorySystemEvent(e, _logger));

      return watcher;
    }

    private FileSystemWatcher MonitorFilePath()
    {
      // create the file syste, watcher.
      var watcher = new FileSystemWatcher
      {
        Path = _path,
        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
        Filter = "*.*",
        IncludeSubdirectories = true,
        EnableRaisingEvents = true,
        InternalBufferSize = _internalBufferSize
      };

      watcher.Renamed += (sender, e) => _renamed(new FileSystemEvent(e, _logger));
      watcher.Changed += (sender, e) => _changed(new FileSystemEvent(e, _logger));
      watcher.Created += (sender, e) => _created(new FileSystemEvent(e, _logger));
      watcher.Deleted += (sender, e) => _deleted(new FileSystemEvent(e, _logger));

      return watcher;
    }
  }
}
