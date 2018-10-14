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
using myoddweb.desktopsearch.interfaces.Logging;

namespace myoddweb.desktopsearch.helper
{
  public class Wait
  {
    #region Const variables
    /// <summary>
    /// How many ms we want to sleep in case we only have a single cpu
    /// </summary>
    private const int SingleProcessorSleep = 10;

    /// <summary>
    /// How many ms we want to slee for multiple processors.
    /// </summary>
    private const int MultipleProcessorsSleep = 2;

    /// <summary>
    /// The maximum amount of time we are prepared to delay.
    /// </summary>
    private const int MaxDelayPerThread = 100;

    /// <summary>
    /// The number of counters currently waiting.
    /// </summary>
    private static int _waitersUntilCount;

    /// <summary>
    /// The object lock.
    /// </summary>
    private static object _lock = new object();
    #endregion

    private Wait()
    {

    }

    public static async Task<T[]> WhenAll<T>(IReadOnlyCollection<Task<T>> tasks, ILogger logger, CancellationToken token = default(CancellationToken))
    {
      var task = Task.WhenAll(tasks.ToArray());
      T[] results = null;
      try
      {
        results = await task.ConfigureAwait(false);
      }
      catch
      {
        // ignore the error as we will get it from task
      }

      if (task.Exception != null)
      {
        LogAggregateException(task.Exception, logger, token);
      }

      return results;
    }

    public static void WaitAll(Task task, ILogger logger, CancellationToken token = default(CancellationToken))
    {
      WaitAll(new []{task}, logger, token);
    }

    public static void WaitAll(IReadOnlyCollection<Task> tasks, ILogger logger, CancellationToken token = default(CancellationToken) )
    {
      try
      {
        Task.WaitAll(tasks.ToArray());
      }
      catch (AggregateException ae)
      {
        LogAggregateException(ae, logger, token );
      }
      catch (Exception e)
      {
        logger.Exception(e);
      }
    }

    /// <summary>
    /// Log an agregate exception
    /// </summary>
    /// <param name="ae"></param>
    /// <param name="logger"></param>
    /// <param name="token"></param>
    internal static void LogAggregateException(AggregateException ae, ILogger logger, CancellationToken token )
    {
      ae.Handle(e =>
      {
        switch (e)
        {
          case OperationCanceledException oc when oc.CancellationToken == token:
            // just log the message.
            logger.Warning("Received cancellation request - Waiting for task to complete.");

            // we handled it, no error to log, it was just a cancellation request.
            return true;

          case TaskCanceledException tc when tc.CancellationToken == token:
            // just log the message.
            logger.Warning("Received cancellation request - Waiting for task to complete.");

            // we handled it, no error to log, it was just a cancellation request.
            return true;
        }

        // otherwise just log it.
        logger.Exception(e);

        // we handled it.
        return true;
      });
    }

    /// <summary>
    /// What until a function complete.
    /// </summary>
    /// <param name="what"></param>
    /// <param name="numProcessors"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private static async Task UntilAsync(Func<bool> what, int numProcessors, CancellationToken token)
    {
      // increment the number of items.
      try
      {
        lock (_lock)
        {
          ++_waitersUntilCount;
        }

        // start number of spin waits.
        var spinWait = numProcessors;
        var count = 0;
        while (!what())
        {
          token.ThrowIfCancellationRequested();

          if (count > numProcessors && numProcessors > 1)
          {
            // reset
            count = 0;

            // The number of iterations we want our spinwait to do.
            // SpinWait essentially puts the processor into a very tight loop, with the loop count specified by the iterations parameter.
            // The duration of the wait therefore depends on the speed of the processor.
            //
            // do some noop operations on our current CPU
            // @see https://msdn.microsoft.com/en-us/library/system.threading.thread.spinwait%28v=vs.110%29.aspx?f=255&MSPPError=-2147217396
            Thread.SpinWait(spinWait);
            spinWait = spinWait > 1024 ? numProcessors : spinWait * _waitersUntilCount;
            continue;
          }

          if (count == 0)
          {
            // just do a small single yield the first time.
            // if we only have one processor, and no one else is waiting, this will return straight back.
            await Task.Yield();
          }
          else if (numProcessors == 1)
          {
            // we only have one processor so we don't want to always yield
            // because if we are the only thread then we will yeald straight back to ourselves
            // so we don't want the CPU to run hot on our behalf.
            // so we just sleep a little...
            Thread.Sleep(SingleProcessorSleep);
          }
          else
          {
            // the more 'waiters' we have, the more starved or resources we will become
            // but we also don't want to wait forever.
            // the value of _waitersUntilCount can never be '0' because there is at least one, (us).
            var delay = _waitersUntilCount * (numProcessors == 1 ? SingleProcessorSleep : MultipleProcessorsSleep);

            // make sure that the value is within range.
            delay = delay > MaxDelayPerThread ? MaxDelayPerThread : delay;

            // cause a small delay that will yield to other CPUs
            await Task.Delay(count * delay, token);
          }

          // move on.
          ++count;
        }
      }
      finally
      {
        lock (_lock)
        {
          --_waitersUntilCount;
        }
      }
    }

    /// <summary>
    /// Wait until a function completes.
    /// </summary>
    /// <param name="what"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public static async Task UntilAsync(Func<bool> what, CancellationToken token = default(CancellationToken) )
    {
      // already cancelled?
      if (token.IsCancellationRequested)
      {
        return;
      }

      // no need to fire a new thread if this is already true 
      if ( what() )
      {
        return;
      }

      // do the actual waiting for the event.
      await UntilAsync(what, Environment.ProcessorCount, token ).ConfigureAwait( false );
    }

    /// <summary>
    /// The non async equivalent of UntilAsync
    /// </summary>
    /// <param name="what"></param>
    public static void Until(Func<bool> what)
    {
      UntilAsync(what).Wait();
    }
  }
}
