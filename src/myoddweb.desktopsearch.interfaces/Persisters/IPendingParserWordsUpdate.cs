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

namespace myoddweb.desktopsearch.interfaces.Persisters
{
  public interface IPendingParserWordsUpdate
  {
    /// <summary>
    /// All the file ids for that word.
    /// </summary>
    IList<long> FileIds { get; }

    /// <summary>
    /// The pending words.
    /// </summary>
    IO.IWord Word { get; }

    /// <summary>
    /// The pending word id.
    /// </summary>
    long Id { get; }
  }
}
