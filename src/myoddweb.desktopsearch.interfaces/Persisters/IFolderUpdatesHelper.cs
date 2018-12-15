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
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Enums;

namespace myoddweb.desktopsearch.interfaces.Persisters
{
  public interface IFolderUpdatesHelper : IDisposable
  {
    /// <summary>
    /// Delete the pending folder ids
    /// </summary>
    /// <param name="ids"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task DeleteAsync(IReadOnlyCollection<long> ids, CancellationToken token);

    /// <summary>
    /// Touch certain folder ids.
    /// </summary>
    /// <param name="folderIds"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    Task TouchAsync(IReadOnlyCollection<long> folderIds, UpdateType type, CancellationToken token);
  }
}