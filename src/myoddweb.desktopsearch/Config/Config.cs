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
using myoddweb.desktopsearch.Interfaces;

namespace myoddweb.desktopsearch.Config
{
  internal class Config : IConfig
  {
    /// <inheritdoc />
    public string Url { get; protected set; }

    /// <inheritdoc />
    public string Save { get; protected set; }

    /// <inheritdoc />
    public int Port { get; protected set; }

    /// <inheritdoc />
    public int MinimumSearchLength { get; protected set; }

    public Config(string url, string save, int port, int minimumSearchLength)
    {
      //  set the save json.
      Save = save ?? "save.json";

      // the url value.
      Url = url ?? throw new ArgumentNullException( nameof(url));

      // port
      Port = port;
      if (Port < 0)
      {
        throw new ArgumentException( @"The port number cannot be -ve", nameof(Port));
      }

      // MinimumSearchLength
      MinimumSearchLength = minimumSearchLength;
      if (MinimumSearchLength < 0)
      {
        throw new ArgumentException( @"The minimum search length cannot be -ve", nameof(MinimumSearchLength));
      }
    }
  }
}
