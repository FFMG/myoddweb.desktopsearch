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
using myoddweb.desktopsearch.interfaces.Logging;

namespace myoddweb.desktopsearch.helper.IO
{
  internal class InternalAsyncTimer : IDisposable
  {
    #region Member variables
    /// <summary>
    /// The running task.
    /// </summary>
    private readonly Task _task;

    /// <summary>
    /// The token
    /// </summary>
    private readonly CancellationTokenSource _cts;

    /// <summary>
    /// What are we doing?
    /// </summary>
    private readonly string _workDescription;

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;
    #endregion

    /// <summary>
    /// Return if the task is completed or not.
    /// </summary>
    public bool IsCompleted => _task?.IsCompleted ?? true;

    public InternalAsyncTimer(Func<Task> work, TimeSpan interval, ILogger logger, string workDescription )
    {
      _cts = new CancellationTokenSource();
      _workDescription = workDescription ?? throw new ArgumentNullException(nameof(workDescription));
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      _task = Task.Run(() => WorkAsync(work, interval));
    }

    #region IDisposable
    public void Dispose()
    {
      _cts?.Cancel();
      _task?.Wait();

      _cts?.Dispose();
      _task?.Dispose();
    }
    #endregion

    /// <summary>
    /// Function called when the timer has completed.
    /// </summary>
    /// <param name="workAsync"></param>
    /// <param name="interval"></param>
    /// <returns></returns>
    private async Task WorkAsync(Func<Task> workAsync, TimeSpan interval)
    {
      try
      {
        await Task.Delay(interval, _cts.Token);
      }
      catch (OperationCanceledException)
      {
        return;
      }

      try
      {
        if (_cts.Token.IsCancellationRequested)
        {
          return;
        }
        await workAsync().ConfigureAwait(false);
      }
      catch (OperationCanceledException)
      {
        //  do nothing.
      }
      catch (Exception ex)
      {
        _logger.Exception($"Exception carrying out Async timer work: {_workDescription}", ex);
      }
    }

    public void Stop()
    {
      _cts?.Cancel();
    }
  }
}
