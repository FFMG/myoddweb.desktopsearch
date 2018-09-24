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
    [JsonProperty]
    public int BusyEventsProcessorMs { get; protected set; }

    /// <inheritdoc />
    [JsonProperty]
    public int ConcurrentDirectoriesProcessor { get; protected set; }

    /// <inheritdoc />
    [JsonProperty]
    public int ConcurrentFilesProcessor { get; protected set; }

    /// <inheritdoc />
    [DefaultValue(100)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public int UpdatesPerFilesEvent { get; protected set; }

    /// <inheritdoc />
    [DefaultValue(null)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public IList<IIgnoreFile> IgnoreFiles { get; protected set; }

    public ConfigProcessor(IList<ConfigIgnoreFile> ignoreFiles)
    {
      // the number of directories processor.
      ConcurrentDirectoriesProcessor = Environment.ProcessorCount;

      // the number of files to process 
      // as we, (normally), have more files than directories
      // we want to use the number of processors to do them all.
      ConcurrentFilesProcessor = 2*Environment.ProcessorCount;

      // this is per ms, so we want to have one busy processor every ms 
      BusyEventsProcessorMs = Environment.ProcessorCount;

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

      if (ConcurrentDirectoriesProcessor <= 0)
      {
        throw new ArgumentException( $"The ConcurrentDirectoriesProcessor cannot be -ve or zeor ({ConcurrentDirectoriesProcessor}");
      }
      if (ConcurrentFilesProcessor <= 0)
      {
        throw new ArgumentException($"The ConcurrentFilesProcessor cannot be -ve or zeor ({ConcurrentFilesProcessor}");
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
