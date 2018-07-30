﻿//This file is part of Myoddweb.DesktopSearch.
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
using System.ComponentModel;
using myoddweb.desktopsearch.interfaces.Configs;
using Newtonsoft.Json;

namespace myoddweb.desktopsearch.service.Configs
{
  internal class ConfigTimers : ITimers
  {
    /// <inheritdoc />
    [DefaultValue(30000)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public int EventsParserMs { get; protected set; }

    /// <inheritdoc />
    [DefaultValue(2000)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public int EventsProcessorMs { get; protected set; }
  }
}
