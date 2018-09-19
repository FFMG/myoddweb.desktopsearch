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
using System.ComponentModel;
using System.Runtime.Serialization;
using myoddweb.desktopsearch.interfaces.Configs;
using Newtonsoft.Json;

namespace myoddweb.desktopsearch.service.Configs
{
  internal class ConfigPerformance : IPerformance
  {
    /// <inheritdoc />
    [DefaultValue("myoddweb.desktopsearch")]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public string CategoryName { get; protected set; }

    /// <inheritdoc />
    [DefaultValue("Performance counters for myoddweb.desktopsearch")]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public string CategoryHelp { get; protected set; }

    [DefaultValue(true)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public bool DeleteStartUp { get; protected set; }

    [OnDeserialized]
    internal void OnDeserialized(StreamingContext context)
    {
      if (string.IsNullOrEmpty(CategoryName))
      {
        throw new ArgumentException( $"The category name cannot be null/empty ({CategoryName ?? "null"})");
      }

      if (string.IsNullOrEmpty(CategoryHelp))
      {
        CategoryHelp = CategoryName;
      }
    }
  }
}
