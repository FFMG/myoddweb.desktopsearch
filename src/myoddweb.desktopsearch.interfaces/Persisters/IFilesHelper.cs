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
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.interfaces.Persisters
{
  public interface IFilesHelper : IDisposable
  {
    /// <summary>
    /// Get the id of all the files in a folder.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<IList<IFileHelper>> GetAsync( long id, CancellationToken token);

    /// <summary>
    /// Get the id of a file for a folder/name
    /// </summary>
    /// <param name="id">The folder id</param>
    /// <param name="name">The file name, (case insensitive)</param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<long> GetAsync(long id, string name, CancellationToken token);

    /// <summary>
    /// Delete a single file
    /// </summary>
    /// <param name="id">The folder id</param>
    /// <param name="name">The file name, (case insensitive)</param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<bool> DeleteAsync(long id, string name, CancellationToken token);

    /// <summary>
    /// Delete all the files linked to folder.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<int> DeleteAsync(long id, CancellationToken token);

    /// <summary>
    /// Insert a file ... and get the id of it.
    /// </summary>
    /// <param name="id">The folder id</param>
    /// <param name="name">The file name, (case insensitive)</param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<long> InsertAndGetAsync(long id, string name, CancellationToken token);

    /// <summary>
    /// Rename a file and return the id of the file
    /// If the file _already_ exists, we will delete the old one
    /// And return the existing value
    /// It will be up to the caller to replace the corresponding links.
    /// </summary>
    /// <param name="newFolderId"></param>
    /// <param name="newName"></param>
    /// <param name="oldFolerId"></param>
    /// <param name="oldName"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<long> RenameAsync( long newFolderId, string newName, long oldFolerId, string oldName, CancellationToken token);
  }
}