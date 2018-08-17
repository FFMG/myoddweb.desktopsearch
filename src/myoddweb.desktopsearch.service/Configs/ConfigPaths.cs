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
using System.Linq;
using System.Runtime.Serialization;
using myoddweb.desktopsearch.interfaces.Configs;
using Newtonsoft.Json;

namespace myoddweb.desktopsearch.service.Configs
{
  internal class ConfigPaths : IPaths
  {
    [DefaultValue(true)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public bool ParseFixedDrives { get; protected set; }

    [DefaultValue(false)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public bool ParseRemovableDrives { get; protected set; }

    [DefaultValue(null)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public List<string> Paths { get; protected set; }

    [DefaultValue(null)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public List<string> IgnoredPaths { get; protected set; }

    [DefaultValue(true)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public bool IgnoreInternetCache { get; protected set; }

    [DefaultValue(true)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public bool IgnoreRecycleBins { get; protected set; }

    [JsonProperty(Required = Required.Always)]
    public List<string> ComponentsPaths { get; protected set; }

    [OnDeserialized]
    internal void OnDeserialized(StreamingContext context)
    {
      if (null == IgnoredPaths)
      {
        //  https://www.microsoft.com/en-us/wdsi/help/folder-variables
        IgnoredPaths = new List<string>
        {
          "%temp%",
          "%tmp%",
          "%ProgramFiles%",
          "%ProgramFiles(x86)%",
          "%ProgramW6432%",
          "%windir%",
          "%SystemRoot%"
        };
      }

      // Recycle Bins
      if (IgnoreRecycleBins)
      {
        // Add the recycle bins as well.
        var drvs = DriveInfo.GetDrives();
        foreach (var drv in drvs.Where(d => d.DriveType == DriveType.Fixed))
        {
          IgnoredPaths.Add(Path.Combine(drv.Name, "$recycle.bin"));
        }
      }

      // internet cache
      if (IgnoreInternetCache)
      {
        IgnoredPaths.Add(Environment.GetFolderPath(Environment.SpecialFolder.InternetCache));
      }

      if (!ComponentsPaths.Any())
      {
        throw new ArgumentException("You must have at least one valid component path!");
      }
    }
  }
}
