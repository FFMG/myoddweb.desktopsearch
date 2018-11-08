﻿//This file is part of Myoddweb.DesktopSearch.
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.helper.Performance;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.processor.Processors
{
  internal class Parser : IProcessor
  {
    #region Member Variables
    /// <summary>
    /// The logger that we will be using to log messages.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The persister.
    /// </summary>
    private readonly IPersister _persister;

    public int MaxUpdatesToProcess { get; }

    /// <summary>
    /// The performance counter.
    /// </summary>
    private readonly ICounter _counter;
    #endregion

    public Parser(ICounter counter, int numberOfFilesToUpdates, IPersister persister, ILogger logger)
    {
      // save the counter
      _counter = counter ?? throw new ArgumentNullException(nameof(counter));

      if (numberOfFilesToUpdates <= 0)
      {
        throw new ArgumentException($"The number of file ids to try per events cannot be -ve or zero, ({numberOfFilesToUpdates})");
      }
      MaxUpdatesToProcess = numberOfFilesToUpdates;

      // set the persister.
      _persister = persister ?? throw new ArgumentNullException(nameof(persister));

      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<int> WorkAsync(CancellationToken token)
    {
      // get the number of file ids we want to work on.
      var pendingParserWordsUpdates = await GetPendingParserWordsUpdatesAsync(token).ConfigureAwait( false );
      if (pendingParserWordsUpdates == null || !pendingParserWordsUpdates.Any())
      {
        return 0;
      }

      using( _counter.Start() )
      {
        var connectionFactory = await _persister.BeginWrite(token).ConfigureAwait(false);
        if (null == connectionFactory)
        {
          throw new Exception("Unable to get transaction!");
        }

        try
        {
          if (!await _persister.FilesWords.AddParserWordsAsync(pendingParserWordsUpdates, connectionFactory, token).ConfigureAwait(false))
          {
            // there was an issue adding those words for that file id.
            _persister.Rollback(connectionFactory);
            return 0;
          }

          foreach (var pendingParserWordsUpdate in pendingParserWordsUpdates)
          {
            // thow if needed.
            token.ThrowIfCancellationRequested();

            // delete that part id so we do not do it again.
            await _persister.ParserWords.DeleteFileIds(pendingParserWordsUpdate.Id, pendingParserWordsUpdate.FileIds, connectionFactory, token).ConfigureAwait( false );

            // if we found any, log it.
            _logger.Verbose($"Processor : {pendingParserWordsUpdate.Word.Value} processed for {pendingParserWordsUpdate.FileIds.Count} file(s).");
          }

          // we are done.
          _persister.Commit(connectionFactory);

          // return what we did
          return pendingParserWordsUpdates.Count;
        }
        catch (OperationCanceledException e)
        {
          _logger.Warning("Received cancellation request - Parsing File words.");
          _persister.Rollback(connectionFactory);

          // is it my token?
          if (e.CancellationToken != token)
          {
            _logger.Exception(e);
          }
          throw;
        }
        catch (Exception)
        {
          _persister.Rollback(connectionFactory);
          throw;
        }
      }
    }

    /// <inheritdoc />
    public void Stop()
    {
      _counter?.Dispose();
    }

    private async Task<IList<IPendingParserWordsUpdate>> GetPendingParserWordsUpdatesAsync(CancellationToken token)
    {
      // get the transaction
      var connectionFactory = await _persister.BeginRead(token).ConfigureAwait(false);
      if (null == connectionFactory)
      {
        throw new Exception("Unable to get transaction!");
      }

      try
      {
        // we will assume one word per file
        // so just get them all at once :)
        var pendingParserWordsUpdates = await _persister.ParserWords.GetPendingParserWordsUpdatesAsync( MaxUpdatesToProcess, connectionFactory, token ).ConfigureAwait(false);
        _persister.Commit(connectionFactory);
        return pendingParserWordsUpdates != null ? (pendingParserWordsUpdates.Any() ? pendingParserWordsUpdates : null)  : null;
      }
      catch (OperationCanceledException e)
      {
        _persister.Rollback(connectionFactory);

        // is it my token?
        if (e.CancellationToken != token)
        {
          _logger.Exception(e);
        }
        throw;
      }
      catch (Exception)
      {
        _persister.Rollback(connectionFactory);
        throw;
      }
    }
  }
}
