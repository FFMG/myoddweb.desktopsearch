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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Enums;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;
using myoddweb.directorywatcher.interfaces;

namespace myoddweb.desktopsearch.parser.IO
{
  /// <inheritdoc />
  internal class DirectoriesSystemEventsParser : SystemEventsParser
  {
    /// <summary>
    /// The folders interface.
    /// </summary>
    private IFolders Folders { get; }

    public DirectoriesSystemEventsParser( IPersister persister, IDirectory directory, int eventsParserMs, ILogger logger) :
      this( persister.Folders, persister, directory, eventsParserMs, logger)
    {
    }

    public DirectoriesSystemEventsParser(IFolders folders, IPersister persister, IDirectory directory, int eventsParserMs, ILogger logger) :
      base(persister, directory, eventsParserMs, logger)
    {
      Folders = folders;
    }

    #region Process Directory events
    /// <summary>
    /// Check if we can process this directory or not.
    /// </summary>
    /// <param name="directory"></param>
    /// <returns></returns>
    private bool CanProcessDirectory(DirectoryInfo directory)
    {
      // it is a directory that we can read?
      if (!helper.File.CanReadDirectory(directory))
      {
        return false;
      }

      // do we monitor this directory?
      return !Directory.IsIgnored(directory);
    }
    #endregion

    #region Abstract Process events
    /// <inheritdoc />
    protected override async Task ProcessAddedAsync(IConnectionFactory factory, directorywatcher.interfaces.IFileSystemEvent e, CancellationToken token)
    {
      var directory = e.FileSystemInfo as DirectoryInfo;
      if (!CanProcessDirectory(directory))
      {
        return;
      }

      // the given file is going to be processed.
      Logger.Verbose($"Directory event: {directory?.FullName} (Created)");

      // just add the directory.
      await Folders.AddOrUpdateDirectoryAsync(directory, factory, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task ProcessRemovedAsync(IConnectionFactory factory, directorywatcher.interfaces.IFileSystemEvent e, CancellationToken token)
    {
      var directory = e.FileSystemInfo as DirectoryInfo;
      if (null == directory)
      {
        return;
      }

      // we cannot call CanProcessDirectory as it is now deleted.
      if (Directory.IsIgnored( directory ))
      {
        return;
      }

      // the given file is going to be processed.
      Logger.Verbose($"Directory event: {directory.FullName} (Deleted)");

      // do we have the directory on record?
      if (await Folders.DirectoryExistsAsync(directory, factory, token).ConfigureAwait(false))
      {
        // just delete the directory.
        await Folders.DeleteDirectoryAsync(directory, factory, token).ConfigureAwait(false);
      }
    }

    /// <inheritdoc />
    protected override async Task ProcessTouchedAsync(IConnectionFactory factory, directorywatcher.interfaces.IFileSystemEvent e, CancellationToken token)
    {
      var directory = e.FileSystemInfo as DirectoryInfo;
      if (!CanProcessDirectory(directory))
      {
        return;
      }

      // we know the directory changed ... but does it even exist on our record?
      if (!await Folders.DirectoryExistsAsync(directory, factory, token).ConfigureAwait(false))
      {
        for (;;)
        {
          // add the directory
          Logger.Verbose($"Directory event: {e.FullName} (Changed - But not on file)");

          // just add the directory.
          await Folders.AddOrUpdateDirectoryAsync(directory, factory, token).ConfigureAwait(false);

          // get the parent directory ... if there is one.
          directory = directory?.Parent;
          if (null == directory)
          {
            break;
          }

          // if the new directory exists then we do not need to worry any further.
          if (await Folders.DirectoryExistsAsync(directory, factory, token).ConfigureAwait(false))
          {
            break;
          }
        }
        return;
      }

      // the given directory is going to be processed.
      Logger.Verbose($"Directory event: {e.FullName} (Changed)");

      // then make sure to touch the folder accordingly
      await Folders.FolderUpdates.TouchDirectoriesAsync( new []{directory}, UpdateType.Changed, factory, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task ProcessRenamedAsync(IConnectionFactory factory, IRenamedFileSystemEvent e, CancellationToken token)
    {
      // get the new name as well as the one one
      // either of those could be null
      var directory = e.FileSystemInfo as DirectoryInfo;
      var oldDirectory = e.PreviousFileSystemInfo as DirectoryInfo;

      // if both are null then we cannot do anything with it
      if (null == directory && null == oldDirectory)
      {
        Logger.Error($"Directory event: I was unable to use the renamed drectories, (old:{e.FullName} / new:{e.FullName})");
        return;
      }

      // if the old directory is null then we can use 
      // the new directory only.
      if (null == oldDirectory)
      {
        // but of course, only if it is usable as well...
        if (!CanProcessDirectory(directory))
        {
          return;
        }

        // just add the new directly.
        if (!await Folders.AddOrUpdateDirectoryAsync(directory, factory, token).ConfigureAwait(false))
        {
          Logger.Error($"Directory event: Unable to add directory {e.FullName} during rename.");
        }
        return;
      }

      // so we now know that the old directory is not null
      // so the new directory could be null or not usubale.
      // so, if we cannot use it, we will simply delete the old one.
      if (!CanProcessDirectory(directory))
      {
        // delete the old folder only, in case it did exist.
        if (!await Folders.DeleteDirectoryAsync(oldDirectory, factory, token).ConfigureAwait(false))
        {
          Logger.Error($"Directory event: Unable to remove old file {e.PreviousFullName} durring rename");
        }
        return;
      }

      // the given directory is going to be processed.
      Logger.Verbose($"Directory event: {e.PreviousFullName} > {e.FullName} (Renamed)");

      // at this point we know we have a new directory that we can use
      // and an old directory that we could also use.
      //
      // if we do not have the old directory on record then it is not a rename
      // but rather is is a new one.
      if (!await Folders.DirectoryExistsAsync(oldDirectory, factory, token).ConfigureAwait(false))
      {
        // just add the new directly.
        if (!await Folders.AddOrUpdateDirectoryAsync( directory, factory, token).ConfigureAwait(false))
        {
          Logger.Error($"Directory event: Unable to add directory {e.FullName} during rename.");
        }
        return;
      }

      // we have the old name on record so we can try and rename it.
      // if we ever have an issue, it could be because we are trying to rename to
      // something that already exists.
      if (-1 == await Folders.RenameOrAddDirectoryAsync( directory, oldDirectory, factory, token).ConfigureAwait(false))
      {
        Logger.Error($"Directory event: Unable to rename directory {e.PreviousFullName} > {e.FullName}");
      }
    }
    #endregion
  }
}
