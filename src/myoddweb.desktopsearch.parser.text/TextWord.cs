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

namespace myoddweb.desktopsearch.parser.text
{
  internal class TextWord : IWord
  {
    /// <inheritdoc />
    public string Word { get; }

    /// <summary>
    /// The word we are adding.
    /// </summary>
    /// <param name="word"></param>
    public TextWord(string word)
    {
      // the name cannot be null
      Word = word ?? throw new ArgumentNullException( nameof(word));
    }
  }
}
