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
    /// The directories we found.
    /// </summary>
    public List<DirectoryInfo> Directories { get; } = new List<DirectoryInfo>();
    #endregion

    public DirectoriesParser(DirectoryInfo startFolder, ILogger logger, IDirectory directory)
    {
      // save the start folder.
      _startFolder = startFolder ?? throw new ArgumentNullException(nameof(startFolder));

      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      // save the directory parser
      _directory = directory ?? throw new ArgumentNullException(nameof(directory));
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
      if (await _directory.ParseDirectoriesAsync(_startFolder, token).ConfigureAwait(false))
      {
        return true;
      }

      _logger.Warning("The parsing was cancelled");
      return false;
    }
  }
}
