using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Logging;

namespace myoddweb.desktopsearch.helper
{
  public class AsyncTimer : IDisposable
  {
    #region Member variables
    /// <summary>
    /// The running task.
    /// </summary>
    private readonly List<Task> _tasks = new List<Task>();

    /// <summary>
    /// The token
    /// </summary>
    private CancellationTokenSource _cts;

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
      StartAsync();
    }

    #region IDisposable
    public void Dispose()
    {
      // stop everything and wait
      Stop( true );
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
        _logger.Exception($"Exception carrying out Async timer work: {_workDescription}", ex);
      }
    }

    public void Stop()
    {
      Stop(false);
    }

    public void Stop( bool forceWait )
    { 
      _cts?.Cancel();
      if (forceWait)
      {
        foreach (var task in _tasks)
        {
          task?.Wait();
        }
      }
      _cts?.Dispose();

      foreach (var task in _tasks)
      {
        if (task.IsCompleted)
        {
          task?.Dispose();
        }
      }
      _tasks.RemoveAll( t => t.IsCompleted );

      _cts = null;
    }

    public Task StartAsync()
    {
      Stop();
      
      _cts = new CancellationTokenSource();
      try
      {
        _tasks.Add( Task.Run(() => WorkAsync(_work, _interval)) );
      }
      catch (OperationCanceledException)
      {
        //  do nothing.
      }
      catch (Exception ex)
      {
        _logger.Exception($"Exception carrying out Async timer work: {_workDescription}", ex);
        throw;
      }
      return Task.CompletedTask;
    }
  }
}