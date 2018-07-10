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

namespace myoddweb.desktopsearch.parser
{
  internal class DirectoriesParser
  {
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
    private readonly string _startFolder;

    public List<DirectoryInfo> Directories { get; }

    public DirectoriesParser( string startFolder, ILogger logger, IDirectory directory)
    {
      // save the start folde.r
      _startFolder = startFolder ?? throw new ArgumentNullException(nameof(startFolder));

      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      // save the directory parser
      _directory = directory ?? throw new ArgumentNullException(nameof(directory));

      Directories = new List<DirectoryInfo>();
    }

    /// <summary>
    /// The call back function to check if we are parsing that directory or not.
    /// </summary>
    /// <param name="directory"></param>
    /// <returns></returns>
    private Task<bool> ParseDirectory(DirectoryInfo directory)
    {
      if (!Helper.File.CanReadDirectory(directory))
      {
        _logger.Warning($"Cannot Parse Directory: {directory.FullName}");
        return Task.FromResult(false);
      }

      // add this directory to our list.
      Directories.Add(directory);

      // we will be parsing it.
      return Task.FromResult(true);
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
