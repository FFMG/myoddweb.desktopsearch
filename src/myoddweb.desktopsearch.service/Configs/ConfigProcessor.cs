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
using System.Runtime.Serialization;
using myoddweb.desktopsearch.interfaces.Configs;
using myoddweb.desktopsearch.service.IO;
using Newtonsoft.Json;

namespace myoddweb.desktopsearch.service.Configs
{
  internal class ConfigProcessor : IProcessors
  {
    /// <inheritdoc />
    [DefaultValue(200)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public int EventsProcessorMs { get; protected set; }

    [DefaultValue(60)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public int MaintenanceProcessorMinutes { get; protected set; }

    /// <inheritdoc />
    [DefaultValue(50)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public int UpdatesFilesPerEvent { get; protected set; }

    /// <inheritdoc />
    [DefaultValue(50)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public int UpdatesFolderPerEvent { get; protected set; }

    /// <inheritdoc />
    [DefaultValue(null)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public IList<IIgnoreFile> IgnoreFiles { get; protected set; }

    public ConfigProcessor(IList<ConfigIgnoreFile> ignoreFiles)
    {
      if (null != ignoreFiles)
      {
        IgnoreFiles = new List<IIgnoreFile>();
        foreach (var ignoreFile in ignoreFiles)
        {
          IgnoreFiles.Add( new IgnoreFile(ignoreFile.Pattern, ignoreFile.MaxSizeMegabytes ));
        }
      }
    }

    [OnDeserialized]
    internal void OnDeserialized(StreamingContext context)
    {
      if (EventsProcessorMs <= 0)
      {
        throw new ArgumentException($"The 'EventsProcessorMs'({EventsProcessorMs}) cannot be -ve or zero");
      }

      if(MaintenanceProcessorMinutes <= 0 )
      {
        throw new ArgumentException($"The 'MaintenanceProcessorMs'({MaintenanceProcessorMinutes}) cannot be -ve or zero");
      }

      if (null == IgnoreFiles)
      {
        IgnoreFiles = new List<IIgnoreFile>
        {
          new IgnoreFile( "*.*", 1024 )
        };
      }
    }
  }
}
