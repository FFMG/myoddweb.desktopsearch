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
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Logging;

namespace myoddweb.desktopsearch.interfaces.Persisters
{
  public interface IPersister : IConfig, IFolders, IFolderUpdates, IFiles, IFileUpdates, IWords, IFilesWords
  {
    /// <summary>
    /// Create a command with a transaction.
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="transaction"></param>
    /// <returns></returns>
    DbCommand CreateDbCommand(string sql, IDbTransaction transaction);

    /// <summary>
    /// Get a database transaction.
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<IDbTransaction> Begin( CancellationToken token );

    /// <summary>
    /// Rollback the current transaction.
    /// </summary>
    /// <returns></returns>
    bool Rollback(IDbTransaction transaction);

    /// <summary>
    /// Commit the current transaction.
    /// </summary>
    /// <returns></returns>
    bool Commit(IDbTransaction transaction);
  }
}