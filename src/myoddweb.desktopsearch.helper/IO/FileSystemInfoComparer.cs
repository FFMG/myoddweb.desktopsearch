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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace myoddweb.desktopsearch.helper.IO
{
  public class FileSystemInfoComparer<T> : IEqualityComparer<T> where T : FileSystemInfo
  {
    #region Member variables
    /// <summary>
    /// All the legal non string characters that could help us
    /// tell if a name is different to another
    /// Without actually looking at the string itself.
    /// </summary>
    private readonly ReadOnlyCollection<char> _legalNonStringCharacters = new List<char>{ '\\', '.', '$', ' ', ':' }.AsReadOnly();

    /// <summary>
    /// The backslash regex
    /// </summary>
    // ReSharper disable once StaticMemberInGenericType
    private static Regex Rgx { get; } = new Regex("\\\\{2,}");
    #endregion

    /// <summary>
    /// Check if 2 file items are equal.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
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

      // cleanup the 2 full names.
      // this is needed to make sure we are comparing
      // apples and apples.
      var xx = FullName(x);
      var yy = FullName(y);

      // if they are not the same lengh, they are not the same
      if (xx.Length != yy.Length)
      {
        return false;
      }

      // we now know they are exactly the same size.
      // check that non letter strings are at the same position...
      // it is better to do that first
      // if we compare case we might run the risk of local
      // characters not returning proper values, (a good example is turkish characters).
      var l = xx.Length;
      for (var i = 0; i < l; ++i)
      {
        // if either of them is a backslash and either of them is not equal
        // then we know that they cannot be the same.
        if (_legalNonStringCharacters.Any(a => (xx[i] == a || yy[i] == a) && xx[i] != yy[i]))
        {
          return false;
        }
      }

      // just about everything seems to math...
      // we can try and compare strings now.
      // and hope that there is no funny characters that make then
      // almost equal...
      return string.Equals(xx, yy, StringComparison.OrdinalIgnoreCase);
    }

    public int GetHashCode(T obj)
    {
      return FullName(obj).GetHashCode();
    }

    /// <summary>
    /// Cleanup the given full name so we can use it.
    /// </summary>
    /// <param name="x"></param>
    /// <returns></returns>
    public static string FullName(T x)
    {
      // get the input, throw in case of null.
      var input = x?.FullName ?? throw new ArgumentNullException(nameof(x));

      // Cater for network drives.
      // we cannot just look for '\'  as you can type '\hello' to navidate
      // to the current directory.
      var isNetword = input.Length > 2 && (input[0] == '\\' && input[1] == '\\');

      // simple clean up
      input = input.Replace('/', '\\').Trim();

      if (x is DirectoryInfo)
      {
        // add a backslash at the end
        // if there is one already it will be removed.
        input += '\\';
      }

      // replace all the double back spaces
      // with just a single one.
      input = Rgx.Replace(input, "\\");

      // if this is a network drive remember to add 
      // the leading '\' that we removed with the previous regex.
      return isNetword ? "\\" + input : input;
    }
  }
}
