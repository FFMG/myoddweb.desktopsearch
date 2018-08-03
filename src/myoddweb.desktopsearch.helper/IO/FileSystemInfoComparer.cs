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

namespace myoddweb.desktopsearch.helper.IO
{
  public class FileSystemInfoComparer<T> : IEqualityComparer<T> where T : FileSystemInfo
  {
    public bool Equals(T x, T y)
    {
      // if they are both null, they are the same.
      if (x == null && y == null)
      {
        return true;
      }

      // we know that they are not both null
      // so if one of them is null... they are not the samme
      if (x == null || y == null)
      {
        return false;
      }

      // then we compare the full name ignoring the case.
      return string.Equals(x.FullName, y.FullName, StringComparison.InvariantCultureIgnoreCase);
    }

    public int GetHashCode(T obj)
    {
      return obj.FullName.GetHashCode();
    }
  }
}
