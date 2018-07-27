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
using myoddweb.desktopsearch.processor.Processors;

namespace myoddweb.desktopsearch.processor
{
  public class Processor
  {
    #region Member variables
    /// <summary>
    /// All the tasks currently running
    /// </summary>
    private readonly List<Task<bool>> _tasks = new List<Task<bool>>();

    /// <summary>
    /// The cancellation source
    /// </summary>
    private CancellationToken _token;

    /// <summary>
    /// When we register a token
    /// </summary>
    private CancellationTokenRegistration _cancellationTokenRegistration;

    /// <summary>
    /// The logger that we will be using to log messages.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The system configuration
    /// </summary>
    private readonly interfaces.Configs.IConfig _config;

    /// <summary>
    /// The lock so we can add/remove data
    /// </summary>
    private readonly object _lock = new object();

    /// <summary>
    /// The timer so we can clear some completed taks.
    /// </summary>
    private System.Timers.Timer _tasksTimer;

    /// <summary>
    /// All the processors.
    /// </summary>
    private readonly List<IProcessor> _processors;
    #endregion

    public Processor(interfaces.Configs.IConfig config, IPersister persister, ILogger logger, IDirectory directory)
    {
      // set the config values.
      _config = config ?? throw new ArgumentNullException(nameof(config));

      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      // Create the various processors, they will not start doing anything just yet
      // or at least, they shouldn't
      _processors = new List<IProcessor>
      {
        new Folders( config.Processors.NumberOfFoldersToProcessPerEvent, persister, logger, directory ),
        new Files( config.Processors.NumberOfFilesToProcessPerEvent, persister, logger )
      };
    }

    #region Events Process Timer
    /// <summary>
    /// Start the event processor timer
    /// </summary>
    private void StartEventsProcessorTimer()
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

        _tasksTimer = new System.Timers.Timer(_config.Timers.EventsProcessorMs)
        {
          AutoReset = false,
          Enabled = true
        };
        _tasksTimer.Elapsed += EventsProcessor;
      }
    }

    /// <summary>
    /// Stop the heartbeats.
    /// </summary>
    private void StopEventsProcessorTimer()
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

    private void EventsProcessor(object sender, ElapsedEventArgs e)
    {
      try
      {
        // stop the timer
        StopEventsProcessorTimer();

        // do the actual work.
        lock (_lock)
        {
          if (_tasks.All(t => t.IsCompleted))
          {
            //  get all the processors to do their work.
            foreach (var processor in _processors)
            {
              _tasks.Add(processor.WorkAsync(_token));
            }
          }

          // remove the completed events.
          _tasks.RemoveAll(t => t.IsCompleted);

        }

      }
      catch (Exception exception)
      {
        _logger.Exception(exception);
      }
      finally
      {
        if (!_token.IsCancellationRequested)
        {
          // restart the timer.
          StartEventsProcessorTimer();
        }
      }
    }

    #region Start/Stop functions
    /// <summary>
    /// Start processor.
    /// </summary>
    public void Start(CancellationToken token)
    {
      // stop what might have already started.
      Stop();

      // start the source.
      _token = token;

      // register the token cancellation
      _cancellationTokenRegistration = _token.Register(TokenCancellation);

      // start the processors
      foreach (var processor in _processors)
      {
        processor.Start();
      }

      // we can start the timers.
      StartEventsProcessorTimer();
    }

    /// <summary>
    /// Stop processor
    /// </summary>
    public void Stop()
    {
      // stop the timer
      StopEventsProcessorTimer();

      try
      {
        //  stop the processors
        foreach (var processor in _processors)
        {
          processor.Stop();
        }

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
    #endregion
  }
}