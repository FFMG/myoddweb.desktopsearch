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
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Logging;

namespace myoddweb.desktopsearch.parser.IO
{
  /// <summary>
  /// Class to parse a given directory looking for sub directories.
  /// </summary>
  internal class DirectoriesParser
  {
    #region Member variables
    /// <summary>
    /// The directory parser we will be using.
    /// </summary>
    private readonly IDirectory _directory;

    /// <summary>
    /// The logger that we will be using to log messages.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The folder we will be parsing
    /// </summary>
    private readonly DirectoryInfo _startFolder;

    /// <summary>
    /// The folders we are ignoring.
    /// </summary>
    private readonly IEnumerable<DirectoryInfo> _ignorePaths;

    /// <summary>
    /// The directories we found.
    /// </summary>
    public List<DirectoryInfo> Directories { get; } = new List<DirectoryInfo>();
    #endregion

    public DirectoriesParser(DirectoryInfo startFolder, IReadOnlyCollection<DirectoryInfo> ignorePaths, ILogger logger, IDirectory directory)
    {
      // save the start folder.
      _startFolder = startFolder ?? throw new ArgumentNullException(nameof(startFolder));

      // the paths we want to ignore.
      _ignorePaths = ignorePaths ?? throw new ArgumentNullException(nameof(ignorePaths));

      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      // save the directory parser
      _directory = directory ?? throw new ArgumentNullException(nameof(directory));
    }

    /// <summary>
    /// The call back function to check if we are parsing that directory or not.
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private Task<bool> ParseDirectory(DirectoryInfo directory, CancellationToken token )
    {
      if (!Helper.File.CanReadDirectory(directory))
      {
        _logger.Warning($"Cannot Parse Directory: {directory.FullName}");
        return Task.FromResult(false);
      }

      if (!Helper.File.IsSubDirectory(_ignorePaths, directory))
      {
        // add this directory to our list.
        Directories.Add(directory);

        // we will be parsing it and the sub-directories.
        return Task.FromResult(true);
      }

      // we are ignoreing this.
      _logger.Verbose($"Ignoring: {directory.FullName} and sub-directories.");

      // we are not parsing this
      return Task.FromResult(false);
    }

    /// <summary>
    /// Search the directory
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<bool> SearchAsync(CancellationToken token)
    {
      Directories.Clear();

      // parse the directory
      if (await _directory.ParseDirectoriesAsync(_startFolder, ParseDirectory, token).ConfigureAwait(false))
      {
        return true;
      }

      _logger.Warning("The parsing was cancelled");
      return false;
    }
  }
}
