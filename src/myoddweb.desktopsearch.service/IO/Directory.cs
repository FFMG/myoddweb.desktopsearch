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
using System.IO;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.IO;

namespace myoddweb.desktopsearch.service.IO
{
  internal class Directory : IDirectory
  {
    public async Task<bool> ParseAsync(string path, Action<FileInfo> actionFile, Func<DirectoryInfo, bool> parseSubDirectory, CancellationToken token)
    {
      return await ParseAsync(new DirectoryInfo(path), actionFile, parseSubDirectory, token).ConfigureAwait(false);
    }

    private async Task<bool> ParseAsync(DirectoryInfo directoryInfo, Action<FileInfo> actionFile, Func<DirectoryInfo, bool> parseSubDirectory, CancellationToken token)
    {
      try
      {
        var dirs = directoryInfo.EnumerateDirectories();
        foreach (var info in dirs)
        {
          // did we get a stop request?
          if (token.IsCancellationRequested)
          {
            return false;
          }

          try
          {
            // does the caller want us to get in this directory?
            if (!parseSubDirectory(info))
            {
              continue;
            }

            if (!await ParseAsync(info, actionFile, parseSubDirectory, token).ConfigureAwait(false))
            {
              return false;
            }
          }
          catch (Exception e)
          {
            Console.WriteLine(e);
            throw;
          }
        }

        // if we are here, we parsed everything.
        return true;
      }
      catch (SecurityException)
      {
        // we cannot access/enumerate this
        // but we might as well continue
        return true;
      }
      catch (UnauthorizedAccessException)
      {
        // we cannot access/enumerate this
        // but we might as well continue
        return true;
      }
    }
  }
}
