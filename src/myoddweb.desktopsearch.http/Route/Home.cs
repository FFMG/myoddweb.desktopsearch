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
using System.IO;
using System.Net;
using System.Reflection;

namespace myoddweb.desktopsearch.http.Route
{
  internal class Home : Route
  {
    /// <summary>
    /// The home page body.
    /// </summary>
    private string _homePage;

    /// <summary>
    /// Get the home page body
    /// </summary>
    private string HomePage
    {
      get
      {
        if (null != _homePage)
        {
          return _homePage;
        }

        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "myoddweb.desktopsearch.http.Route.index.html";
        using (var stream = assembly.GetManifestResourceStream(resourceName))
        using (var reader = new StreamReader(stream ?? throw new InvalidOperationException()))
        {
          _homePage = reader.ReadToEnd();
          return _homePage;
        }
      }
    }

    /// <summary>
    /// This is the search route
    /// The method is 'search' while the value is the query. 
    /// </summary>
    public Home() : base(new string [0], Method.Get)
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
        Response = HomePage,
        StatusCode = HttpStatusCode.OK,
        ContentType = "text/html"
      };
    }
  }
}
