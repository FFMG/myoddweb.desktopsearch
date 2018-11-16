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
using myoddweb.desktopsearch.helper.Persisters;
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
    public async Task<int> WorkAsync(IConnectionFactory factory, CancellationToken token)
    {
      // get the number of file ids we want to work on.
      var pendingParserWordsUpdates = await GetPendingParserWordsUpdatesAsync(factory, token).ConfigureAwait( false );
      if (pendingParserWordsUpdates == null || !pendingParserWordsUpdates.Any())
      {
        return 0;
      }

      using( _counter.Start() )
      {
        try
        {
          using (var wordsHelper = new WordsHelper(factory, _persister.Words.TableName))
          using (var partsHelper = new PartsHelper(factory, _persister.Parts.TableName))
          using (var filesWordsHelper = new FilesWordsHelper(factory, _persister.FilesWords.TableName))
          using (var wordsPartsHelper = new WordsPartsHelper(factory, _persister.WordsParts.TableName))
          using (var parserWordsHelper = new ParserWordsHelper(factory, _persister.ParserWords.TableName))
          using (var parserFilesWordsHelper = new ParserFilesWordsHelper(factory, _persister.ParserFilesWords.TableName))
          {
            foreach (var pendingParserWordsUpdate in pendingParserWordsUpdates)
            {
              // thow if needed.
              token.ThrowIfCancellationRequested();

              // is this a valid word?
              if (!_persister.Words.IsValidWord(pendingParserWordsUpdate.Word))
              {
                // no, it is not, so just get rid of it.
                var id = await parserWordsHelper.GetIdAsync(pendingParserWordsUpdate.Word.Value, token).ConfigureAwait(false);
                await parserWordsHelper.DeleteWordAsync( id, token).ConfigureAwait(false);
                await parserFilesWordsHelper.DeleteWordAsync(id, token).ConfigureAwait(false);

                // if we found any, log it.
                _logger.Verbose($"Processor : Deleted word {pendingParserWordsUpdate.Word.Value} for {pendingParserWordsUpdate.FileIds.Count} file(s) as it is not a valid word.");

                continue;
              }

              if (!await _persister.FilesWords.AddParserWordsAsync(
                  pendingParserWordsUpdate,
                  wordsHelper,
                  filesWordsHelper,
                  partsHelper,
                  wordsPartsHelper,
                  token)
                .ConfigureAwait(false))
              {
                // there was an issue adding those words for that file id.
                continue;
              }

              // delete all the file ids that used that parser word id.
              // because we have just processed them all at once.
              await _persister.ParserWords.DeleteFileIds(pendingParserWordsUpdate.Id, pendingParserWordsUpdate.FileIds, parserFilesWordsHelper, token).ConfigureAwait(false);

              // if we found any, log it.
              _logger.Verbose($"Processor : {pendingParserWordsUpdate.Word.Value} processed for {pendingParserWordsUpdate.FileIds.Count} file(s).");
            }
          }

          // return what we did
          return pendingParserWordsUpdates.Count;
        }
        catch (OperationCanceledException e)
        {
          _logger.Warning("Received cancellation request - Parsing File words.");

          // is it my token?
          if (e.CancellationToken != token)
          {
            _logger.Exception(e);
          }
          throw;
        }
      }
    }

    /// <inheritdoc />
    public void Stop()
    {
      _counter?.Dispose();
    }

    private async Task<IList<IPendingParserWordsUpdate>> GetPendingParserWordsUpdatesAsync(IConnectionFactory factory, CancellationToken token)
    {
      // get the transaction
      try
      {
        // we will assume one word per file
        // so just get them all at once :)
        var pendingParserWordsUpdates = await _persister.ParserWords.GetPendingParserWordsUpdatesAsync( MaxUpdatesToProcess, factory, token ).ConfigureAwait(false);
        return pendingParserWordsUpdates != null ? (pendingParserWordsUpdates.Any() ? pendingParserWordsUpdates : null)  : null;
      }
      catch (OperationCanceledException e)
      {
        // is it my token?
        if (e.CancellationToken != token)
        {
          _logger.Exception(e);
        }
        throw;
      }
    }
  }
}
