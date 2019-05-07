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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;
using myoddweb.directorywatcher.interfaces;
using IFileSystemEvent = myoddweb.directorywatcher.interfaces.IFileSystemEvent;

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
    /// The file event processing;
    /// </summary>
    private Task _task;

    /// <summary>
    /// When we register a token
    /// </summary>
    private CancellationTokenRegistration _cancellationTokenRegistration;

    /// <summary>
    /// The folders we are ignoring.
    /// </summary>
    protected IDirectory Directory;

    /// <summary>
    /// The lock so we can add/remove files events
    /// </summary>
    private readonly object _lockEvents = new object();

    /// <summary>
    /// The timer so we can clear some completed taks.
    /// </summary>
    private System.Timers.Timer _tasksTimer;

    /// <summary>
    /// How often we will be remobing compledted tasks.
    /// </summary>
    private readonly int _eventsTimeOutInMs;

    /// <summary>
    /// The maximum amount of time we want to wait for transaction
    /// </summary>
    private readonly int _eventsMaxWaitTransactionMs;

    /// <summary>
    /// The folder persister that will allow us to add/remove folders.
    /// </summary>
    private IPersister Persister { get; }
    #endregion

    protected SystemEventsParser( IPersister persister, IDirectory directory, int eventsParserMs, int eventsMaxWaitTransactionMs, ILogger logger)
    {
      // the directories handler
      Directory = directory ?? throw new ArgumentNullException(nameof(directory));

      if (eventsParserMs <= 0)
      {
        throw new ArgumentException($"The event timeout, ({eventsParserMs}), cannot be zero or negative.");
      }
      _eventsTimeOutInMs = eventsParserMs;

      if (eventsParserMs < 0)
      {
        throw new ArgumentException($"The event max wait for transaction, ({_eventsMaxWaitTransactionMs}), cannot be negative.");
      }
      _eventsMaxWaitTransactionMs = eventsMaxWaitTransactionMs;

      // save the logger
      Logger = logger ?? throw new ArgumentNullException(nameof(logger));

      // save the database
      Persister = persister ?? throw new ArgumentNullException(nameof(persister));
    }

    #region Events Process Timer
    private void StartSystemEventsTimer()
    {
      if (null != _tasksTimer)
      {
        return;
      }

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

    /// <summary>
    /// Rebuild all the system events and remove duplicates.
    /// </summary>
    private IEnumerable<IFileSystemEvent> RebuildSystemEvents()
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
            if (_token.IsCancellationRequested)
            {
              _currentEvents.Clear();
              return null;
            }

            // we will add this event below
            // so we can remove all the previous 'changed' events.
            // that way, the last one in the list will be the most 'up-to-date' ones.
            rebuiltEvents.RemoveAll(e =>
              e.Action == currentEvent.Action &&  e.FullName == currentEvent.FullName);

            // remove the duplicate
            if (currentEvent.Action == EventAction.Touched )
            {
              // we know that this event is changed
              // but if we have some 'created' events for the same file
              // then there is no need to add the changed events.
              if (rebuiltEvents.Any(e => e.Action == EventAction.Added && e.FullName == currentEvent.FullName))
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
        _task = 
        ProcessEventsAsync(_token).ContinueWith( t => 
          {
            if (!_token.IsCancellationRequested && !t.IsCanceled)
            {
              // restart the timer.
              StartSystemEventsTimer();
            }
          }, _token);
      }
      catch (Exception exception)
      {
        Logger.Exception(exception);
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

      if (_tasksTimer == null)
      {
        return;
      }

      _tasksTimer.Enabled = false;
      _tasksTimer.Stop();
      _tasksTimer.Dispose();
      _tasksTimer = null;
    }
    #endregion

    #region Process events

    /// <summary>
    /// This function is called when processing a list of file events.
    /// It should be non-blocking.
    /// </summary>
    /// <param name="token"></param>
    private async Task ProcessEventsAsync(CancellationToken token)
    {
      // quick check before we get the transaction.
      lock (_lockEvents)
      {
        if (!_currentEvents.Any())
        {
          return;
        }
      }

      try
      {
        // we are starting to process events.
        var factory = await Persister.BeginWrite(_eventsMaxWaitTransactionMs, token).ConfigureAwait(false);
        try
        {
          // we now have the lock... so actually get the events.
          var events = RebuildSystemEvents();

          // try and do everything at once.
          // not that we could return null if we have nothing at all to process.
          foreach (var e in events ?? new List<IFileSystemEvent>())
          {
            if (token.IsCancellationRequested)
            {
              break;
            }

            await ProcessEventAsync(factory, e).ConfigureAwait(false);
          }

          Persister.Commit(factory);
        }
        catch (OperationCanceledException e)
        {
          // we cancelled so we need to rollback
          Persister.Rollback(factory);

          // but no need to log it if it is our token.
          if (e.CancellationToken != token)
          {
            // log it
            Logger.Exception(e);
          }
          throw;
        }
        catch (Exception e)
        {
          Persister.Rollback(factory);

          // log it
          Logger.Exception(e);
        }
      }
      catch (TimeoutException)
      {
        // log it
        Logger.Verbose("Timeout while waiting for transaction...");
      }
    }

    /// <summary>
    /// Try and process a created file event.
    /// </summary>
    /// <param name="factory"></param>
    /// <param name="e"></param>
    /// <returns></returns>
    private async Task TryProcessAddedAsync(IConnectionFactory factory, IFileSystemEvent e)
    {
      try
      {
        if (e.Action != EventAction.Added)
        {
          return;
        }

        await ProcessAddedAsync(factory, e, _token).ConfigureAwait(false);
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
    /// <param name="factory"></param>
    /// <param name="e"></param>
    /// <returns></returns>
    private async Task TryProcessRemovedAsync(IConnectionFactory factory, IFileSystemEvent e)
    {
      try
      {
        if (e.Action != EventAction.Removed)
        {
          return;
        }

        await ProcessRemovedAsync(factory, e, _token).ConfigureAwait(false);
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
    /// <param name="factory"></param>
    /// <param name="e"></param>
    /// <returns></returns>
    private async Task TryProcessTouchedAsync(IConnectionFactory factory, IFileSystemEvent e)
    {
      try
      {
        if (e.Action != EventAction.Touched)
        {
          return;
        }

        await ProcessTouchedAsync(factory, e, _token).ConfigureAwait(false);
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
    /// <param name="factory"></param>
    /// <param name="e"></param>
    /// <returns></returns>
    private async Task TryProcessRenamedAsync(IConnectionFactory factory, IFileSystemEvent e)
    {
      try
      {
        if (e.Action != EventAction.Renamed)
        {
          return;
        }

        var rfse = e as IRenamedFileSystemEvent;

        // we have both a new and old name...
        await ProcessRenamedAsync(factory, rfse, _token).ConfigureAwait(false);
      }
      catch
      {
        var rfse = e as IRenamedFileSystemEvent;
        Logger.Error($"There was an error trying to rename {rfse?.FullName ?? "???"} to {rfse?.PreviousFullName ?? "???"}!");
        throw;
      }
    }

    /// <summary>
    /// Process a single event.
    /// </summary>
    /// <param name="factory"></param>
    /// <param name="e"></param>
    /// <returns></returns>
    private async Task ProcessEventAsync( IConnectionFactory factory, IFileSystemEvent e)
    {
      // try process as created.
      await TryProcessAddedAsync(factory, e).ConfigureAwait(false);

      // try process as deleted.
      await TryProcessRemovedAsync(factory, e).ConfigureAwait(false);

      // try process as changed.
      await TryProcessTouchedAsync(factory, e).ConfigureAwait(false);

      // process as a renamed event
      await TryProcessRenamedAsync(factory, e).ConfigureAwait(false);
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

      // stop the token cancellation
      _cancellationTokenRegistration.Dispose();

      // wait for them all to finish
      try
      {
        if (!_task?.IsCompleted ?? false )
        {
          // Log that we are stopping the tasks.
          Logger.Verbose("Waiting for file event task to complete in the File System Events Timer.");

          // then wait...
          helper.Wait.WaitAll(_task, Logger, _token);

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
      catch (Exception e)
      {
        Logger.Exception(e);
        throw;
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
    /// <param name="token"></param>
    public async Task AddAsync(IFileSystemEvent fileSystemEventArgs, CancellationToken token )
    {
      await Task.Run(() =>
      {
        lock (_lockEvents)
        {
          try
          {
            if (_token.IsCancellationRequested)
            {
              return;
            }
            _currentEvents.Add(fileSystemEventArgs);
          }
          catch (Exception e)
          {
            Logger.Exception(e);
          }
        }
      }, token);
    }

    #region Abstract Process events

    /// <summary>
    /// Process a created event
    /// </summary>
    /// <param name="factory"></param>
    /// <param name="fileSystemEvent"></param>
    /// <param name="token"></param>
    protected abstract Task ProcessAddedAsync(IConnectionFactory factory, IFileSystemEvent fileSystemEvent, CancellationToken token);

    /// <summary>
    /// Process a deleted event
    /// </summary>
    /// <param name="factory"></param>
    /// <param name="fileSystemEvent"></param>
    /// <param name="token"></param>
    protected abstract Task ProcessRemovedAsync(IConnectionFactory factory, IFileSystemEvent fileSystemEvent, CancellationToken token);

    /// <summary>
    /// Process a created event
    /// </summary>
    /// <param name="factory"></param>
    /// <param name="fileSystemEvent"></param>
    /// <param name="token"></param>
    protected abstract Task ProcessTouchedAsync(IConnectionFactory factory, IFileSystemEvent fileSystemEvent, CancellationToken token);

    /// <summary>
    /// Process a renamed event
    /// </summary>
    /// <param name="factory"></param>
    /// <param name="fileSystemEvent"></param>
    /// <param name="token"></param>
    protected abstract Task ProcessRenamedAsync(IConnectionFactory factory, IRenamedFileSystemEvent fileSystemEvent, CancellationToken token);
    #endregion
  }
}
