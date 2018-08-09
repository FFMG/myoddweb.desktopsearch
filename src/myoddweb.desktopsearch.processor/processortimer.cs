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
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace myoddweb.desktopsearch.processor
{
  internal class ProcessorTimer
  {
    /// <summary>
    /// Very short delay when work was picked up
    /// </summary>
    private const int BusyEventsProcessorMs = 1;

    /// <summary>
    /// Much bigger delay for when we want to throlle back
    /// </summary>
    private const int QuietEventsProcessorMs = 1000;


    #region Member variables
    /// <summary>
    /// The processor we are currently running.
    /// </summary>
    private readonly IProcessor _processor;

    /// <summary>
    /// The timer to start the next round.
    /// </summary>
    private System.Timers.Timer _timer;

    /// <summary>
    /// The cancellation
    /// </summary>
    private CancellationToken _token;

    /// <summary>
    /// The current task.
    /// </summary>
    private Task _task;
    #endregion

    public ProcessorTimer(IProcessor processor)
    {
      _processor = processor ?? throw new ArgumentNullException(nameof(processor));
    }

    #region Events Process Timer
    /// <summary>
    /// Start the event processor timer
    /// </summary>
    private void StartProcessorTimer( int interval)
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
      _task = _processor.WorkAsync(_token).
        ContinueWith( 
          task => StartProcessorTimer( task.Result < _processor.MaxUpdatesToProcess ? QuietEventsProcessorMs : BusyEventsProcessorMs ), _token 
        );
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

        // and wait for the task to complete.
        _task?.Wait( _token );
      }
      catch (OperationCanceledException e)
      {
        if (e.CancellationToken != _token)
        {
          // not my item.
          throw;
        }
      }
    }
  }
}
