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
using System.Threading;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;
using myoddweb.desktopsearch.processor.Processors;

namespace myoddweb.desktopsearch.processor
{
  public class Processor
  {
    #region Member variables

    /// <summary>
    /// When we register a token
    /// </summary>
    private CancellationTokenRegistration _cancellationTokenRegistration;

    /// <summary>
    /// The logger that we will be using to log messages.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// All the processor timeers running.
    /// </summary>
    private readonly List<ProcessorTimer> _timers;
    #endregion

    public Processor(
      List<IFileParser> fileParsers,
      interfaces.Configs.IProcessors config, 
      IPersister persister, 
      ILogger logger, 
      IDirectory directory)
    {
      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      const string categoryName = "myoddweb.desktopsearch";
      const string categoryHelp = "Performance counters for myoddweb.desktopsearch";
      const string directoryCounterName = "Average time processing Directories";
      const string fileCounterName = "Average time processing Files";

      if (PerformanceCounterCategory.Exists(categoryName))
      {
        PerformanceCounterCategory.Delete(categoryName);
      }

      // Create the various processors, they will not start doing anything just yet
      // or at least, they shouldn't
      _timers = new List<ProcessorTimer>();

      var directoriesCounter = new PerformanceCounter(categoryName, categoryHelp, directoryCounterName, logger);
      for ( var i = 0; i < config.ConcurrentDirectoriesProcessor; ++i)
      {
        _timers.Add( new ProcessorTimer(new Folders(directoriesCounter, persister, logger, directory), _logger, config.QuietEventsProcessorMs, config.BusyEventsProcessorMs));
      }

      var filesCounter = new PerformanceCounter(categoryName, categoryHelp, fileCounterName, logger );
      for (var i = 0; i < config.ConcurrentFilesProcessor; ++i)
      {
        _timers.Add( new ProcessorTimer(new Files(filesCounter, config.UpdatesPerFilesEvent, fileParsers, persister, logger), _logger, config.QuietEventsProcessorMs, config.BusyEventsProcessorMs));
      }
    }

    #region Start/Stop functions
    /// <summary>
    /// Start processor.
    /// </summary>
    public void Start(CancellationToken token)
    {
      // stop what might have already started.
      Stop();

      // start the timers
      _timers.ForEach(t => t.Start( token));

      // register the token cancellation
      _cancellationTokenRegistration = token.Register(TokenCancellation);
    }

    /// <summary>
    /// Stop processor
    /// </summary>
    public void Stop()
    {
      // we are done with the registration
      _cancellationTokenRegistration.Dispose();

      // and we can stop the timers.
      _timers.ForEach(t => t.Stop());
    }

    /// <summary>
    /// Called when the token has been cancelled.
    /// </summary>
    private void TokenCancellation()
    {
      _logger.Verbose("Stopping Events parser");
      Stop();
      _logger.Verbose("Done events parser");
    }
    #endregion
  }
}