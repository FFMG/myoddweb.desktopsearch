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
  internal class RouteFile : Route
  {
    #region Member Variables
    /// <summary>
    /// The home page body.
    /// </summary>
    private string _script;

    /// <summary>
    /// The full rescource name
    /// </summary>
    private readonly string _resourceName;
    #endregion

    #region Properties
    /// <summary>
    /// Get the home page body
    /// </summary>
    private string ScriptPage
    {
      get
      {
        if (null != _script)
        {
          return _script;
        }

        var assembly = Assembly.GetExecutingAssembly();
        using (var stream = assembly.GetManifestResourceStream(_resourceName))
        using (var reader = new StreamReader(stream ?? throw new InvalidOperationException()))
        {
          _script = reader.ReadToEnd();
          return _script;
        }
      }
    }

    /// <summary>
    /// The file content type.
    /// </summary>
    private string ContentType { get; }
    #endregion

    protected RouteFile(string script, string contentType ) : base(new[] { script }, Method.Get)
    {
      _resourceName = $"myoddweb.desktopsearch.http.Resources.{script}";
      ContentType = contentType;
    }

    /// <inheritdoc />
    protected override RouteResponse OnProcess(string raw, Dictionary<string, string> parameters, HttpListenerRequest request)
    {
      return new RouteResponse
      {
        Response = ScriptPage,
        StatusCode = HttpStatusCode.OK,
        ContentType = ContentType
      };
    }
  }
}
