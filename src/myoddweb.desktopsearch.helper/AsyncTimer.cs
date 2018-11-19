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
using System.Threading.Tasks;
using myoddweb.desktopsearch.helper.IO;
using myoddweb.desktopsearch.interfaces.Logging;

namespace myoddweb.desktopsearch.helper
{
  public class AsyncTimer : IDisposable
  {
    #region Member variables
    /// <summary>
    /// The running task.
    /// </summary>
    private readonly List<InternalAsyncTimer> _tasks = new List<InternalAsyncTimer>();

    /// <summary>
    /// What are we doing?
    /// </summary>
    private readonly string _workDescription;

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The function we will call.
    /// </summary>
    private readonly Func<Task> _work;

    /// <summary>
    /// The interval
    /// </summary>
    private readonly TimeSpan _interval;
    #endregion

    public AsyncTimer(Func<Task> work, double intervalMs, ILogger logger, string workDescription = "Async Work") :
      this( work, TimeSpan.FromMilliseconds(intervalMs), logger, workDescription )
    {

    }

    public AsyncTimer(Func<Task> work, TimeSpan interval, ILogger logger, string workDescription = "Async Work")
    {
      _workDescription = workDescription ?? throw new ArgumentNullException(nameof(workDescription));
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      _interval = interval;
      _work = work;

      // Start the timer.
      StartAsync().GetAwaiter().GetResult();
    }

    #region IDisposable
    public void Dispose()
    {
      // stop everything and wait
      StopAsync( true ).GetAwaiter().GetResult();
    }
    #endregion

    /// <summary>
    /// Stop the current task, (if any)
    /// But we do not wait for it to complete
    /// </summary>
    /// <returns></returns>
    public Task StopAsync()
    {
      // stop ... but don't wait.
      return StopAsync(false);
    }

    /// <summary>
    /// Stop the tasks currently running and optionally, wait.
    /// </summary>
    /// <param name="forceWait"></param>
    /// <returns></returns>
    private async Task StopAsync( bool forceWait )
    {
      //  stop everything
      foreach (var task in _tasks.Where( t => !t.IsCompleted))
      {
        task.Stop();
      }

      //  do we want to wait?
      if (forceWait)
      {
        await Wait.UntilAsync(() => _tasks.Any(t => t.IsCompleted));
      }

      // house keeping.
      _tasks.RemoveAll( t => t.IsCompleted );
    }

    public async Task StartAsync()
    {
      // stop whatever is running now.
      await StopAsync(false).ConfigureAwait(false);

      // start a new task
      _tasks.Add( new InternalAsyncTimer(_work, _interval, _logger, _workDescription ));
    }
  }
}