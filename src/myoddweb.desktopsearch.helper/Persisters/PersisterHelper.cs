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
using System.Data;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.helper.Persisters
{
  internal class PersisterHelper : IDisposable
  {
    protected IConnectionFactory Factory { get; }

    protected string Sql { get; }

    /// <summary>
    /// The async await lock
    /// </summary>
    protected Lock.Lock Lock { get; } = new Lock.Lock();

    /// <summary>
    /// The insert command;
    /// </summary>
    private IDbCommand _command;

    /// <summary>
    /// Create the command on request.
    /// </summary>
    protected IDbCommand Command
    {
      get
      {
        if (_command != null)
        {
          return _command;
        }

        _command = Factory.CreateCommand(Sql);
        return _command;
      }
    }

    protected PersisterHelper(IConnectionFactory factory, string sql)
    {
      Factory = factory ?? throw new ArgumentNullException(nameof(factory));
      Sql = sql ?? throw new ArgumentNullException(nameof(sql));
    }

    public virtual void Dispose()
    {
      _command?.Dispose();
    }
  }

  internal class MultiplePersisterHelper<T> where T : PersisterHelper, IDisposable
  {
    private readonly IList<T> _ph = new List<T>();

    private readonly Random _rnd = new Random();

    public MultiplePersisterHelper( Func<T> func, int count )
    {
      for (var i = 0; i < count; i++)
      {
        Add( func() );
      }
    }

    protected MultiplePersisterHelper()
    {
    }

    public T Next()
    {
      var r = _rnd.Next(_ph.Count);
      return _ph[r];
    }

    public void Add(T ph)
    {
      _ph.Add(ph);
    }

    public virtual void Dispose()
    {
      foreach (var persisterHelper in _ph)
      {
        persisterHelper.Dispose();
      }
      _ph.Clear();
    }
  }
}
