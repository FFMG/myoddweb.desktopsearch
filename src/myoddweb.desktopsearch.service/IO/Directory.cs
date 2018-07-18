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
using myoddweb.desktopsearch.interfaces.Configs;
using myoddweb.desktopsearch.interfaces.IO;
using ILogger = myoddweb.desktopsearch.interfaces.Logging.ILogger;

namespace myoddweb.desktopsearch.service.IO
{
  internal class Directory : IDirectory
  {
    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The paths information, (ignored paths and so on).
    /// </summary>
    private readonly IPaths _paths;

    /// <summary>
    /// The current ignored paths
    /// </summary>
    private IReadOnlyCollection<DirectoryInfo> _ignorePaths;

    /// <summary>
    /// The list of directories we found.
    /// </summary>
    public List<DirectoryInfo> Directories { get; } = new List<DirectoryInfo>();

    /// <summary>
    /// Getter function to get the ignore paths.
    /// </summary>
    private IEnumerable<DirectoryInfo> IgnorePaths
    {
      get
      {
        if (null != _ignorePaths)
        {
          return _ignorePaths;
        }

        _ignorePaths = helper.IO.Paths.GetIgnorePaths(_paths, _logger);
        return _ignorePaths;
      }
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="paths"></param>
    public Directory(ILogger logger, IPaths paths )
    {
      // The logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      // The path
      _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    /// <summary>
    /// The call back function to check if we are parsing that directory or not.
    /// </summary>
    /// <param name="directory"></param>
    /// <returns></returns>
    private bool ParseDirectory(DirectoryInfo directory)
    {
      if (!helper.File.CanReadDirectory(directory))
      {
        _logger.Warning($"Cannot Parse Directory: {directory.FullName}");
        return false;
      }

      if (!helper.File.IsSubDirectory(IgnorePaths, directory))
      {
        // add this directory to our list.
        Directories.Add(directory);

        // we will be parsing it and the sub-directories.
        return true;
      }

      // we are ignoreing this.
      _logger.Verbose($"Ignoring: {directory.FullName} and sub-directories.");

      // we are not parsing this
      return false;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DirectoryInfo>> ParseDirectoriesAsync(DirectoryInfo directory, CancellationToken token)
    {
      // reset what we might have found already
      Directories.Clear();

      // and rebuild the directory,
      await BuildDirectoryListAsync(directory, token);
      
      // if we cancelled then we return null
      // this will help to force the callers to bail out as well.
      return token.IsCancellationRequested ? null : Directories;
    }

    /// <summary>
    /// Recuring parse of the directories.
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task BuildDirectoryListAsync(DirectoryInfo directory, CancellationToken token)
    {
      try
      {
        // does the caller want us to get in this directory?
        if (!ParseDirectory(directory))
        {
          return;
        }

        var dirs = directory.EnumerateDirectories();
        foreach (var info in dirs)
        {
          // did we get a stop request?
          if (token.IsCancellationRequested)
          {
            return;
          }

          // we can parse this directory now.
          await BuildDirectoryListAsync(info, token).ConfigureAwait(false);
        }
      }
      catch (SecurityException)
      {
        // we cannot access/enumerate this file
        // but we might as well continue
        _logger.Verbose($"Security error while parsing directory: {directory.FullName}.");
      }
      catch (UnauthorizedAccessException)
      {
        // we cannot access/enumerate this file
        // but we might as well continue
        _logger.Verbose($"Unauthorized Access while parsing directory: {directory.FullName}.");
      }
      catch (Exception e)
      {
        _logger.Error($"Exception while parsing directory: {directory.FullName}. {e.Message}");
      }
    }

    /// <inheritdoc />
    public async Task<bool> ParseDirectoryAsync(DirectoryInfo directory, ParseFileAsync actionFile, CancellationToken token)
    {
      IEnumerable<FileSystemInfo> files;
      try
      {
        files = directory.EnumerateFileSystemInfos();
      }
      catch (SecurityException)
      {
        // we cannot access/enumerate this file
        // but we might as well continue
        _logger.Verbose($"Security error while parsing directory: {directory.FullName}.");
        return true;
      }
      catch (UnauthorizedAccessException)
      {
        // we cannot access/enumerate this file
        // but we might as well continue
        _logger.Verbose($"Unauthorized Access while parsing directory: {directory.FullName}.");
        return true;
      }
      catch (Exception e)
      {
        _logger.Error($"Exception while parsing directory: {directory.FullName}. {e.Message}");
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
        tasks.Add(ParseFileActionAsync(actionFile, file, token));
      }

      // then wait for them all
      await Task.WhenAll(tasks).ConfigureAwait(false);

      // return if we did not cancel the request.
      return !token.IsCancellationRequested;
    }

    public async Task ParseFileActionAsync(ParseFileAsync actionFile, FileSystemInfo file, CancellationToken token)
    {
      try
      {
        await actionFile(file, token).ConfigureAwait(false);
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
    }
  }
}
