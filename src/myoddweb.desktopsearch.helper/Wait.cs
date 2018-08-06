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

namespace myoddweb.desktopsearch.helper
{
  public class Wait
  {
    #region Const variables
    /// <summary>
    /// The number of iterations we want our spinwait to do.
    /// SpinWait essentially puts the processor into a very tight loop, with the loop count specified by the iterations parameter.
    /// The duration of the wait therefore depends on the speed of the processor.
    /// </summary>
    private const int NumberOfIterations = 1024;

    /// <summary>
    /// How many ms we want to sleep in case we only have a single cpu
    /// </summary>
    private const int SingleProcessorSleep = 10;
    #endregion

    private Wait()
    {

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
      var count = 0;
      while (!what())
      {
        token.ThrowIfCancellationRequested();

        if (count > numProcessors && numProcessors > 1 )
        {
          // reset
          count = 0;

          // do some noop operations on our current CPU
          // @see https://msdn.microsoft.com/en-us/library/system.threading.thread.spinwait%28v=vs.110%29.aspx?f=255&MSPPError=-2147217396
          Thread.SpinWait(NumberOfIterations);
          continue;
        }

        if (count == 0)
        {
          // just do a small single yield the first time.
          // if we only have one processor, and no one else is waiting, this will return straight back.
          await Task.Yield();
        }
        else if( numProcessors == 1 )
        {
          // we only have one processor so we don't want to always yield
          // because if we are the only thread then we will yeald straight back to ourselves
          // so we don't want the CPU to run hot on our behalf.
          // so we just sleep a little...
          Thread.Sleep(SingleProcessorSleep);
        }
        else
        {
          // cause a small delay that will yield to other CPUs
          await Task.Delay(count, token );
        }

        // move on.
        ++count;
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
