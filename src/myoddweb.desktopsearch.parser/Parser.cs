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
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Logging;

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

    public Parser(ILogger logger, IDirectory directory)
    {
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
      //if (!await ParseAllDirectoriesAsync(startFolder, token).ConfigureAwait(false))
      //{
      //  return false;
      //}

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
      var stopwatch = new Stopwatch();
      stopwatch.Start();
      var directoryCounter = 0;
      var parseDirectoryCounter = new Func<DirectoryInfo, bool>(( directoryInfo) =>
      {
        if (!ParseDirectory(directoryInfo))
        {
          return false;
        }

        ++directoryCounter;
        return true;
      });

      // parse the directory
      if (!await _directory.ParseDirectoriesAsync( _logger, startFolder, parseDirectoryCounter, token).ConfigureAwait(false))
      {
        _logger.Warning( "The parsing was cancelled");
        return false;
      }

      stopwatch.Stop();
      _logger.Information($"Parsed {directoryCounter} directories (Elapsed:{stopwatch.Elapsed:g})");

      return true;
    }

    /// <summary>
    /// Check if we are able to process this directlry.
    /// </summary>
    /// <param name="directoryInfo"></param>
    /// <returns></returns>
    public bool CanReadDirectory(DirectoryInfo directoryInfo)
    {
      try
      {
        var accessControlList = directoryInfo.GetAccessControl();

        var accessRules =
          accessControlList?.GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier));
        if (accessRules == null)
        {
          return false;
        }

        var readAllow = false;
        var readDeny = false;
        foreach (FileSystemAccessRule rule in accessRules)
        {
          if ((FileSystemRights.Read & rule.FileSystemRights) != FileSystemRights.Read){ continue;}

          switch (rule.AccessControlType)
          {
            case AccessControlType.Allow:
              readAllow = true;
              break;

            case AccessControlType.Deny:
              readDeny = true;
              break;
          }
        }

        return readAllow && !readDeny;
      }
      catch (UnauthorizedAccessException)
      {
        return false;
      }
      catch (SecurityException)
      {
        return false;
      }
      catch (Exception e)
      {
        _logger.Error(e.Message);
        return false;
      }
    }

    /// <summary>
    /// Check if we want to parse this directory or not.
    /// </summary>
    /// <param name="directoryInfo"></param>
    /// <returns></returns>
    private bool ParseDirectory(DirectoryInfo directoryInfo )
    {
      if (!CanReadDirectory(directoryInfo))
      {
        _logger.Warning($"Cannot Parse Directory: {directoryInfo.FullName}");
        return false;
      }

      // we can parse it.
      _logger.Verbose($"Parsing Directory: {directoryInfo.FullName}");

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
