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
  internal class FilesWatcher : Watcher
  {
    public FilesWatcher(DirectoryInfo folder, ILogger logger, SystemEventsParser parser) : 
      base( WatcherTypes.Files, folder, logger, parser)
    {
      ErrorAsync += OnFolderErrorAsync;
      ChangedAsync += OnFileTouchedAsync;
      RenamedAsync += OnFileTouchedAsync;
      CreatedAsync += OnFileTouchedAsync;
      DeletedAsync += OnFileTouchedAsync;
    }

    #region Files Events
    /// <summary>
    /// When the file watcher errors out.
    /// </summary>
    /// <param name="e"></param>
    /// <param name="token"></param>
    private Task OnFolderErrorAsync(Exception e, CancellationToken token)
    {
      // the watcher raised an error
      Logger.Error($"File watcher error: {e.Message} (File)");
      return Task.CompletedTask;
    }

    /// <summary>
    /// When a file was changed
    /// </summary>
    /// <param name="e"></param>
    /// <param name="token"></param>
    private Task OnFileTouchedAsync(IFileSystemEvent e, CancellationToken token)
    {
      // It is posible that the event parser has not started yet.
      return Task.Run(() => EventsParser.Add(e), token);
    }
    #endregion

    #region Cancel
    /// <inheritdoc/>
    protected override void OnCancelling()
    {
      Logger.Verbose($"Stopping Files watcher : {Folder}");
    }

    /// <inheritdoc/>
    protected override void OnCancelled()
    {
      Logger.Verbose($"Done Files watcher : {Folder}");
    }
    #endregion
  }
}
