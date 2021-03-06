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
using System.IO;
using myoddweb.desktopsearch.interfaces.Enums;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  public class PendingFileUpdate : IPendingFileUpdate
  {
    /// <summary>
    /// The folder id with a pending update
    /// </summary>
    public long FileId { get; }

    /// <summary>
    /// Get the file info.
    /// </summary>
    public FileInfo File { get; }

    /// <summary>
    /// The pending update type.
    /// </summary>
    public UpdateType PendingUpdateType { get; }

    public PendingFileUpdate(long fileId, FileInfo file, UpdateType pendingUpdateType)
    {
      // set the file id.
      FileId = fileId;

      // the file.
      File = file;

      // the pending update type.
      PendingUpdateType = pendingUpdateType;
    }

    /// <summary>
    /// Copy contructor.
    /// </summary>
    /// <param name="pu"></param>
    public PendingFileUpdate(IPendingFileUpdate pu) : this(pu.FileId, pu.File, pu.PendingUpdateType)
    {
    }
  }
}
