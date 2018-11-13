using System;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Logging;

namespace myoddweb.desktopsearch.helper
{
  public class AsyncTimer : IDisposable
  {
    /// <summary>
    /// The running task.
    /// </summary>
    private readonly Task _task;

    /// <summary>
    /// The token
    /// </summary>
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();

    /// <summary>
    /// What are we doing?
    /// </summary>
    private readonly string _workDescription;

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;

    public AsyncTimer(Func<Task> work, double intervalMs, ILogger logger, string workDescription = "Async Work") :
      this( work, TimeSpan.FromMilliseconds(intervalMs), logger, workDescription )
    {

    }

    public AsyncTimer(Func<Task> work, TimeSpan interval, ILogger logger, string workDescription = "Async Work")
    {
      _workDescription = workDescription ?? throw new ArgumentNullException(nameof(workDescription));
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      // Start the timer.
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
    /// <param name="work"></param>
    /// <param name="interval"></param>
    /// <returns></returns>
    private async Task WorkAsync(Func<Task> work, TimeSpan interval)
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
        await work();
      }
      catch (OperationCanceledException)
      {
        //  do nothing.
      }
      catch (Exception ex)
      {
        _logger.Exception($"Exception carrying out work: {_workDescription}", ex);
      }
    }
  }
}