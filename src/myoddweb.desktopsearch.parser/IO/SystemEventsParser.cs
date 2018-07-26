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
using myoddweb.desktopsearch.interfaces.IO;
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
    private readonly List<IFileSystemEvent> _currentEvents = new List<IFileSystemEvent>();

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
    protected IDirectory Directory;

    /// <summary>
    /// The lock so we can add/remove files events
    /// </summary>
    private readonly object _lockEvents = new object();

    /// <summary>
    /// The lock so we can add/remove tasks
    /// </summary>
    private readonly object _lockTasks = new object();

    /// <summary>
    /// The timer so we can clear some completed taks.
    /// </summary>
    private System.Timers.Timer _tasksTimer;

    /// <summary>
    /// How often we will be remobing compledted tasks.
    /// </summary>
    private readonly int _eventsTimeOutInMs;
    #endregion

    protected SystemEventsParser(IDirectory directory, int eventsParserMs, ILogger logger)
    {
      // the directories handler
      Directory = directory ?? throw new ArgumentNullException(nameof(directory));

      if (eventsParserMs <= 0)
      {
        throw new ArgumentException($"The event timeout, ({eventsParserMs}), cannot be zero or negative.");
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

      lock (_lockTasks)
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
    /// Rebuild all the system events and remove duplicates.
    /// </summary>
    public List<IFileSystemEvent> RebuildSystemEvents()
    {
      lock (_lockEvents)
      {
        try
        {
          // ReSharper disable once InconsistentlySynchronizedField
          if (!_currentEvents.Any())
          {
            return null;
          }

          // the re-created list.
          var rebuiltEvents = new List<IFileSystemEvent>();

          // ReSharper disable once InconsistentlySynchronizedField
          foreach (var currentEvent in _currentEvents)
          {
            // we will add this event below
            // so we can remove all the previous 'changed' events.
            // that way, the last one in the list will be the most 'up-to-date' ones.
            rebuiltEvents.RemoveAll(e =>
              e.ChangeType == currentEvent.ChangeType && e.FullName == currentEvent.FullName);

            // remove the duplicate
            if (currentEvent.Is(WatcherChangeTypes.Changed))
            {
              // we know that this event is changed
              // but if we have some 'created' events for the same file
              // then there is no need to add the changed events.
              if (rebuiltEvents.Any(e => e.Is(WatcherChangeTypes.Created) && e.FullName == currentEvent.FullName))
              {
                // Windows sometime flags an item as changed _and_ created.
                // we just need to worry about the created one.
                continue;
              }
            }

            // then add the event
            rebuiltEvents.Add(currentEvent);
          }

          // clear the current values within the lock
          _currentEvents.Clear();

          // if we have noting to return, just return null
          return rebuiltEvents.Any() ? rebuiltEvents : null;
        }
        catch (Exception e)
        {
          //  something in the current events is hurting
          // so we might as well get rid of it here.
          _currentEvents.Clear();

          // log it
          Logger.Exception(e);

          // return nothing.
          return null;
        }
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

        // the events.
        var events = RebuildSystemEvents();
        if (null != events)
        {
          lock (_lockTasks)
          {
            _tasks.Add(ProcessEventsAsync(events));

          // remove the completed events.
            _tasks.RemoveAll(t => t.IsCompleted);
          }
        }
      }
      catch (Exception exception)
      {
        Logger.Exception(exception);
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

      lock (_lockTasks)
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

    #region Process events
    /// <summary>
    /// This function is called when processing a list of file events.
    /// It should be non-blocking.
    /// </summary>
    /// <param name="events"></param>
    private async Task ProcessEventsAsync(IReadOnlyList<IFileSystemEvent> events)
    {
      // assume errors...
      var hadErrors = true;
      try
      {
        // we are starting to process events.
        ProcessEventsStart();

        // try and do everything at once.
        foreach (var e in events)
        {
          await ProcessEventAsync(e).ConfigureAwait(false);
        }

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
        ProcessEventsEnd(hadErrors);
      }
    }

    /// <summary>
    /// Try and process a created file event.
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    private async Task TryProcessCreatedAsync(IFileSystemEvent e)
    {
      try
      {
        if (!e.Is(WatcherChangeTypes.Created))
        {
          return;
        }

        await ProcessCreatedAsync(e, _token).ConfigureAwait(false);
      }
      catch
      {
        Logger.Error($"There was an error trying to process created game event {e.FullName}!");

        // the exception is logged
        throw;
      }
    }

    /// <summary>
    /// Try and process a deleted file event.
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    private async Task TryProcessDeletedAsync(IFileSystemEvent e)
    {
      try
      {
        if (!e.Is(WatcherChangeTypes.Deleted))
        {
          return;
        }

        await ProcessDeletedAsync(e, _token).ConfigureAwait(false);
      }
      catch
      {
        Logger.Error($"There was an error trying to process deleted game event {e.FullName}!");

        // the exception is logged
        throw;
      }
    }

    /// <summary>
    /// Try and process a changed file event.
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    private async Task TryProcessChangedAsync(IFileSystemEvent e)
    {
      try
      {
        if (!e.Is(WatcherChangeTypes.Changed))
        {
          return;
        }

        await ProcessChangedAsync(e, _token).ConfigureAwait(false);
      }
      catch
      {
        Logger.Error($"There was an error trying to process changed game event {e.FullName}!");

        // the exception is logged
        throw;
      }
    }

    /// <summary>
    /// Try and process a changed file event.
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    private async Task TryProcessRenamedAsync(IFileSystemEvent e)
    {
      try
      {
        if (!e.Is(WatcherChangeTypes.Renamed))
        {
          return;
        }

        // we have both a new and old name...
        await ProcessRenamedAsync(e, _token).ConfigureAwait(false);
      }
      catch
      {
        Logger.Error($"There was an error trying to rename {e.OldFullName} to {e.FullName}!");
        throw;
      }
    }

    /// <summary>
    /// Process a single event.
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    private async Task ProcessEventAsync(IFileSystemEvent e)
    {
      // try process as created.
      await TryProcessCreatedAsync(e).ConfigureAwait(false);

      // try process as deleted.
      await TryProcessDeletedAsync(e).ConfigureAwait(false);

      // try process as changed.
      await TryProcessChangedAsync(e).ConfigureAwait(false);

      // process as a renamed event
      await TryProcessRenamedAsync(e).ConfigureAwait(false);
    }
    #endregion

    /// <summary>
    /// Start watching for the folder changes.
    /// </summary>
    public void Start(CancellationToken token)
    {
      // stop what might have already started.
      Stop();

      // start the source.
      _token = token;

      // register the token cancellation
      _cancellationTokenRegistration = _token.Register(TokenCancellation);

      //Start the system events timer.
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

      lock (_lockTasks)
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
    public void Add(IFileSystemEvent fileSystemEventArgs)
    {
      lock (_lockEvents)
      {
        try
        {
          _currentEvents.Add(fileSystemEventArgs);
        }
        catch (Exception e)
        {
          Logger.Exception(e);
        }
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
    protected abstract void ProcessEventsEnd(bool hadErrors);

    /// <summary>
    /// Process a created event
    /// </summary>
    /// <param name="fileSystemEvent"></param>
    /// <param name="token"></param>
    protected abstract Task ProcessCreatedAsync(IFileSystemEvent fileSystemEvent, CancellationToken token);

    /// <summary>
    /// Process a deleted event
    /// </summary>
    /// <param name="fileSystemEvent"></param>
    /// <param name="token"></param>
    protected abstract Task ProcessDeletedAsync(IFileSystemEvent fileSystemEvent, CancellationToken token);

    /// <summary>
    /// Process a created event
    /// </summary>
    /// <param name="fileSystemEvent"></param>
    /// <param name="token"></param>
    protected abstract Task ProcessChangedAsync(IFileSystemEvent fileSystemEvent, CancellationToken token);

    /// <summary>
    /// Process a renamed event
    /// </summary>
    /// <param name="fileSystemEvent"></param>
    /// <param name="token"></param>
    protected abstract Task ProcessRenamedAsync(IFileSystemEvent fileSystemEvent, CancellationToken token);
    #endregion
  }
}
