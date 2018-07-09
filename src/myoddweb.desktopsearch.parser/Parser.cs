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
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.parser
{
  public class Parser
  {
    /// <summary>
    /// The file watcher
    /// </summary>
    private FileWatcher _watcher;

    /// <summary>
    /// The logger that we will be using to log messages.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The directory parser we will be using.
    /// </summary>
    private readonly IDirectory _directory;

    /// <summary>
    /// The persister.
    /// </summary>
    private readonly IPersister _perister;

    public Parser( IPersister persister, ILogger logger, IDirectory directory)
    {
      // set the persister.
      _perister = persister ?? throw new ArgumentNullException(nameof(persister));

      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      // save the directory parser
      _directory = directory ?? throw new ArgumentNullException(nameof(directory));
    }

    /// <summary>
    /// Start parsing.
    /// </summary>
    public void Start( CancellationToken token )
    {
      var thread = new Thread(async () => await WorkAsync(token).ConfigureAwait(false));
      thread.Start();
      _logger.Information("Parser started");
    }

    /// <summary>
    /// Do all the parsing work,
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> WorkAsync(CancellationToken token)
    {
      const string startFolder = "c:\\";
      // first we get a full list of files/directories.
      if (!await ParseAllDirectoriesAsync(startFolder, token).ConfigureAwait(false))
      {
        return false;
      }

      // we then watch for files/folder changes.
      if( !StartWatcher(startFolder))
      {
        return false;
      }

      return true;
    }

    private void StopWatcher()
    {
      // are we watching?
      _watcher?.Stop();
      _watcher = null;
    }

    /// <summary>
    /// Start to watch all the folders and sub folders.
    /// </summary>
    /// <param name="startFolder"></param>
    /// <returns></returns>
    private bool StartWatcher(string startFolder)
    {
      _watcher = new FileWatcher(startFolder);
      _watcher.Error += OnFolderError;
      _watcher.Changed += OnFolderChanged;
      _watcher.Renamed += OnFolderRenamed;
      _watcher.Created += OnFolderCreated;
      _watcher.Deleted += OnFolderDeleted;

      _watcher.Start();
      return true;
    }

    /// <summary>
    /// When the file watcher errors out.
    /// </summary>
    /// <param name="e"></param>
    private void OnFolderError( ErrorEventArgs e)
    {
      // the watcher raised an error
      _logger.Error( $"File watcher error: {e.GetException().Message}");

      // we have to stop the watcher
      StopWatcher();
    }

    /// <summary>
    /// When a file/folder has been renamed.
    /// </summary>
    /// <param name="e"></param>
    private void OnFolderRenamed( RenamedEventArgs e)
    {
      _logger.Verbose($"File/Folder: {e.OldFullPath} to {e.FullPath} ({e.ChangeType})");
    }

    /// <summary>
    /// When a file/folder has been changed.
    /// </summary>
    /// <param name="e"></param>
    private void OnFolderChanged( FileSystemEventArgs e)
    {
      _logger.Verbose( $"File/Folder: {e.FullPath} ({e.ChangeType})");
    }

    /// <summary>
    /// When a file/folder has been created.
    /// </summary>
    /// <param name="e"></param>
    private void OnFolderCreated( FileSystemEventArgs e)
    {
      _logger.Verbose($"File/Folder: {e.FullPath} ({e.ChangeType})");
    }

    /// <summary>
    /// When a file/folder has been deleted.
    /// </summary>
    /// <param name="e"></param>
    private void OnFolderDeleted(FileSystemEventArgs e)
    {
      _logger.Verbose($"File/Folder: {e.FullPath} ({e.ChangeType})");
    }

    /// <summary>
    /// Parse all the directories.
    /// </summary>
    /// <param name="startFolder"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> ParseAllDirectoriesAsync(string startFolder, CancellationToken token)
    {
      try
      {
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var directoriesParser = new DirectoriesParser(startFolder, _logger, _directory);
        await directoriesParser.SearchAsync(token).ConfigureAwait(false);

        // process all the files.
        if (!await ParseDirectoryAsync(directoriesParser.Directories, token).ConfigureAwait(false))
        {
          _logger.Warning($"Parsing was cancelled (Elapsed:{stopwatch.Elapsed:g})");
          return false;
        }

        // stop the watch and log how many items we found.
        stopwatch.Stop();
        _logger.Information($"Parsed {directoriesParser.Directories.Count} directories (Elapsed:{stopwatch.Elapsed:g})");
        return true;
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        return false;
      }
    }

    /// <summary>
    /// Check if we want to parse this directory or not.
    /// </summary>
    /// <param name="directories"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> ParseDirectoryAsync(IEnumerable<DirectoryInfo> directories, CancellationToken token )
    {
      // add the folder to the list.
      if (!await _perister.AddOrUpdateFoldersAsync(directories, token).ConfigureAwait(false))
      {
        return false;
      }

      // we always parse sub directories
      return true;
    }

    /// <summary>
    /// Process a file that has been found.
    /// </summary>
    /// <param name="fileInfo"></param>
    private void ProcessFile(FileSystemInfo fileInfo )
    {
      _logger.Verbose( $"Processing File: {fileInfo.FullName}");
    }

    /// <summary>
    /// Stop parsing
    /// </summary>
    public void Stop()
    {
      StopWatcher();
    }
  }
}
