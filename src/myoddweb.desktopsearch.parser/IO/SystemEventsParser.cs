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
  internal abstract class SystemEventsParser
  {
    #region Member variables
    /// <summary>
    /// The logger that we will be using to log messages.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// The list of current/unprocessed events.
    /// </summary>
    private readonly List<FileSystemEventArgs> _currentEvents = new List<FileSystemEventArgs>();

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
    protected IEnumerable<DirectoryInfo> IgnorePaths;

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

    protected SystemEventsParser(IReadOnlyCollection<DirectoryInfo> ignorePaths, int eventsParserMs, ILogger logger)
    {
      // the paths we want to ignore.
      IgnorePaths = ignorePaths ?? throw new ArgumentNullException(nameof(ignorePaths));

      if (eventsParserMs <= 0)
      {
        throw new ArgumentException( $"The event timeout, ({eventsParserMs}), cannot be zero or negative.");
      }
      _eventsTimeOutInMs = eventsParserMs;

      // save the logger
      Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Events Process Timer
    private void StartSystemEventsTimer()
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
        _tasksTimer.Elapsed += SystemEventsProcess;
      }
    }

    /// <summary>
    /// Cleanup all the completed tasks
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void SystemEventsProcess(object sender, ElapsedEventArgs e)
    {
      try
      {
        // stop the timer
        StopSystemEventsTimer();

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
          _tasks.Add(ProcessEventsAsync(events));
        }
      }
      catch (Exception exception)
      {
        Logger.Exception( exception);
      }
      finally
      {
        if (!_token.IsCancellationRequested)
        {
          // restart the timer.
          StartSystemEventsTimer();
        }
      }
    }

    /// <summary>
    /// Stop the heartbeats.
    /// </summary>
    private void StopSystemEventsTimer()
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
    private static bool Is(WatcherChangeTypes types, WatcherChangeTypes type)
    {
      return (types & type) == type;
    }

    /// <summary>
    /// Check if the change type was created.
    /// </summary>
    /// <param name="types"></param>
    /// <returns></returns>
    protected static bool IsCreated(WatcherChangeTypes types)
    {
      return Is(types, WatcherChangeTypes.Created);
    }

    /// <summary>
    /// Check if the change type was deleted.
    /// </summary>
    /// <param name="types"></param>
    /// <returns></returns>
    protected static bool IsDeleted(WatcherChangeTypes types)
    {
      return Is( types, WatcherChangeTypes.Deleted);
    }

    /// <summary>
    /// Check if the change type was changed.
    /// </summary>
    /// <param name="types"></param>
    /// <returns></returns>
    protected static bool IsChanged(WatcherChangeTypes types)
    {
      return Is(types, WatcherChangeTypes.Changed);
    }

    /// <summary>
    /// Check if the change type was renamed.
    /// </summary>
    /// <param name="types"></param>
    /// <returns></returns>
    protected static bool IsRenamed(WatcherChangeTypes types)
    {
      return Is(types, WatcherChangeTypes.Renamed);
    }
    #endregion

    #region Process events
    /// <summary>
    /// This function is called when processing a list of file events.
    /// It should be non-blocking.
    /// </summary>
    /// <param name="events"></param>
    private async Task ProcessEventsAsync(IEnumerable<FileSystemEventArgs> events)
    {
      // assume errors...
      var hadErrors = true;
      try
      {
        // we are starting to process events.
        ProcessEventsStart();

        // try and do everything at once.
        await Task.WhenAll(events.Select(ProcessEventAsync).ToArray()).ConfigureAwait(false);

        // if we are here, then we had no errors
        hadErrors = false;
      }
      catch (Exception e)
      {
        // we had an error
        hadErrors = true;

        // log it
        Logger.Exception(e);
      }
      finally
      {
        // end the work and log if we had any errors.
        ProcessEventsEnd( hadErrors );
      }
    }

    /// <summary>
    /// Process a single event.
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    private async Task ProcessEventAsync(FileSystemEventArgs e)
    {
      if (IsCreated(e.ChangeType))
      {
        await ProcessCreatedAsync(e.FullPath, _token).ConfigureAwait(false);
      }

      if (IsDeleted(e.ChangeType))
      {
        await ProcessDeletedAsync(e.FullPath, _token).ConfigureAwait(false);
      }

      if (IsChanged(e.ChangeType))
      {
        await ProcessChangedAsync(e.FullPath, _token).ConfigureAwait(false);
      }

      if (IsRenamed(e.ChangeType))
      {
        if (e is RenamedEventArgs renameEvent)
        {
          await ProcessRenamedAsync(renameEvent.FullPath, renameEvent.OldFullPath, _token).ConfigureAwait(false);
        }
        else
        {
          Logger.Warning( $"A file, ({e.FullPath}), was marked as renamed, but the event was not.");
          await ProcessChangedAsync(e.FullPath, _token).ConfigureAwait(false);
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

      StartSystemEventsTimer();
    }
    
    /// <summary>
    /// Stop the folder monitoring.
    /// </summary>
    public void Stop()
    {
      // stop the cleanup timer
      // we don't need it anymore.
      StopSystemEventsTimer();

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
            Logger.Verbose($"Waiting for {_tasks.Count} tasks to complete in the File System Events Timer.");

            // then wait...
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
    /// Called when the token has been cancelled.
    /// </summary>
    private void TokenCancellation()
    {
      Logger.Verbose("Stopping Events parser");
      Stop();
      Logger.Verbose("Done events parser");
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

    #region Abstract Process events
    /// <summary>
    /// We are starting to process events
    /// </summary>
    protected abstract void ProcessEventsStart();

    /// <summary>
    /// We finished processing events.
    /// </summary>
    /// <param name="hadErrors">Was there any errors?</param>
    protected abstract void ProcessEventsEnd( bool hadErrors );

    /// <summary>
    /// Process a created event
    /// </summary>
    /// <param name="fullPath"></param>
    /// <param name="token"></param>
    protected abstract Task ProcessCreatedAsync(string fullPath, CancellationToken token);

    /// <summary>
    /// Process a deleted event
    /// </summary>
    /// <param name="fullPath"></param>
    /// <param name="token"></param>
    protected abstract Task ProcessDeletedAsync(string fullPath, CancellationToken token);

    /// <summary>
    /// Process a created event
    /// </summary>
    /// <param name="fullPath"></param>
    /// <param name="token"></param>
    protected abstract Task ProcessChangedAsync(string fullPath, CancellationToken token);

    /// <summary>
    /// Process a renamed event
    /// </summary>
    /// <param name="fullPath"></param>
    /// <param name="oldFullPath"></param>
    /// <param name="token"></param>
    protected abstract Task ProcessRenamedAsync(string fullPath, string oldFullPath, CancellationToken token );
    #endregion
  }
}
