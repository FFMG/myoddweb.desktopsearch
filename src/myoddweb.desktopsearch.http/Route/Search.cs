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
using System.Net;
using myoddweb.desktopsearch.http.Models;
using Newtonsoft.Json;

namespace myoddweb.desktopsearch.http.Route
{
  internal class Search : Route
  {
    /// <summary>
    /// This is the search route
    /// The method is 'search' while the value is the query. 
    /// </summary>
    public Search() : base(new[] { "Search" }, Method.Post)
    {
    }

    /// <inheritdoc />
    protected override RouteResponse OnProcess(string raw, Dictionary<string, string> parameters, HttpListenerRequest request)
    {
      try
      {
        // get the search query.
        var search = JsonConvert.DeserializeObject<SearchRequest>(raw);

        // build the response packet.
        var response = BuildResponse(search);

        return new RouteResponse
        {
          Response = JsonConvert.SerializeObject(response),
          StatusCode = HttpStatusCode.OK,
          ContentType = "application/json"
        };
      }
      catch (Exception e)
      {
        return new RouteResponse
        {
          Response = e.Message,
          StatusCode = HttpStatusCode.InternalServerError
        };
      }
    }

    /// <summary>
    /// Given the search request, build the response.
    /// </summary>
    /// <param name="search"></param>
    /// <returns></returns>
    private List<SearchResponse> BuildResponse(SearchRequest search)
    {
      // we can now build the response model.
      return new List<SearchResponse>
      {
        new SearchResponse{ FullName = "c:\\test1.txt", Directory = "C:\\", Name = "test1.txt"},
        new SearchResponse{ FullName = "c:\\test2.txt", Directory = "C:\\", Name = "test2.txt" },
        new SearchResponse{ FullName = "c:\\test3.txt", Directory = "C:\\", Name = "test3.txt" }
      };
    }
  }
}
