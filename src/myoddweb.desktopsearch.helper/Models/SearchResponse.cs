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
using myoddweb.desktopsearch.interfaces.Models;
using Newtonsoft.Json;

namespace myoddweb.desktopsearch.helper.Models
{
  public class SearchResponse : ISearchResponse
  {
    /// <inheritdoc />
    public IList<IWord> Words { get; protected set; }

    /// <inheritdoc />
    public long ElapsedMilliseconds { get; protected set; }

    /// <inheritdoc />
    public IStatusResponse Status { get; protected set; }

    [JsonConstructor]
    protected SearchResponse(IList<Word> words, long elapsedMilliseconds, StatusResponse status)
    {
      Words = new List<IWord>(words);
      ElapsedMilliseconds = elapsedMilliseconds;
      Status = status;
    }

    public SearchResponse(IList<IWord> words, long elapsedMilliseconds, IStatusResponse status)
    {
      Words = words;
      ElapsedMilliseconds = elapsedMilliseconds;
      Status = status;
    }

    public SearchResponse()
    {
      Words = null;
      ElapsedMilliseconds = 0;
      Status = null;
    }
  }
}
