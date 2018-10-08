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
using myoddweb.directorywatcher.interfaces;
using IFileSystemEvent = myoddweb.desktopsearch.interfaces.IO.IFileSystemEvent;

namespace myoddweb.desktopsearch.parser.IO
{
  public delegate Task FileEventHandler(IFileSystemEvent e );

  public delegate Task ErrorEventHandler(Exception e, CancellationToken token);

  internal abstract class Watcher
  {
    /// <summary>
    /// Event when a FileSystem event was 'touched'.
    /// Changed attribute, size changed etc...
    /// </summary>
    event WatcherEvent<IFileSystemEvent> OnTouchedAsync;

    /// <summary>
    /// Event when a FileSystem event was added.
    /// </summary>
    event WatcherEvent<IFileSystemEvent> OnAddedAsync;

    /// <summary>
    /// Event when a FileSystem event was Removed.
    /// </summary>
    event WatcherEvent<IFileSystemEvent> OnRemovedAsync;

    /// <summary>
    /// Event when a FileSystem event was Renamed.
    /// </summary>
    event WatcherEvent<IRenamedFileSystemEvent> OnRenamedAsync;

    /// <summary>
    /// There was an error.
    /// </summary>
    event WatcherEvent<IEventError> OnErrorAsync;

    /// <summary>
    /// How often we will be removing compledted tasks.
    /// </summary>
    private const int TaskTimerTimeOutInMs = 10000;

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

      // _directoryWatcher = RecoveringWatcher.StartFolderWatcher(Renamed, Changed, Created, Deleted, Error, Folder.FullName, Logger, _token );
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
//      _fileWatcher = RecoveringWatcher.StartFileWatcher( Renamed, Changed, Created, Deleted, Error, Folder.FullName, Logger, _token );
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
}
