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
using myoddweb.desktopsearch.interfaces.Configs;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Persisters;
using myoddweb.desktopsearch.processor.Processors;
using ILogger = myoddweb.desktopsearch.interfaces.Logging.ILogger;
using ProcessorPerformanceCounter = myoddweb.desktopsearch.processor.IO.ProcessorPerformanceCounter;

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
      IList<IFileParser> fileParsers,
      IProcessors config, 
      IPersister persister, 
      ILogger logger, 
      IDirectory directory,
      IPerformance performance
    )
    {
      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      const string directoryCounterName = "Processor: Average time processing Directories";
      const string fileCounterName = "Processor: Average time processing Files";
      const string parserCounterName = "Processor: Average time processing Words";

      // Create the various processors, they will not start doing anything just yet
      // or at least, they shouldn't
      _timers = new List<ProcessorTimer>();

      var directoriesCounter = new ProcessorPerformanceCounter(performance, directoryCounterName, logger);
      _timers.Add( new ProcessorTimer(new Folders(directoriesCounter, config.UpdatesPerFolderEvent, persister, logger, directory), _logger, config.QuietEventsProcessorMs, config.BusyEventsProcessorMs));

      var filesCounter = new ProcessorPerformanceCounter(performance, fileCounterName, logger);
      _timers.Add( new ProcessorTimer(new Files(filesCounter, config.UpdatesPerFilesEvent, fileParsers, config.IgnoreFiles, persister, logger), _logger, config.QuietEventsProcessorMs, config.BusyEventsProcessorMs));

      // the word parser.
      var parserCounter = new ProcessorPerformanceCounter(performance, parserCounterName, logger);
      _timers.Add(new ProcessorTimer(new Parser(parserCounter, config.UpdateFileIdsEvent, persister, logger), _logger, config.QuietEventsProcessorMs, config.BusyEventsProcessorMs));
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