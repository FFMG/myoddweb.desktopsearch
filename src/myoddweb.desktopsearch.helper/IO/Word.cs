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
using myoddweb.desktopsearch.interfaces.IO;

namespace myoddweb.desktopsearch.helper.IO
{
  public class Word : IWord
  {
    /// <inheritdoc />
    public string Value { get; }

    /// <summary>
    /// All the parts of the word.
    /// </summary>
    private Parts _parts;

    /// <inheritdoc />
    public IParts Parts(int maxNumMeaningfulCharacters)
    {
      if (maxNumMeaningfulCharacters <= 0)
      {
        throw new ArgumentException("The number of meaningful characters cannot be zero or -ve");
      }

      if (_parts != null && _parts.MaxPartLength == maxNumMeaningfulCharacters )
      {
        return _parts;
      }

      // save the parts.
      _parts = new Parts(Value, maxNumMeaningfulCharacters);
      
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
