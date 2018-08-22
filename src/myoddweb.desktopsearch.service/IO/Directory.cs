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
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.helper.IO;
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
    private readonly List<string> _paths;

    /// <summary>
    /// The current ignored paths
    /// </summary>
    private IReadOnlyCollection<DirectoryInfo> _ignorePaths;

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

        _ignorePaths = Paths.GetIgnorePaths(_paths, _logger);
        return _ignorePaths;
      }
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="paths"></param>
    public Directory(ILogger logger, List<string> paths )
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
        // we will be parsing it and the sub-directories.
        return true;
      }

      // we are ignoreing this.
      _logger.Verbose($"Ignoring: {directory.FullName} and sub-directories.");

      // we are not parsing this
      return false;
    }

    /// <inheritdoc />
    public async Task<List<DirectoryInfo>> ParseDirectoriesAsync(DirectoryInfo directory, CancellationToken token)
    {
      try
      {
        // get out if needed.
        token.ThrowIfCancellationRequested();

        // reset what we might have found already
        var directories = await BuildDirectoryListAsync(directory, token).ConfigureAwait(false);

        return directories;
      }
      catch (OperationCanceledException)
      {
        _logger.Warning( "Received cancellation request - directories parsing");
        throw;
      }
    }

    /// <inheritdoc />
    public async Task<List<FileInfo>> ParseDirectoryAsync(DirectoryInfo directory, CancellationToken token)
    {
      try
      {
        // sanity check
        if (!helper.File.CanReadDirectory(directory))
        {
          _logger.Warning($"Cannot Parse Directory: {directory.FullName}");
          return null;
        }

        IEnumerable<FileInfo> files;
        try
        {
          files = directory.EnumerateFiles();
        }
        catch (SecurityException)
        {
          // we cannot access/enumerate this file
          // but we might as well continue
          _logger.Verbose($"Security error while parsing directory: {directory.FullName}.");
          return null;
        }
        catch (UnauthorizedAccessException)
        {
          // we cannot access/enumerate this file
          // but we might as well continue
          _logger.Verbose($"Unauthorized Access while parsing directory: {directory.FullName}.");
          return null;
        }
        catch (Exception e)
        {
          _logger.Error($"Exception while parsing directory: {directory.FullName}. {e.Message}");
          return null;
        }

        var posibleFiles = new HashSet<FileInfo>( new FileInfoComparer() );
        foreach (var file in files)
        {
          if (await Task.Run( () => helper.File.CanReadFile(file), token).ConfigureAwait(false))
          {
            posibleFiles.Add(file);
          }
        }

        // if we found nothing we return null.
        return posibleFiles.Any() ? posibleFiles.ToList() : null;
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - directory parsing");
        throw;
      }
    }

    /// <inheritdoc />
    public bool IsIgnored(DirectoryInfo directory)
    {
      return helper.File.IsSubDirectory(IgnorePaths, directory);
    }

    /// <inheritdoc />
    public bool IsIgnored(FileInfo file)
    {
      // if the directory itself is ignored
      // then the file has to be ignored.
      if (IsIgnored(file.Directory))
      {
        return true;
      }

      // we now need to check if that particular file itslef is ignored.

      // otherwise it is not ignored.
      return false;
    }

    /// <summary>
    /// Recuring parse of the directories.
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<List<DirectoryInfo>> BuildDirectoryListAsync( DirectoryInfo directory, CancellationToken token)
    {
      try
      {
        // create an empty list.
        var directories = new List<DirectoryInfo>();

        // does the caller want us to get in this directory?
        if (!ParseDirectory(directory))
        {
          return directories;
        }

        // we can then add, at least, this directory
        directories.Add(directory);

        var dirs = directory.EnumerateDirectories();
        foreach (var info in dirs)
        {
          // get out if needed.
          token.ThrowIfCancellationRequested();

          // we can parse this directory now and add whatever we found to the list.
          directories.AddRange(await BuildDirectoryListAsync(info, token).ConfigureAwait(false));
        }

        // return what we found
        return directories;
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Building directory list");
        throw;
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

      return new List<DirectoryInfo>();
    }
  }
}
