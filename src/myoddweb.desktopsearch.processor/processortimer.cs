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
    private System.Timers.Timer _timer;

    /// <summary>
    /// The cancellation
    /// </summary>
    private CancellationToken _token;

    /// <summary>
    /// When the file/folders updates are compelte
    /// the is the amount of time we want to wait
    /// before we check again...
    /// </summary>
    private int QuietEventsProcessorMs { get; }

    /// <summary>
    /// If we still have files/folders/... to check
    /// this is the amount of time between processing we want to wai.t
    /// </summary>
    private int BusyEventsProcessorMs { get; }

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;
    #endregion

    public ProcessorTimer( IPersister persister, IList<IProcessor> processors, ILogger logger, int quietEventsProcessorMs, int busyEventsProcessorMs)
    {
      QuietEventsProcessorMs = quietEventsProcessorMs;
      BusyEventsProcessorMs = busyEventsProcessorMs;
      if (QuietEventsProcessorMs <= 0)
      {
        throw new ArgumentException( $"The quiet event processor timer cannot be -ve or zeor ({QuietEventsProcessorMs})");
      }
      if (BusyEventsProcessorMs <= 0)
      {
        throw new ArgumentException($"The busy event processor timmer cannot be -ve or zeor ({BusyEventsProcessorMs})");
      }
      if (BusyEventsProcessorMs > QuietEventsProcessorMs )
      {
        throw new ArgumentException($"The busy event processor timer cannot be more than the quiet event processor timer ({QuietEventsProcessorMs} < {BusyEventsProcessorMs})");
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

      _timer = new System.Timers.Timer(interval)
      {
        AutoReset = false,
        Enabled = true
      };
      _timer.Elapsed += EventsProcessor;
    }

    /// <summary>
    /// The event called when the timer fires.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void EventsProcessor(object sender, ElapsedEventArgs e)
    {
      // stop the timer in case it is running
      StopProcessorTimer();

      // process the item ... and when done
      // restart the timer.
      CreateTask().GetAwaiter().GetResult();
    }

    private async Task CreateTask()
    { 
      var factory = await _persister.BeginWrite(_token).ConfigureAwait(false);

      var tasks = new List<Task<int>>();
      foreach (var processor in _processors)
      {
        tasks.Add( processor.WorkAsync(factory, _token) );
      }

      await helper.Wait.WhenAll(tasks, _logger, _token).
      ContinueWith(
        task =>
        {
          // if the operation was cancelled, no point going further.
          if (task.IsCanceled)
          {
            factory.Rollback();
            _logger.Warning("Received cancellation request - Processor timer.");
            throw new OperationCanceledException(_token);
          }

          // if it was faulted, just log the error.
          // and restart the timer again.
          if (task.IsFaulted)
          {
            factory.Rollback();
            _logger.Exception(task.Exception ?? new Exception("There was an exception durring EventsProcessor handling"));

            // restart the timer ... quietly.
            StartProcessorTimer( Almost(QuietEventsProcessorMs) );
          }
          else
          {
            factory.Commit();

            var totalMaxUpdatesToProcess = _processors.Sum(p => p.MaxUpdatesToProcess);
            var processed = task.GetAwaiter().GetResult().Sum();

            // restart the timer.
            StartProcessorTimer( Almost(processed < totalMaxUpdatesToProcess ? QuietEventsProcessorMs : BusyEventsProcessorMs));
          }

        }, _token
      ).ConfigureAwait(false);
    }

    /// <summary>
    /// Given a static time, this function tries to randomize it a little.
    /// This helps to prevent posible collisions.
    /// </summary>
    /// <param name="given"></param>
    /// <returns></returns>
    private static double Almost(int given)
    {
      // get a random number
      var rnd = new Random( DateTime.UtcNow.Millisecond );

      // and get it withing +/- 10% of the actual value.
      // we add 20% so that we are within -10% and +10% of the actual value...
      // we use ceiling to make sure we never get zero...
      return (given * .9) + (rnd.NextDouble() * ( given * 0.2));
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

      _timer.Enabled = false;
      _timer.Stop();
      _timer.Dispose();
      _timer = null;
    }
    #endregion

    public void Start(CancellationToken token)
    {
      // set the token
      _token = token;

      // start the timer using the 'quiet' delays.
      StartProcessorTimer( QuietEventsProcessorMs );
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
