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

namespace myoddweb.desktopsearch.interfaces.IO
{
  public class WordEqualityComparer : IEqualityComparer<IWord>
  {
    public bool Equals(IWord x, IWord y)
    {
      if (x == null && y == null)
      {
        return true;
      }
      if (x == null || y == null)
      {
        return false;
      }
      return x.Word == y.Word;
    }

    public int GetHashCode(IWord obj)
    {
      return obj.Word.GetHashCode();
    }
  }
}
