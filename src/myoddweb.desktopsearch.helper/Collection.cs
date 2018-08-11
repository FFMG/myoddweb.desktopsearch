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
using System.Linq;

namespace myoddweb.desktopsearch.helper
{
  public class Collection
  {
    /// <summary>
    /// Get the relative complement
    /// Return the list of elements that are in B but not in A
    /// </summary>
    /// <param name="lhs"></param>
    /// <param name="rhs"></param>
    /// <param name="comparer"></param>
    /// <returns></returns>
    public static HashSet<T> RelativeComplement<T>(HashSet<T> lhs, HashSet<T> rhs, IEqualityComparer<T> comparer = null )
    {
      // if they are both empty then the complement is nothing.
      if (null == lhs && null == rhs)
      {
        return new HashSet<T>();
      }

      // if we have a null A then everything in B is the relative
      // complement as they never intercet.
      if (null == lhs || !lhs.Any())
      {
        return rhs ?? new HashSet<T>();
      }

      // If B is empty then it will never intercet with A
      // so there is no complement values.
      if (null == rhs || !rhs.Any())
      {
        return new HashSet<T>();
      }

      // we can now go around B and find all the ones that are _not_ in A
      // A = {2,3,4}
      // B = {3,4,5}
      // RC = {5}
      var rc = comparer == null ? new HashSet<T>() : new HashSet<T>(comparer);
      foreach (var l in rhs )
      {
        if (lhs.Contains(l))
        {
          continue;
        }
        rc.Add(l);
      }

      // return the relatibe complements.
      return rc;
    }
  }
}
