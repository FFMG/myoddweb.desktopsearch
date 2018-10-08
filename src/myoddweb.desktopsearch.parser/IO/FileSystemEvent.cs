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
using System.IO;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.directorywatcher.interfaces;
using IFileSystemEvent = myoddweb.desktopsearch.interfaces.IO.IFileSystemEvent;

namespace myoddweb.desktopsearch.parser.IO
{
  internal class FileSystemEvent : IFileSystemEvent
  {
    /// <inheritdoc />
    public WatcherChangeTypes ChangeType { get; protected set;}

    /// <inheritdoc />
    public bool IsDirectory { get; }

    /// <inheritdoc />
    public DirectoryInfo Directory { get; protected set; }

    /// <inheritdoc />
    public FileInfo File { get; protected set; }

    /// <inheritdoc />
    public DirectoryInfo OldDirectory { get; protected set; }

    /// <inheritdoc />
    public FileInfo OldFile { get; protected set; }

    /// <inheritdoc />
    public string FullName => IsDirectory ? Directory.FullName : File.FullName;

    /// <inheritdoc />
    public string OldFullName => IsDirectory ? OldDirectory.FullName : OldFile.FullName;

    public FileSystemEvent(directorywatcher.interfaces.IFileSystemEvent e, ILogger logger ) : this( e, false, logger )
    {
    }

    protected FileSystemEvent(directorywatcher.interfaces.IFileSystemEvent e, bool isDirectory, ILogger logger)
    {
      // we must have an argument
      if (null == e)
      {
        throw new ArgumentNullException(nameof(e));
      }

      // set if this is a directory or not.
      IsDirectory = isDirectory;

      // the change type.
      switch (e.Action)
      {
        case EventAction.Added:
          ChangeType = WatcherChangeTypes.Created;
          break;
        case EventAction.Removed:
          ChangeType = WatcherChangeTypes.Deleted;
          break;
        case EventAction.Touched:
          ChangeType = WatcherChangeTypes.Changed;
          break;
        case EventAction.Renamed:
          ChangeType = WatcherChangeTypes.Renamed;
          break;

        case EventAction.Error:
        case EventAction.ErrorMemory:
        case EventAction.ErrorOverflow:
        case EventAction.ErrorAborted:
        case EventAction.ErrorCannotStart:
        case EventAction.ErrorAccess:
        case EventAction.Unknown:
        default:
          throw new ArgumentOutOfRangeException();
      }

      // The file and directory must never be null
      SetFileAndDirectory(e.FileSystemInfo);
      ProcessRenameEvent(e, logger);
    }

    /// <inheritdoc />
    public bool Is( WatcherChangeTypes type)
    {
      return (ChangeType & type) == type;
    }

    /// <summary>
    /// Given a path, set the file/directory
    /// </summary>
    /// <param name="fsi"></param>
    private void SetFileAndDirectory( FileSystemInfo fsi )
    {
      if (fsi is DirectoryInfo)
      {
        File = null;
        Directory = new DirectoryInfo(fsi.FullName );
        return;
      }

      File = new FileInfo(fsi.FullName);
      Directory = File.Directory;
    }

    /// <summary>
    /// Given a path, set the old file/directory
    /// </summary>
    /// <param name="fsi"></param>
    private void SetOldFileAndDirectory(FileSystemInfo fsi)
    {
      if (fsi is DirectoryInfo)
      {
        OldFile = null;
        OldDirectory = new DirectoryInfo(fsi.FullName);
        return;
      }

      OldFile = new FileInfo(fsi.FullName);
      OldDirectory = File.Directory;
    }

    /// <summary>
    /// Process the rename events and set the values accordingly.
    /// </summary>
    /// <param name="e"></param>
    /// <param name="logger"></param>
    private void ProcessRenameEvent(directorywatcher.interfaces.IFileSystemEvent e, ILogger logger)
    {
      if (!Is(WatcherChangeTypes.Renamed))
      {
        return;
      }

      if (e is IRenamedFileSystemEvent renameEvent)
      {
        try
        {
          // we have both a new and old name...
          SetOldFileAndDirectory(renameEvent.FileSystemInfo );
        }
        catch
        {
          logger.Error($"There was an error trying to rename {renameEvent.PreviousFileSystemInfo.FullName} to {renameEvent.FileSystemInfo.FullName}!");
          throw;
        }
      }
      else
      {
        logger.Warning($"A file, ({e.FileSystemInfo.FullName}), was marked as renamed, but the event was not.");
        ChangeType &= ~WatcherChangeTypes.Renamed;
        ChangeType |= WatcherChangeTypes.Changed;
      }
    }
  }
}
