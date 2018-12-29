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
using myoddweb.desktopsearch.interfaces.Configs;
using Newtonsoft.Json;

namespace myoddweb.desktopsearch.service.Configs
{
  internal class ConfigActive : IActive
  {
    /// <inheritdoc />
    [DefaultValue(23)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public int From { get; protected set; }

    /// <inheritdoc />
    [DefaultValue(5)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public int To { get; protected set; }

    /// <inheritdoc />
    [DefaultValue(false)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public bool Utc { get; protected set; }

    /// <inheritdoc />
    public bool IsActive()
    {
      var currentTime = Utc ? DateTime.UtcNow : DateTime.Now;
      var currentHour = currentTime.Hour;
      if (From < To)
      {
        // we are checking times during the day
        // something like 8:00 and 22:00
        return currentHour >= From && currentHour <= To;
      }

      // we are checking for over night times
      // something like from 22:00 to 5:00
      // the actuall check should be (hr >= From && hr <24) || hr >= 0 && hr <= To)
      // but we already know that the To/From are within ranges.
      return currentHour >= From || currentHour <= To;
    }
  }
}
