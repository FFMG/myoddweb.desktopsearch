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
    private const int NumberOfIterations = 1024;

    private const int SingleProcessorSleep = 10;

    private Wait()
    {

    }

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

          // cause a small number of Noop operations to all the CPUs
          Thread.SpinWait(NumberOfIterations);
          continue;
        }

        if (count == 0)
        {
          // just do a small single yield the first time.
          await Task.Yield();
        }
        else if( numProcessors == 1 )
        {
          // we only have one processor so we don't want to yield
          // because if we are the only thread then we will yeald straight back to ourselves
          // so we don't want the CPU to run hot on our behalf.
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

    public static async Task UntilAsync(Func<bool> what, CancellationToken token = default(CancellationToken) )
    {
      if (token.IsCancellationRequested)
      {
        return;
      }

      if (what())
      {
        return;
      }
      await UntilAsync(what, Environment.ProcessorCount, token ).ConfigureAwait( false );
    }
  }
}
