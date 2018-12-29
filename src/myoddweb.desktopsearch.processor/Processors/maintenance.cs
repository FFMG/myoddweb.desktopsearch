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
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Configs;
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
    #endregion

    /// <summary>
    /// The persister.
    /// </summary>
    private readonly IPersister _persister;

    /// <summary>
    /// The active times
    /// </summary>
    private readonly IActive _active;

    public Maintenance(IActive active, IPersister persister, ILogger logger)
    {
      _active = active ?? throw new ArgumentNullException(nameof(active));
      _persister = persister ?? throw new ArgumentNullException(nameof(persister));
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<long> WorkAsync(IConnectionFactory connectionFactory, CancellationToken token)
    {
      try
      {
        // check if we are active at the current time.
        if (!_active.IsActive())
        {
          // we are not active ... so we have nothing to do.
          _logger.Verbose("Maintenance Process ignored, out of active hours.");
          return 0;
        }

        _logger.Verbose("Started Maintenance Process.");
        await _persister.MaintenanceAsync(connectionFactory, token).ConfigureAwait(false);
        _logger.Verbose("Complete Maintenance Process.");
      }
      catch (OperationCanceledException)
      {
        // nothing to log
        throw;
      }
      catch (Exception)
      {
        _logger.Verbose("Error while processing Maintenance.");
        throw;
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
