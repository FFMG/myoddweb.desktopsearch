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
using System.IO;
using System.Linq;
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
  internal class FilesSystemEventsParser : SystemEventsParser
  {
    /// <summary>
    /// The folders interface.
    /// </summary>
    private IFiles Files { get; }

    public FilesSystemEventsParser( IPersister persister, IDirectory directory, int eventsParserMs, ILogger logger) :
      this(persister.Folders.Files, persister, directory, eventsParserMs, logger)
    {
    }

    public FilesSystemEventsParser(IFiles files, IPersister persister, IDirectory directory, int eventsParserMs, ILogger logger) :
      base(persister, directory, eventsParserMs, logger)
    {
      Files = files;
    }

    #region Process File events
    /// <summary>
    /// Check if we can process this file or not.
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    private bool CanProcessFile(FileInfo file)
    {
      if (null == file)
      {
        return false;
      }

      // it is a file that we can read?
      // we do not check delted files as they are ... deleted.
      if (!helper.File.CanReadFile(file))
      {
        return false;
      }

      // do we monitor this directory?
      return !Directory.IsIgnored( file);
    }
    #endregion

    #region Abstract Process events
    /// <inheritdoc />
    protected override async Task ProcessAddedAsync(IConnectionFactory factory, directorywatcher.interfaces.IFileSystemEvent e, CancellationToken token)
    {
      var file = e.FileSystemInfo as FileInfo;
      if (!CanProcessFile(file))
      {
        return;
      }

      // the given file is going to be processed.
      Logger.Verbose($"File: {e.FullName} (Created)");

      // just add the file.
      await Files.AddOrUpdateFileAsync( file, factory, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task ProcessRemovedAsync(IConnectionFactory factory, directorywatcher.interfaces.IFileSystemEvent e, CancellationToken token)
    {
      if (!(e.FileSystemInfo is FileInfo file))
      {
        return;
      }

      // we cannot call CanProcessFile as it is now deleted.
      if (Directory.IsIgnored(file))
      {
        return;
      }

      // the given file is going to be processed.
      Logger.Verbose($"File: {e.FullName} (Deleted)");

      // do we have the file on record?
      if (await Files.FileExistsAsync(file, factory, token).ConfigureAwait(false))
      {
        // just delete the folder.
        await Files.DeleteFileAsync(file, factory, token).ConfigureAwait(false);
      }
    }

    /// <inheritdoc />
    protected override async Task ProcessTouchedAsync(IConnectionFactory factory, directorywatcher.interfaces.IFileSystemEvent e, CancellationToken token)
    {
      var file = e.FileSystemInfo as FileInfo;
      if (!CanProcessFile(file))
      {
        return;
      }

      // the given file is going to be processed.
      Logger.Verbose($"File: {e.FullName} (Changed)");

      // just add the file.
      await Files.AddOrUpdateFileAsync(file, factory, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task ProcessRenamedAsync(IConnectionFactory factory, IRenamedFileSystemEvent e, CancellationToken token)
    {
      // get the new name as well as the one one
      // either of those could be null
      var file = e.FileSystemInfo as FileInfo;
      var oldFile = e.PreviousFileSystemInfo as FileInfo;

      // if both are null then we cannot do anything with it
      if (null == file && null == oldFile)
      {
        Logger.Error($"I was unable to use the renamed files, (old:{e.PreviousFullName} / new:{e.FullName})");
        return;
      }

      // if the old directory is null then we can use 
      // the new directory only.
      if (null == oldFile)
      {
        // but of course, only if it is usable as well...
        if (!CanProcessFile(file))
        {
          return;
        }

        // just add the new directly.
        if (!await Files.AddOrUpdateFileAsync(file, factory, token).ConfigureAwait(false))
        {
          Logger.Error( $"Unable to add file {e.FullName} during rename.");
        }
        return;
      }

      // so we now know that the old file is not null
      // so the new directory could be null and/or not usubale.
      // so, if we cannot use it, we will simply delete the old one.
      if (!CanProcessFile(file))
      {
        // if the old path is not an ignored path
        // then we might be able to delete that file.
        if (Directory.IsIgnored(oldFile))
        {
          return;
        }

        // if the old file does not exist on record ... then there is nothing more for us to do .
        if (!await Files.FileExistsAsync(oldFile, factory, token).ConfigureAwait(false))
        {
          return;
        }

        // delete the old folder only, in case it did exist.
        if (!await Files.DeleteFileAsync(oldFile, factory, token).ConfigureAwait(false))
        {
          Logger.Error( $"Unable to remove old file {e.PreviousFullName} durring rename");
        }
        return;
      }

      // the given file is going to be processed.
      Logger.Verbose($"File: {e.PreviousFullName} > {e.FullName} (Renamed)");

      // at this point we know we have a new file that we can use
      // and an old file that we could also use.
      //
      // if we do not have the old file on record then it is not a rename
      // but rather is is a new one.
      if (!await Files.FileExistsAsync(oldFile, factory, token).ConfigureAwait(false))
      {
        // just add the new directly.
        if (!await Files.AddOrUpdateFileAsync(file, factory, token).ConfigureAwait(false))
        {
          Logger.Error($"Unable to add file {e.FullName} during rename.");
        }
        return;
      }

      // we have the old name on record so we can try and rename it.
      // if we ever have an issue, it could be because we are trying to rename to
      // something that already exists.
      if (-1 == await Files.RenameOrAddFileAsync(file, oldFile, factory, token).ConfigureAwait(false))
      {
        Logger.Error( $"Unable to rename file {e.PreviousFullName} > {e.FullName}");
      }
    }
    #endregion
  }
}
