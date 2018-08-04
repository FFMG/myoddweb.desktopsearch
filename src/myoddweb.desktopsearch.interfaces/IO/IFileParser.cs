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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Logging;

namespace myoddweb.desktopsearch.interfaces.IO
{
  public interface IFileParser
  {
    /// <summary>
    /// The name of the parser.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The list of extensions we aim to support
    /// </summary>
    string[] Extenstions { get; }

    /// <summary>
    /// Check if the given file is supported.
    /// Return true if we will parse it or not.
    /// </summary>
    bool Supported(FileInfo file);

    /// <summary>
    /// Parse a single file and return a list of words.
    /// Return null if the file is not supported.
    /// </summary>
    /// <param name="file"></param>
    /// <param name="logger"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<Words> ParseAsync(FileInfo file, ILogger logger, CancellationToken token);
  }
}