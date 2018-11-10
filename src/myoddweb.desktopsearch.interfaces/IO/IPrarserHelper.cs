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
using System.Threading;
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.interfaces.IO
{
  public interface IParserHelper : IDisposable
  {
    /// <summary>
    /// The file that is being processed.
    /// </summary>
    FileSystemInfo File { get; }

    /// <summary>
    /// The number of words added.
    /// </summary>
    long Count { get; }

    /// <summary>
    /// Add multiple words.
    /// </summary>
    /// <param name="words"></param>
    /// <param name="token"></param>
    /// <returns>The number of words actually added.</returns>
    Task<long> AddWordsAsync(IReadOnlyList<string> words, CancellationToken token );
  }
}