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
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Models;

namespace myoddweb.desktopsearch.interfaces.Persisters
{
  public interface IQuery
  {
    /// <summary>
    /// Look for a certain word in the database
    /// </summary>
    /// <param name="what">The word we are looking for.</param>
    /// <param name="count">How many do we want?</param>
    /// <param name="factory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<IList<IWord>> FindAsync( string what, int count, IConnectionFactory factory, CancellationToken token);
  }
}
