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
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

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
    /// Execute a write command, open the db connection if needed.
    /// </summary>
    /// <param name="command"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<int> ExecuteWriteAsync(IDbCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Execute a read command.
    /// @see https://github.com/Microsoft/referencesource/blob/master/System.Data/System/Data/Common/DBCommand.cs
    /// @see https://github.com/dotnet/corefx/commit/297fcc33db4e65287455f6575684f24975688b53
    /// </summary>
    /// <param name="command"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IDataReader> ExecuteReadAsync(IDbCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Executes the query, and returns the first column of the first row in the result set returned by the query. Additional columns or rows are ignored.
    /// @see https://github.com/Microsoft/referencesource/blob/master/System.Data/System/Data/Common/DBCommand.cs
    /// @see https://github.com/dotnet/corefx/commit/297fcc33db4e65287455f6575684f24975688b53
    /// </summary>
    /// <param name="command"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<object> ExecuteReadOneAsync(IDbCommand command, CancellationToken cancellationToken);

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