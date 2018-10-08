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
    private delegate IFileSystemEvent FileSystemEventCreator(directorywatcher.interfaces.IFileSystemEvent e, ILogger l);

    /// <summary>
    /// File system raised event function.
    /// </summary>
    /// <param name="e"></param>
    public delegate Task RaisedFileSystemEvent(directorywatcher.interfaces.IFileSystemEvent e);
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
    #endregion

    private RecoveringWatcher
    (
      RaisedFileSystemEvent renamed,
      RaisedFileSystemEvent changed,
      RaisedFileSystemEvent created,
      RaisedFileSystemEvent deleted,
      Action<Exception> error,
      FileSystemEventCreator func,
      string path,
      ILogger logger
    )
    {
      // watch files or folders?
      _fileSystemEventCreator = func ?? throw new ArgumentNullException(nameof(func));

      // set the actions.
      _renamed = renamed ?? throw new ArgumentNullException(nameof(renamed));
      _changed = changed ?? throw new ArgumentNullException(nameof(changed));
      _created = created ?? throw new ArgumentNullException(nameof(created));
      _deleted = deleted ?? throw new ArgumentNullException(nameof(deleted));

      _error = error ?? throw new ArgumentNullException(nameof(error));

      // the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
        directorywatcher.Watcher watcher = null;
        try
        {
          // create the watcher and start monitoring.
          watcher = MonitorPath();

          // we call error ... but we do not care about the return value.
 //         watcher.OnErrorAsync +=  ( e, t ) => 
 //         {
 //           lastException = new Exception( e.Message );
 //         };

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
          watcher?.Stop();
        }
      }
    }

    private directorywatcher.Watcher MonitorPath()
    {
      // create the file syste, watcher.
      var watcher = new directorywatcher.Watcher();
      watcher.Add( new directorywatcher.Request( 
        _path,
        true ));

//      watcher.OnRenamedAsync += (e, t) => _tasks.Add( _renamed ( _fileSystemEventCreator(e, _logger)));
//      watcher.OnTouchedAsync += (e, t) => _tasks.Add( _changed ( _fileSystemEventCreator(e, _logger)));
//      watcher.OnAddedAsync   += (e, t) => _tasks.Add( _created ( _fileSystemEventCreator(e, _logger)));
//      watcher.OnRemovedAsync += (e, t) => _tasks.Add( _deleted ( _fileSystemEventCreator(e, _logger)));

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
      ILogger logger,
      CancellationToken token
    )
    {

      IFileSystemEvent Lambda(directorywatcher.interfaces.IFileSystemEvent e, ILogger l) => (watchFolders ? new DirectorySystemEvent(e, l) : new FileSystemEvent(e, l));

      // create the watcher
      var watcher = new RecoveringWatcher
      (
        renamed, 
        changed, 
        created, 
        deleted, 
        error, 
        Lambda,
        path,
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
      ILogger logger,
      CancellationToken token
    )
    {
      // start a file watcher
      return StartWatcher( renamed, changed, created, deleted, error, path, false, logger, token );
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
      ILogger logger,
      CancellationToken token
    )
    {
      // start a folder watcher
      return StartWatcher(renamed, changed, created, deleted, error, path, true, logger, token);
    }
  }
}
