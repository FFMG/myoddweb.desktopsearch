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

namespace myoddweb.desktopsearch.interfaces.Persisters
{
  public interface IConnectionFactory
  {
    /// <summary>
    /// If the factory is readonly or not.
    /// </summary>
    bool IsReadOnly { get; }

    /// <summary>
    /// The current connection
    /// </summary>
    IDbConnection Connection { get; }

    /// <summary>
    /// Create a db commands
    /// </summary>
    /// <param name="sql"></param>
    /// <returns></returns>
    DbCommand CreateCommand( string sql );

    /// <summary>
    /// Commit a transaction, (and close the transaction)
    /// </summary>
    void Commit();

    /// <summary>
    /// rollback a transaction, (and close the transaction)
    /// </summary>
    void Rollback();
  }
}