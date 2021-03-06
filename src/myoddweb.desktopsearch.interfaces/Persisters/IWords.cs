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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.IO;

namespace myoddweb.desktopsearch.interfaces.Persisters
{
  public interface IWords: ITransaction
  {
    /// <summary>
    /// Add or update a single word.
    /// </summary>
    /// <param name="word"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<long> AddOrUpdateWordAsync( IWord word, CancellationToken token);

    /// <summary>
    /// Add or update multiple word.
    /// </summary>
    /// <param name="words"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<IList<long>> AddOrGetWordsAsync( IO.IWords words, CancellationToken token);

    /// <summary>
    /// Check if the given word is valid or not, (and can be added).
    /// </summary>
    /// <param name="word"></param>
    /// <returns></returns>
    bool IsValidWord( IWord word);
  }
}
