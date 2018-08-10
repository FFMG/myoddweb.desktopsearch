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
    private HashSet<string> _parts;

    /// <summary>
    /// Get all the parts of our word.
    /// </summary>
    public HashSet<string> Parts
    {
      get
      {
        if (_parts != null)
        {
          return _parts;
        }

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

        // Gauss formula for the sum of the serries of lettes.
        var setSize = (Value.Length * (Value.Length + 1)) / 2;
        _parts = new HashSet<string>();
        for (var start = 0; start < Value.Length; ++start)
        {
          for (var i = start; i < Value.Length; ++i)
          {
            _parts.Add(Value.Substring(start, ( i -start + 1)));
          }
        }

        return _parts;
      }
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
