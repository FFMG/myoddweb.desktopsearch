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

    public ConfigActive( int to, int from)
    {
      To = to;
      From = from;
      if (To < 0)
      {
        throw new ArgumentException( $"The 'To' value cannot be -ve ({To}).", nameof(To) );
      }
      if (From < 0)
      {
        throw new ArgumentException($"The 'From' value cannot be -ve ({From}).", nameof(From));
      }

      // The 'to' value is exclusive
      if (To > 24)
      {
        throw new ArgumentException($"The 'To' value cannot be more than 24hrs ({To}).", nameof(To));
      }

      // the 'from' value is inclusive, so 24hr is not posible
      if (From == 24)
      {
        From = 0;
      }
      if (From > 23 )
      {
        throw new ArgumentException($"The 'From' value cannot be more than 23hrs ({From}).", nameof(From));
      }
    }

    /// <inheritdoc />
    public bool IsActive()
    {
      // The from is inclusive, so 8:00 is from 8:00... 8:05 and so on.
      // the to is exclusive so 23:00 is up to 23:00.
      var currentTime = Utc ? DateTime.UtcNow : DateTime.Now;
      var currentHour = currentTime.Hour;
      if (From < To)
      {
        // we are checking times during the day
        // something like 8:00 and 22:00
        return currentHour >= From && currentHour < To;
      }

      // we are checking for over night times
      // something like from 22:00 to 5:00
      // the actuall check should be (hr >= From && hr <24) || hr >= 0 && hr <= To)
      // but we already know that the To/From are within ranges.
      return currentHour >= From || currentHour < To;
    }
  }
}
