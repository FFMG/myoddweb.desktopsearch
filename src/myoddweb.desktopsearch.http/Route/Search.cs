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
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;

namespace myoddweb.desktopsearch.http.Route
{
  internal class Search : Route
  {
    /// <summary>
    /// This is the search route
    /// The method is 'search' while the value is the query. 
    /// </summary>
    public Search() : base(new[] { "Search", "{query}" }, Method.Get)
    {
    }

    /// <summary>
    /// Build the response packet
    /// </summary>
    /// <param name="parameters"></param>
    /// <param name="request"></param>
    /// <returns></returns>
    protected override RouteResponse OnProcess(Dictionary<string, string> parameters, HttpListenerRequest request)
    {
      return new RouteResponse
      {
        Response = JsonConvert.SerializeObject(parameters),
        StatusCode = HttpStatusCode.OK,
        ContentType = "application/json"
      };
    }
  }
}
