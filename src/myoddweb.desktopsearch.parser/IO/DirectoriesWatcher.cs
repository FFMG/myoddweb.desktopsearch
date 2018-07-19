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
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Logging;

namespace myoddweb.desktopsearch.parser.IO
{
  internal class DirectoriesWatcher : Watcher
  {
    public DirectoriesWatcher(DirectoryInfo folder, ILogger logger, SystemEventsParser parser) :
      base( WatcherTypes.Directories, folder, logger, parser)
    {
      ErrorAsync += OnFolderErrorAsync;
      ChangedAsync += OnDirectoryTouchedAsync;
      RenamedAsync += OnDirectoryTouchedAsync;
      CreatedAsync += OnDirectoryTouchedAsync;
      DeletedAsync += OnDirectoryTouchedAsync;
    }

    #region Directories Events
    /// <summary>
    /// When the file watcher errors out.
    /// </summary>
    /// <param name="e"></param>
    /// <param name="token"></param>
    private Task OnFolderErrorAsync(Exception e, CancellationToken token)
    {
      // the watcher raised an error
      Logger.Error($"File watcher error: {e.Message}");
      return Task.CompletedTask;
    }

    /// <summary>
    /// When a directory has been changed.
    /// </summary>
    /// <param name="e"></param>
    /// <param name="token"></param>
    private async Task OnDirectoryTouchedAsync(IFileSystemEvent e, CancellationToken token)
    {
      // It is posible that the event parser has not started yet.
      await Task.Run(() => EventsParser.Add(e), token).ConfigureAwait(false);
    }
    #endregion

    #region Cancel
    /// <inheritdoc/>
    protected override void OnCancelling()
    {
      Logger.Verbose($"Stopping Directories watcher : {Folder}");
    }

    /// <inheritdoc/>
    protected override void OnCancelled()
    {
      Logger.Verbose($"Done Directories watcher : {Folder}");
    }
    #endregion
  }
}
