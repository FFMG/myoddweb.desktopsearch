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
using System.Text.RegularExpressions;

namespace myoddweb.desktopsearch.http.Route
{
  internal abstract class Route
  {
    public enum Method
    {
      All,
      Get,
      Post
    }

    /// <summary>
    /// All the parts in the route.
    /// </summary>
    private readonly List<string> _parts;

    /// <summary>
    /// All the parameters we will be processing.
    /// </summary>
    private readonly List<string> _parameters;

    /// <summary>
    /// The method we will be accepting.
    /// </summary>
    private readonly Method _method;

    protected Route(IReadOnlyCollection<string> parts, Method method )
    {
      // make sure that the parts are 'clean'
      _parts = CleanParts(parts);

      // we then need to get the parameters
      _parameters = GetParameterNames(parts);

      // save the method as well.
      _method = method;
    }

    /// <summary>
    /// Build a list of parameters at the end of our route
    /// All parameters have something like /route/{id}/{name}
    /// We will throw if we have an invalid route
    /// With values like /route/{id}/again
    /// </summary>
    /// <param name="parts"></param>
    /// <returns></returns>
    private static List<string> GetParameterNames(IEnumerable<string> parts)
    {
      var regex = new Regex("^{([^}]+)}$");
      var parameters = new List<string>();
      foreach (var part in parts)
      {
        if (!regex.IsMatch(part))
        {
          if (parameters.Count > 0)
          {
            // we cannot start having more routing after the parameters
            // /route/{a}       is valid
            // /route/{a}/route is not valid
            throw new ArgumentException("The given route is invalid, there is a part _after_ the parameters.");
          }
          continue;
        }

        // add this parameter to our list.
        parameters.Add(part.TrimStart('{').TrimEnd('}'));
      }
      return parameters;
    }

    /// <summary>
    /// Check if the given route is a match for this route.
    /// We check the parts and exclude the parameters.
    /// </summary>
    /// <param name="parts"></param>
    /// <param name="httpMethod"></param>
    /// <returns></returns>
    public bool IsMarch(IEnumerable<string> parts, string httpMethod )
    {
      // check the method firts
      if (!IsMethodMatch(httpMethod))
      {
        return false;
      }

      var cleanParts = CleanParts(parts);

      // quick check
      if (cleanParts.Count != _parts.Count )
      {
        return false;
      }

      // we then need to compare the values
      // up to when the parameters are
      for (var i = 0; i < cleanParts.Count - _parameters.Count; ++i)
      {
        if (cleanParts[i] != _parts[i])
        {
          return false;
        }
      }

      // if we made it this far
      // then it means that the parts are a match.
      return true;
    }

    /// <summary>
    /// Check if we support the given http method.
    /// </summary>
    /// <param name="httpMethod"></param>
    /// <returns></returns>
    private bool IsMethodMatch(string httpMethod)
    {
      if (_method == Method.All)
      {
        return true;
      }

      if (0 == string.Compare(httpMethod, "GET", StringComparison.InvariantCultureIgnoreCase))
      {
        return _method == Method.Get;
      }

      if (0 == string.Compare(httpMethod, "POST", StringComparison.InvariantCultureIgnoreCase))
      {
        return _method == Method.Post;
      }

      // we do not support that method.
      return false;
    }

    /// <summary>
    /// Clean the given route parts.
    /// </summary>
    /// <param name="parts"></param>
    /// <returns></returns>
    private static List<string> CleanParts(IEnumerable<string> parts)
    {
      var cleanParts = new List<string>();
      foreach (var part in parts)
      {
        if (part == "/")
        {
          continue;
        }

        // remove posible trailling '/'
        cleanParts.Add( part.Trim( '/' ));
      }
      return cleanParts;
    }

    /// <summary>
    /// Process the request and return a string.
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public RouteResponse Process( HttpListenerRequest request )
    {
      // Get all the parameters.
      var parameters = GetParameters(request.Url.Segments);

      // and then get the routes to return the response.
      return OnProcess( parameters, request );
    }

    /// <summary>
    /// Get the parameters from the request
    /// </summary>
    /// <param name="urlSegments"></param>
    /// <returns></returns>
    private Dictionary<string,string> GetParameters(IEnumerable<string> urlSegments)
    {
      // get the parts.
      var parts = CleanParts(urlSegments);

      // the return value
      var parameters = new Dictionary<string,string>( _parameters.Count );
      var offset = parts.Count - _parameters.Count;
      for (var i = offset; i < _parts.Count; i++)
      {
        parameters.Add( _parameters[i-offset], parts[i] );
      }
      return parameters;
    }

    /// <summary>
    /// All the route get to build their own responses now.
    /// </summary>
    /// <param name="parameters"></param>
    /// <param name="request"></param>
    /// <returns></returns>
    protected abstract RouteResponse OnProcess(Dictionary<string, string> parameters, HttpListenerRequest request);
  }
}

