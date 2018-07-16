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
using myoddweb.desktopsearch.interfaces.Logging;
using ILogger = myoddweb.desktopsearch.interfaces.Configs.ILogger;

namespace myoddweb.desktopsearch.service.Configs
{
  internal class ConfigFileLogger : ILogger
  {
    public LogLevel LogLevel { get; }

    public DirectoryInfo BaseDirectoryInfo { get; }

    public ConfigFileLogger( string path, LogLevel ll)
    {
      BaseDirectoryInfo = path != null ? new DirectoryInfo(path) : throw new ArgumentNullException(path);
      if (!BaseDirectoryInfo.Exists)
      {
        BaseDirectoryInfo.Create();
      }
      if (!BaseDirectoryInfo.Exists)
      {
        throw new ArgumentException($"I was unable to create the path: {path}");
      }
      LogLevel = ll;
    }
  }
}
