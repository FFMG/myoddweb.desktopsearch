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
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.helper;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.processor
{
  internal class ProcessorTimer
  {
    #region Member variables
    /// <summary>
    /// The processor we are currently running.
    /// </summary>
    private readonly IList<IProcessor> _processors;

    /// <summary>
    /// The persister.
    /// </summary>
    private readonly IPersister _persister;

    /// <summary>
    /// The timer to start the next round.
    /// </summary>
    private AsyncTimer _timer;

    /// <summary>
    /// The cancellation
    /// </summary>
    private CancellationToken _token;

    /// <summary>
    /// When the file/folders updates are compelte
    /// the is the amount of time we want to wait
    /// before we check again...
    /// </summary>
    private int EventsProcessorMs { get; }

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;
    #endregion

    public ProcessorTimer( IPersister persister, IList<IProcessor> processors, ILogger logger, int eventsProcessorMs )
    {
      EventsProcessorMs = eventsProcessorMs;
      if (EventsProcessorMs <= 0)
      {
        throw new ArgumentException( $"The quiet event processor timer cannot be -ve or zeor ({EventsProcessorMs})");
      }
      _processors = processors ?? throw new ArgumentNullException(nameof(processor));
      if (_processors.Count == 0)
      {
        throw new ArgumentException("You must have at least one item to process!");
      }
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      _persister = persister ?? throw new ArgumentNullException(nameof(persister));
    }

    #region Events Process Timer
    /// <summary>
    /// Start the event processor timer
    /// </summary>
    private void StartProcessorTimer( double interval)
    {
      if (null != _timer)
      {
        return;
      }

      _timer = new AsyncTimer(EventsProcessorAsync, interval, _logger, "Events Processor");
    }

    /// <summary>
    /// The event called when the timer fires.
    /// </summary>
    private async Task EventsProcessorAsync()
    {
      // process the item ... and when done
      // restart the timer.
      var factory = await _persister.BeginWrite(_token).ConfigureAwait(false);

      var tasks = new List<Task<int>>();
      foreach (var processor in _processors)
      {
        tasks.Add( processor.WorkAsync(factory, _token) );
      }
      
      try
      {
        // wait for everything
        await Wait.WhenAll(tasks, _logger, _token).ConfigureAwait(false);

        // and complete the work.
        _persister.Commit(factory);
      }
      catch (OperationCanceledException)
      {
        factory.Rollback();
        _logger.Warning("Received cancellation request - Processor timer.");
        throw;
      }
      catch (Exception e)
      {
        factory.Rollback();
        _logger.Exception(e);
      }
      
      // restart the timer ...
      StartProcessorTimer(EventsProcessorMs);
    }

    /// <summary>
    /// Stop the heartbeats.
    /// </summary>
    private void StopProcessorTimer()
    {
      if (_timer == null)
      {
        return;
      }

      _timer.Dispose();
      _timer = null;
    }
    #endregion

    public void Start(CancellationToken token)
    {
      // set the token
      _token = token;

      // start the timer using the 'quiet' delays.
      StartProcessorTimer( EventsProcessorMs );
    }

    public void Stop()
    {
      try
      {
        // Stop the timers
        StopProcessorTimer();

        // stop the processors
        foreach (var processor in _processors)
        {
          processor.Stop();
        }
      }
      catch (Exception e)
      {
        _logger.Exception(e);
      }
    }
  }
}
