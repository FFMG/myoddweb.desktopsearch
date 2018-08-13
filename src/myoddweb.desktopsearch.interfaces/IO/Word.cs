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
using System.Linq;

namespace myoddweb.desktopsearch.interfaces.IO
{
  public class Word
  {
    /// <summary>
    /// The word value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// All the parts of the word.
    /// </summary>
    private IReadOnlyCollection<string> _parts;

    /// <summary>
    /// The maximum number of meaningful charactets in the parts.
    /// Anything longer than that will be ignored.
    /// </summary>
    private int _lastMaxNumMeaningfulCharacters;

    /// <summary>
    /// Get all the parts of our word.
    /// <param name="maxNumMeaningfulCharacters"></param>
    /// </summary>
    public IReadOnlyCollection<string> Parts(int maxNumMeaningfulCharacters)
    {
      if (maxNumMeaningfulCharacters <= 0)
      {
        throw new ArgumentException("The number of meaningful characters cannot be zero or -ve");
      }

      if (_parts != null && _lastMaxNumMeaningfulCharacters == maxNumMeaningfulCharacters )
      {
        return _parts;
      }

      // save the last number of items, so we don't
      //  run the same query more than once.
      _lastMaxNumMeaningfulCharacters = maxNumMeaningfulCharacters;

      // Help <- Word
      // H
      // He
      // Hel
      // Help         << the word help itslef is a 'part'
      // e
      // elp
      // l
      // lp
      // Word len             = 4
      // expected hashet size = 8
      //
      // Mix = 3 letters 
      //     = 6 expected
      // M
      // Mi
      // Mix
      // i
      // ix
      // x

      // Gauss formula for the sum of the serries of letters.
      var l = Value.Length;
      var setsize = (l * (l + 1)) / 2;
      var parts = new List<string>(setsize);
      for (var start = 0; start < l; ++start)
      {
        for (var i = start; i < l; ++i)
        {
          var length = (i - start + 1);
          if (length > maxNumMeaningfulCharacters)
          {
            continue;
          }
          parts.Add(Value.Substring(start, length ));
        }
      }

      // convert it to a list and remove duplicates.
      _parts = parts.Distinct().ToList();

      // then return the value.
      return _parts;
    }

    /// <summary>
    /// The word we are adding.
    /// </summary>
    /// <param name="value"></param>
    public Word(string value)
    {
      // the name cannot be null
      Value = value ?? throw new ArgumentNullException(nameof(value));
    }
  }
}
