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
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Logging;

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

    public FileSystemEvent(FileSystemEventArgs e, ILogger logger ) : this( e, false, logger )
    {
    }

    protected FileSystemEvent(FileSystemEventArgs e, bool isDirectory, ILogger logger)
    {
      // we must have an argument
      if (null == e)
      {
        throw new ArgumentNullException(nameof(e));
      }

      // set if this is a directory or not.
      IsDirectory = isDirectory;

      // the change type.
      ChangeType = e.ChangeType;

      // The file and directory must never be null
      SetFileAndDirectory(e.FullPath);
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
    /// <param name="path"></param>
    private void SetFileAndDirectory( string path )
    {
      if (IsDirectory)
      {
        File = null;
        Directory = new DirectoryInfo(path);
        return;
      }

      File = new FileInfo(path);
      Directory = File.Directory;
    }

    /// <summary>
    /// Given a path, set the old file/directory
    /// </summary>
    /// <param name="path"></param>
    private void SetOldFileAndDirectory(string path)
    {
      if (IsDirectory)
      {
        OldFile = null;
        OldDirectory = new DirectoryInfo(path);
        return;
      }

      OldFile = new FileInfo(path);
      OldDirectory = File.Directory;
    }

    /// <summary>
    /// Process the rename events and set the values accordingly.
    /// </summary>
    /// <param name="e"></param>
    /// <param name="logger"></param>
    private void ProcessRenameEvent(FileSystemEventArgs e, ILogger logger)
    {
      if (!Is(WatcherChangeTypes.Renamed))
      {
        return;
      }

      if (e is RenamedEventArgs renameEvent)
      {
        try
        {
          // there are some cases where we get a rename event with no old name
          // so we cannot really delete the old one
          // so we will just fire as if it was a new one
          // @see https://referencesource.microsoft.com/#system/services/io/system/io/FileSystemWatcher.cs
          // with an explaination of what could have happened.
          if (renameEvent.OldName == null)
          {
            logger.Warning($"Received a 'rename' event without an old file name, so processing event as a new one, {e.FullPath}");
            ChangeType &= ~WatcherChangeTypes.Renamed;
            ChangeType |= WatcherChangeTypes.Created;
          }
          else if (renameEvent.Name == null)
          {
            // if we got an old name without a new name
            // then we cannot really rename anything at all.
            // all we can do is remove the old one.
            logger.Warning($"Received a 'rename' event without a new file name, so processing event as delete old one, {renameEvent.OldFullPath}");
            SetFileAndDirectory(renameEvent.OldFullPath);
            ChangeType &= ~WatcherChangeTypes.Renamed;
            ChangeType |= WatcherChangeTypes.Deleted;
          }
          else
          {
            // we have both a new and old name...
            SetOldFileAndDirectory(renameEvent.OldFullPath);
          }
        }
        catch
        {
          logger.Error($"There was an error trying to rename {renameEvent.OldFullPath} to {renameEvent.FullPath}!");
          throw;
        }
      }
      else
      {
        logger.Warning($"A file, ({e.FullPath}), was marked as renamed, but the event was not.");
        ChangeType &= ~WatcherChangeTypes.Renamed;
        ChangeType |= WatcherChangeTypes.Changed;
      }
    }
  }
}
