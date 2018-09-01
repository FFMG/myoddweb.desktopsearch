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
using System.Data.Common;
using System.Data.SQLite;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal class SqliteReadOnlyConnectionFactory : SqliteConnectionFactory
  {
    public override bool IsReadOnly => true;

    public SqliteReadOnlyConnectionFactory( string source ) : 
      base(  new SQLiteConnection($"Data Source={source};Version=3;Pooling=True;Max Pool Size=100;Read Only=True;") )
    {
    }

    /// <inheritdoc />
    protected override void OnCommit()
    {
      //  we do nothing
    }

    /// <inheritdoc />
    protected override void OnRollback()
    {
      //  we do nothing
    }

    /// <inheritdoc />
    protected override void OnClose()
    {
      //  we do nothing
    }

    /// <inheritdoc />
    protected override DbCommand OnCreateCommand(string sql)
    {
      return new SQLiteCommand(sql, SqLiteConnection, null);
    }

  }
}
