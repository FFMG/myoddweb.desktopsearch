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
using System.Threading;
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.interfaces.Persisters
{
  public interface IPersister
  {
    /// <summary>
    /// Configuration
    /// </summary>
    IConfig Config { get; }

    /// <summary>
    /// The various counters.
    /// </summary>
    ICounts Counts { get; }

    /// <summary>
    /// The words persister
    /// </summary>
    IWords Words { get; }

    /// <summary>
    /// The words in a file.
    /// </summary>
    IFilesWords FilesWords { get; }

    /// <summary>
    /// The folders interface
    /// </summary>
    IFolders Folders { get; }

    /// <summary>
    /// The parts manager for a word.
    /// </summary>
    IWordsParts WordsParts { get; }

    /// <summary>
    /// The table that contains the words of a file being parsed.
    /// </summary>
    IParserWords ParserWords { get; }

    /// <summary>
    /// Get a database transaction.
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<IConnectionFactory> BeginWrite( CancellationToken token );

    /// <summary>
    /// Get a database readonly transaction.
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<IConnectionFactory> BeginRead(CancellationToken token);

    /// <summary>
    /// Rollback the current transaction.
    /// </summary>
    /// <paramref name="connectionFactory"/>
    /// <returns></returns>
    bool Rollback(IConnectionFactory connectionFactory);

    /// <summary>
    /// Commit the current transaction.
    /// </summary>
    /// <paramref name="connectionFactory"/>
    /// <returns></returns>
    bool Commit(IConnectionFactory connectionFactory);

    /// <summary>
    /// Start the database work.
    /// </summary>
    /// <param name="token"></param>
    void Start(CancellationToken token);

    /// <summary>
    /// Stop the database work.
    /// </summary>
    void Stop();
  }
}