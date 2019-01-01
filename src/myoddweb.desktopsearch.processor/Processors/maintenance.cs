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
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Configs;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Persisters;
using ILogger = myoddweb.desktopsearch.interfaces.Logging.ILogger;

namespace myoddweb.desktopsearch.processor.Processors
{
  internal class Maintenance : IProcessor
  {
    #region Member Variables
    /// <summary>
    /// The logger that we will be using to log messages.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// This is the parser we are currently working with.
    /// </summary>
    private readonly IParser _parser;

    /// <summary>
    /// The persister.
    /// </summary>
    private readonly IPersister _persister;

    /// <summary>
    /// The active times
    /// </summary>
    private readonly IActive _active;
    #endregion

    public Maintenance(IActive active, IParser parser, IPersister persister, ILogger logger)
    {
      _parser = parser ?? throw new ArgumentNullException(nameof(parser));
      _active = active ?? throw new ArgumentNullException(nameof(active));
      _persister = persister ?? throw new ArgumentNullException(nameof(persister));
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<long> WorkAsync(CancellationToken token)
    {
      // check if we are active at the current time.
      if (!_active.IsActive())
      {
        // we are not active ... so we have nothing to do.
        _logger.Verbose("Maintenance Process ignored, out of active hours.");
        return 0;
      }

      var stopwatch = new Stopwatch();
      stopwatch.Start();
      var success = false;

      try
      {
        _logger.Information("Started Maintenance Process.");
        await _persister.MaintenanceAsync(token).ConfigureAwait(false);

        await _parser.MaintenanceAsync(token).ConfigureAwait(false);

        // it worked
        success = true;
      }
      catch (OperationCanceledException)
      {
        // nothing to log
        throw;
      }
      catch (Exception e)
      {
        _logger.Exception("Error while processing Maintenance.", e);
        throw;
      }
      finally
      {
        _logger.Information(success
          ? $"Complete Maintenance Process (Time Elapsed: {stopwatch.Elapsed:g})."
          : $"Complete Maintenance Process with errors (Time Elapsed: {stopwatch.Elapsed:g}).");
      }
      return 0;
    }

    /// <inheritdoc />
    public void Stop()
    {
      // nothing to do.
    }
  }
}
