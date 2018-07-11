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
    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;

    public Directory(ILogger logger)
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> ParseDirectoriesAsync(DirectoryInfo directoryInfo, Func<DirectoryInfo, Task<bool>> parseSubDirectory, CancellationToken token)
    {
      try
      {
        // does the caller want us to get in this directory?
        if (!await parseSubDirectory(directoryInfo).ConfigureAwait(false))
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
          if (!await ParseDirectoriesAsync(info, parseSubDirectory, token).ConfigureAwait(false))
          {
            return false;
          }
        }
      }
      catch (SecurityException)
      {
        // we cannot access/enumerate this file
        // but we might as well continue
        _logger.Verbose($"Security error while parsing directory: {directoryInfo.FullName}.");
      }
      catch (UnauthorizedAccessException)
      {
        // we cannot access/enumerate this file
        // but we might as well continue
        _logger.Verbose($"Unauthorized Access while parsing directory: {directoryInfo.FullName}.");
      }
      catch (Exception e)
      {
        _logger.Error($"Exception while parsing directory: {directoryInfo.FullName}. {e.Message}");
      }

      // if we are here, we parsed everything.
      return true;
    }

    /// <inheritdoc />
    public async Task<bool> ParseDirectoryAsync(DirectoryInfo directoryInfo, Func<FileSystemInfo, Task> actionFile, CancellationToken token)
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
        _logger.Verbose($"Security error while parsing directory: {directoryInfo.FullName}.");
        return true;
      }
      catch (UnauthorizedAccessException)
      {
        // we cannot access/enumerate this file
        // but we might as well continue
        _logger.Verbose($"Unauthorized Access while parsing directory: {directoryInfo.FullName}.");
        return true;
      }
      catch (Exception e)
      {
        _logger.Error($"Exception while parsing directory: {directoryInfo.FullName}. {e.Message}");
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

        tasks.Add(Task.Run(async () =>
        {
          try
          {
            await actionFile(file).ConfigureAwait(false);
          }
          catch (SecurityException)
          {
            // we cannot access/enumerate this file
            // but we might as well continue
            _logger.Verbose($"Security error while parsing file: {file.FullName}.");
          }
          catch (UnauthorizedAccessException)
          {
            // we cannot access/enumerate this file
            // but we might as well continue
            _logger.Verbose($"Unauthorized Access while parsing file: {file.FullName}.");
          }
          catch (Exception e)
          {
            _logger.Error($"Exception while parsing file: {file.FullName}. {e.Message}");
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
