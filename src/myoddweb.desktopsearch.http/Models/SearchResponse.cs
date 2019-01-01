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
using System.Collections.Generic;
using myoddweb.desktopsearch.helper.Models;
using myoddweb.desktopsearch.interfaces.Models;

namespace myoddweb.desktopsearch.http.Models
{
  internal class SearchResponse
  {
    /// <summary>
    /// All the words we found.
    /// </summary>
    public IList<IWord> Words { get; set; }

    /// <summary>
    /// How long it took to get the insformation.
    /// </summary>
    public long ElapsedMilliseconds { get; set; }

    /// <summary>
    /// The status of the entire system.
    /// </summary>
    public StatusResponse Status { get; set; }
  }
}
