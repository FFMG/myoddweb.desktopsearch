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
using System.ComponentModel;
using System.IO;
using myoddweb.desktopsearch.interfaces.Enums;
using Newtonsoft.Json;

namespace myoddweb.desktopsearch.service.Configs
{
  internal class ConfigLogger
  {
    [JsonProperty(Required = Required.Always)]
    public string Type { get; protected set; }

    /// <summary>
    /// The expanded dabase source.
    /// </summary>
    private string _path;

    /// <summary>
    /// Path only used for certain loggers.
    /// </summary>
    [DefaultValue(null)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public string Path
    {
      get => _path;
      protected set
      {
        if (value == null)
        {
          _path = null;
          return;
        }
        // set the database source
        _path = Environment.ExpandEnvironmentVariables(value);

        // if not null, make sure that the path is set.
        var path = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
        {
          Directory.CreateDirectory(path);
        }
      }
    }

    [JsonProperty(Required = Required.Always)]
    public List<LogLevel> LogLevels { get; protected set; }
  }
}
