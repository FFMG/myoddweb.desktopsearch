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
using System.IO;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Logging;

namespace myoddweb.desktopsearch.service.IO
{
  internal class Directory : IDirectory
  {
    public async Task<bool> ParseDirectoriesAsync(ILogger logger, string path, Func<DirectoryInfo, bool> parseSubDirectory, CancellationToken token)
    {
      return await ParseDirectoryAsync(logger, new DirectoryInfo(path), parseSubDirectory, token).ConfigureAwait(false);
    }

    public async Task<bool> ParseDirectoryAsync(ILogger logger, string path, Action<FileSystemInfo> actionFile, CancellationToken token)
    {
      return await ParseDirectoryAsync(logger, new DirectoryInfo(path), actionFile, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Parse the given directory and sub directories
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="directoryInfo"></param>
    /// <param name="parseSubDirectory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> ParseDirectoryAsync(ILogger logger, DirectoryInfo directoryInfo, Func<DirectoryInfo, bool> parseSubDirectory, CancellationToken token)
    {
      try
      {
        // does the caller want us to get in this directory?
        if (!parseSubDirectory(directoryInfo))
        {
          return true;
        }

        var dirs = directoryInfo.EnumerateDirectories();
        foreach (var info in dirs)
        {
          // did we get a stop request?
          if (token.IsCancellationRequested)
          {
            return false;
          }

          // we can parse this directory now.
          if (!await ParseDirectoryAsync(logger, info, parseSubDirectory, token).ConfigureAwait(false))
          {
            return false;
          }
        }
      }
      catch (SecurityException)
      {
        // we cannot access/enumerate this file
        // but we might as well continue
        logger.Verbose($"Security error while parsing directory: {directoryInfo.FullName}.");
      }
      catch (UnauthorizedAccessException)
      {
        // we cannot access/enumerate this file
        // but we might as well continue
        logger.Verbose($"Unauthorized Access while parsing directory: {directoryInfo.FullName}.");
      }
      catch (Exception e)
      {
        logger.Error($"Exception while parsing directory: {directoryInfo.FullName}. {e.Message}");
      }

      // if we are here, we parsed everything.
      return true;
    }

    /// <summary>
    /// Parse files in a directory
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="directoryInfo"></param>
    /// <param name="actionFile"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> ParseDirectoryAsync(ILogger logger, DirectoryInfo directoryInfo, Action<FileSystemInfo> actionFile, CancellationToken token)
    {
      IEnumerable<FileSystemInfo> files;
      try
      {
        files = directoryInfo.EnumerateFileSystemInfos();
      }
      catch (SecurityException)
      {
        // we cannot access/enumerate this file
        // but we might as well continue
        logger.Verbose($"Security error while parsing directory: {directoryInfo.FullName}.");
        return true;
      }
      catch (UnauthorizedAccessException)
      {
        // we cannot access/enumerate this file
        // but we might as well continue
        logger.Verbose($"Unauthorized Access while parsing directory: {directoryInfo.FullName}.");
        return true;
      }
      catch (Exception e)
      {
        logger.Error($"Exception while parsing directory: {directoryInfo.FullName}. {e.Message}");
        return true;
      }
      var tasks = new List<Task>();
      foreach (var file in files)
      {
        // did we get a stop request?
        if (token.IsCancellationRequested)
        {
          // we cannot just return here
          // as we might have some taks already stated.
          // they all have a token, so they will stop at some point.
          // all we can do is stop adding more to the list.
          break;
        }

        tasks.Add(Task.Run(() =>
        {
          try
          {
            actionFile(file);
          }
          catch (SecurityException)
          {
            // we cannot access/enumerate this file
            // but we might as well continue
            logger.Verbose($"Security error while parsing file: {file.FullName}.");
          }
          catch (UnauthorizedAccessException)
          {
            // we cannot access/enumerate this file
            // but we might as well continue
            logger.Verbose($"Unauthorized Access while parsing file: {file.FullName}.");
          }
          catch (Exception e)
          {
            logger.Error($"Exception while parsing file: {file.FullName}. {e.Message}");
          }
        }, token));
      }

      // then wait for them all
      await Task.WhenAll(tasks).ConfigureAwait(false);

      // return if we did not cancel the request.
      return !token.IsCancellationRequested;
    }
  }
}
