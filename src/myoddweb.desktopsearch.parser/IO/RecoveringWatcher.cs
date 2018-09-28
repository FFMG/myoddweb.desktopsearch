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
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Logging;

namespace myoddweb.desktopsearch.parser.IO
{
  internal sealed class RecoveringWatcher
  {
    #region Delegates
    /// <summary>
    /// The file creator delegate
    /// </summary>
    private delegate IFileSystemEvent FileSystemEventCreator(FileSystemEventArgs e, ILogger l);

    /// <summary>
    /// File system raised event function.
    /// </summary>
    /// <param name="e"></param>
    public delegate void RaisedFileSystemEvent(IFileSystemEvent e );
    #endregion

    #region Member Variables
    /// <summary>
    /// The maximum number of tasks we want to run.
    /// </summary>
    private const int MaxNumberOfTasks = 64;

    /// <summary>
    /// The path we will be watching
    /// </summary>
    private readonly string _path;

    /// <summary>
    /// The File watcher internal bufer size
    /// </summary>
    private readonly int _internalBufferSize;

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The action called when a file/folder is renamed
    /// </summary>
    private readonly RaisedFileSystemEvent _renamed;

    /// <summary>
    /// The action called when a file/folder is changed.
    /// </summary>
    private readonly RaisedFileSystemEvent _changed;

    /// <summary>
    /// Action called when a file/folder is created
    /// </summary>
    private readonly RaisedFileSystemEvent _created;

    /// <summary>
    /// Action called when a file/folder is deleted.
    /// </summary>
    private readonly RaisedFileSystemEvent _deleted;

    /// <summary>
    /// Action called when there is an error.
    /// </summary>
    private readonly Action<Exception> _error;

    /// <summary>
    /// Our token source.
    /// </summary>
    private CancellationTokenSource _tokenSource;

    /// <summary>
    /// Monitor cancellations
    /// </summary>
    private CancellationTokenRegistration _register;

    /// <summary>
    /// The currently running task
    /// </summary>
    private Task _task;

    /// <summary>
    /// The currently running notifications.
    /// We make sure that the capacity has more than enough
    /// </summary>
    private readonly List<Task> _tasks = new List<Task>( 2 * MaxNumberOfTasks );

    /// <summary>
    /// The file creator
    /// </summary>
    private readonly FileSystemEventCreator _fileSystemEventCreator;

    /// <summary>
    /// The file watcher notifier.
    /// </summary>
    private readonly NotifyFilters _notifyFilters;
    #endregion

    private RecoveringWatcher
    (
      RaisedFileSystemEvent renamed,
      RaisedFileSystemEvent changed,
      RaisedFileSystemEvent created,
      RaisedFileSystemEvent deleted,
      Action<Exception> error,
      string path,
      FileSystemEventCreator func,
      NotifyFilters notifyFilters,
      int internalBufferSize,
      ILogger logger
    )
    {
      // watch files or folders?
      _fileSystemEventCreator = func ?? throw new ArgumentNullException(nameof(func));

      _notifyFilters = notifyFilters;
      
      // set the actions.
      _renamed = renamed ?? throw new ArgumentNullException(nameof(renamed));
      _changed = changed ?? throw new ArgumentNullException(nameof(changed));
      _created = created ?? throw new ArgumentNullException(nameof(created));
      _deleted = deleted ?? throw new ArgumentNullException(nameof(deleted));

      _error = error ?? throw new ArgumentNullException(nameof(error));

      // the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      // the internal buffer size
      _internalBufferSize = internalBufferSize;

      // set the path
      _path = path ?? throw new ArgumentNullException(nameof(path));
    }

    /// <summary>
    /// Start the file watcher.
    /// </summary>
    private void Start(CancellationToken token)
    {
      // stop
      Stop();

      // register for stop
      _register = new CancellationTokenRegistration();
      if (token.CanBeCanceled)
      {
        _register = token.Register(Stop);
      }

      // create a new token source.
      _tokenSource = new CancellationTokenSource();

      // start the long running thread
      _task = Task.Run(() => MonitorPath(_tokenSource.Token), _tokenSource.Token);
    }

    /// <summary>
    /// Stop the file watching
    /// </summary>
    public void Stop()
    {
      // stop the register. 
      _register.Dispose();

      // ask to cancel
      _tokenSource?.Cancel();

      // if we have a task running...wait for it.
      if (_task != null)
      {
        // and wait for it.
        helper.Wait.WaitAll(_task, _logger);

        // and wait for the tasks.
        helper.Wait.WaitAll(_tasks, _logger); 
      }
    }

