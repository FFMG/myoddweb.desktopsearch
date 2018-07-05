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

namespace myoddweb.desktopsearch.interfaces.IO
{
  public interface IDirectory
  {
    /// <summary>
    /// Parse a directlry and look for sub folders.
    /// </summary>
    /// <param name="path">The start path</param>
    /// <param name="actionFile">Called when a file is found</param>
    /// <param name="parseSubDirectory">Called when a directory is found, return true if we want to parse it further,</param>
    void Parse(string path, Action<FileInfo> actionFile, Func<bool, DirectoryInfo> parseSubDirectory );
  }
}