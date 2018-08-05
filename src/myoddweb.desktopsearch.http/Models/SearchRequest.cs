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
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace myoddweb.desktopsearch.http.Models
{
  internal class SearchRequest
  {
    /// <summary>
    /// The string we are searching for.
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    public string What { get; set; }

    /// <summary>
    /// The maximun number of items we want to get
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    public int Count { get; set; }

    /// <summary>
    /// Validate that the values given are valid.
    /// </summary>
    /// <param name="context"></param>
    [OnDeserialized]
    internal void OnDeserialized(StreamingContext context)
    {
      if (Count <= 0)
      {
        throw new ArgumentException( $"The number of items to return cannot be zero or -ve ({Count})");
      }
    }
  }
}