    /// <summary>
    /// The main function that monitors for changes
    /// we are running in a while loop so we can catch any weeors thrown by FileSystemWatcher
    /// Unfortunately, that's the only way we can, safely, manage exceptions that are thrown.
    /// </summary>
    /// <param name="token"></param>
    private void MonitorPath(CancellationToken token)
    {
      // Keep going until the token is cancelled.
      while (!token.IsCancellationRequested)
      {
        Exception lastException = null;
        FileSystemWatcher watcher = null;
        try
        {
          // create the watcher and start monitoring.
          watcher = MonitorPath();

          // we call error ... but we do not care about the return value.
          watcher.Error += (sender, e) => lastException = e.GetException();

          // wait forever ... or until we cancel
          helper.Wait.Until(() =>
          {
            if (_tasks.Count > MaxNumberOfTasks)
            {
              _tasks.RemoveAll(t => t.IsCompleted);
            }
            return lastException != null || token.IsCancellationRequested;
          });

          // are we here because of an exception?
          if (lastException != null)
          {
            _error(lastException);
            lastException = null;
          }
        }
        catch (OperationCanceledException e)
        {
          // is it our token?
          if (e.CancellationToken != token)
          {
            _logger.Exception(e);
          }

          // we are done.
          return;
        }
        catch (Exception e)
        {
          _logger.Exception(e);
          _error(e);
        }
        finally
        {
          // wait for all the tasks to complete
          helper.Wait.WaitAll( _tasks, _logger, token );

          // we can now remove everything
          // as we know they are all complete.
          _tasks.Clear();

          // clean up the file watcher.
          if (null != watcher)
          {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
          }
        }
      }
    }

    private FileSystemWatcher MonitorPath()
    {
      // create the file syste, watcher.
      var watcher = new FileSystemWatcher
      {
        Path = _path,
        NotifyFilter = _notifyFilters,
        Filter = "*.*",
        IncludeSubdirectories = true,
        EnableRaisingEvents = true,
        InternalBufferSize = _internalBufferSize
      };

      watcher.Renamed += (sender, e) => _tasks.Add( Task.Run( () => _renamed( _fileSystemEventCreator(e, _logger))));
      watcher.Changed += (sender, e) => _tasks.Add( Task.Run(() => _changed ( _fileSystemEventCreator(e, _logger))));
      watcher.Created += (sender, e) => _tasks.Add( Task.Run(() => _created ( _fileSystemEventCreator(e, _logger))));
      watcher.Deleted += (sender, e) => _tasks.Add( Task.Run(() => _deleted ( _fileSystemEventCreator(e, _logger))));

      return watcher;
    }

    /// <summary>
    /// Start monitoring a given path
    /// </summary>
    /// <param name="renamed"></param>
    /// <param name="changed"></param>
    /// <param name="created"></param>
    /// <param name="deleted"></param>
    /// <param name="error"></param>
    /// <param name="path"></param>
    /// <param name="watchFolders"></param>
    /// <param name="internalBufferSize"></param>
    /// <param name="logger"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private static RecoveringWatcher StartWatcher
    (
      RaisedFileSystemEvent renamed,
      RaisedFileSystemEvent changed,
      RaisedFileSystemEvent created,
      RaisedFileSystemEvent deleted,
      Action<Exception> error,
      string path,
      bool watchFolders,
      int internalBufferSize,
      ILogger logger,
      CancellationToken token
    )
    {
      IFileSystemEvent Lambda(FileSystemEventArgs e, ILogger l) => (watchFolders ? new DirectorySystemEvent(e, l) : new FileSystemEvent(e, l));
      var notifier = watchFolders ? NotifyFilters.DirectoryName : NotifyFilters.FileName | NotifyFilters.LastWrite;

      // create the watcher
      var watcher = new RecoveringWatcher
      (
        renamed, 
        changed, 
        created, 
        deleted, 
        error, 
        path,
        Lambda,
        notifier, 
        internalBufferSize, 
        logger
      );

      // start it
      watcher.Start(token);

      // return it...
      return watcher;
    }

    /// <summary>
    /// Start monitoring a given path
    /// </summary>
    /// <param name="renamed"></param>
    /// <param name="changed"></param>
    /// <param name="created"></param>
    /// <param name="deleted"></param>
    /// <param name="error"></param>
    /// <param name="path"></param>
    /// <param name="internalBufferSize"></param>
    /// <param name="logger"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public static RecoveringWatcher StartFileWatcher
    (
      RaisedFileSystemEvent renamed,
      RaisedFileSystemEvent changed,
      RaisedFileSystemEvent created,
      RaisedFileSystemEvent deleted,
      Action<Exception> error,
      string path,
      int internalBufferSize,
      ILogger logger,
      CancellationToken token
    )
    {
      // start a file watcher
      return StartWatcher( renamed, changed, created, deleted, error, path, false, internalBufferSize, logger, token );
    }

    /// <summary>
    /// Start monitoring a given path
    /// </summary>
    /// <param name="renamed"></param>
    /// <param name="changed"></param>
    /// <param name="created"></param>
    /// <param name="deleted"></param>
    /// <param name="error"></param>
    /// <param name="path"></param>
    /// <param name="internalBufferSize"></param>
    /// <param name="logger"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public static RecoveringWatcher StartFolderWatcher
    (
      RaisedFileSystemEvent renamed,
      RaisedFileSystemEvent changed,
      RaisedFileSystemEvent created,
      RaisedFileSystemEvent deleted,
      Action<Exception> error,
      string path,
      int internalBufferSize,
      ILogger logger,
      CancellationToken token
    )
    {
      // start a folder watcher
      return StartWatcher(renamed, changed, created, deleted, error, path, true, internalBufferSize, logger, token);
    }
  }
}
