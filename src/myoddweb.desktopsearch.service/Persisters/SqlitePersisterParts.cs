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

using System;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal class SqlitePersisterParts : IParts
  {
    /// <inheritdoc />
    public int MaxNumCharactersPerParts { get; }

    public SqlitePersisterParts(int maxNumCharactersPerParts)
    {
      if (maxNumCharactersPerParts <= 0)
      {
        throw new ArgumentException( $"The max number of charaters per parts cannot be -ve or zero, ({maxNumCharactersPerParts})!");
      }
      MaxNumCharactersPerParts = maxNumCharactersPerParts;
    }
  }
}
