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

namespace myoddweb.desktopsearch.interfaces.Configs
{
  public interface IIgnoreFile
  {
    /// <summary>
    /// The pattern of the file being ignored.
    /// </summary>
    string Pattern { get; }

    /// <summary>
    /// The maximum size of the file in megabytes.
    /// </summary>
    long MaxSizeMegabytes { get; }

    /// <summary>
    /// Check if a given file name is a match or not.
    /// </summary>
    /// <param name="file">The file we are checking</param>
    /// <returns>True if the file matches the pattern and if the size matches as well.</returns>
    bool Match( FileInfo file );
  }
}