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
    [DefaultValue(10000)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public int QuietEventsProcessorMs { get; protected set; }

    /// <inheritdoc />
    [DefaultValue(20)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public int BusyEventsProcessorMs { get; protected set; }

    /// <inheritdoc />
    [DefaultValue(50)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public int UpdatesFilesPerEvent { get; protected set; }

    /// <inheritdoc />
    [DefaultValue(10)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public int UpdatesFolderPerEvent { get; protected set; }

    /// <inheritdoc />
    [DefaultValue(50)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public int UpdateWordParsedPerEvent { get; protected set; }

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
      if (BusyEventsProcessorMs <= 0)
      {
        throw new ArgumentException($"The 'BusyEventsProcessorMs'({BusyEventsProcessorMs}) cannot be -ve or zero");
      }
      if (QuietEventsProcessorMs <= 0)
      {
        throw new ArgumentException($"The 'QuietEventsProcessorMs'({QuietEventsProcessorMs}) cannot be -ve or zero");
      }
      if (QuietEventsProcessorMs < BusyEventsProcessorMs)
      {
        throw new ArgumentException("The 'QuietEventsProcessorMs' cannot be less than the 'BusyEventsProcessorMs'");
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
