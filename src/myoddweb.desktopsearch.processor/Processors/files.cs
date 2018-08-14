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
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.processor.Processors
{
  internal class Files : IProcessor
  {
    /// <summary>
    /// The words for a completed pending update.
    /// </summary>
    internal class CompletedPendingFileUpdate : PendingFileUpdate
    {
      /// <summary>
      /// The words we found for the pending updates.
      /// </summary>
      public Words Words { get; }

      public CompletedPendingFileUpdate(
        PendingFileUpdate pu, Words words ) : base(pu)
      {
        Words = words;
      }
    }

    #region Member Variables
    /// <summary>
    /// The logger that we will be using to log messages.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The persister.
    /// </summary>
    private readonly IPersister _persister;

    /// <summary>
    /// All the file parsers.
    /// </summary>
    private readonly List<IFileParser> _parsers;

    /// <inheritdoc />
    /// <summary>
    /// The number of updates we want to try and do at a time.
    /// </summary>
    public int MaxUpdatesToProcess { get; }
    #endregion

    public Files( int updatesPerFilesEvent, List<IFileParser> parsers, IPersister persister, ILogger logger)
    {
      if (updatesPerFilesEvent <= 0)
      {
        throw new ArgumentException( $"The number of files to try per events cannot be -ve or zero, ({updatesPerFilesEvent})");
      }
      MaxUpdatesToProcess = updatesPerFilesEvent;

      // make sure that the parsers are valid.
      _parsers = parsers ?? throw new ArgumentNullException(nameof(parsers));

      // set the persister.
      _persister = persister ?? throw new ArgumentNullException(nameof(persister));

      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<int> WorkAsync(CancellationToken token)
    {
      try
      {
        // then get _all_ the file updates that we want to do.
        var pendingUpdates = await GetPendingFileUpdatesAndMarkFileProcessedAsync(token).ConfigureAwait(false);
        if (null == pendingUpdates)
        {
          //  probably was canceled.
          return 0;
        }

        // process the updates.
        await ProcessFileUpdates(pendingUpdates, token).ConfigureAwait(false);

        // return how many updates we did.
        return pendingUpdates.Count;
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Files Processor - Work");
        throw;
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        throw;
      }
    }

    /// <summary>
    /// Process a single list update
    /// </summary>
    /// <param name="pendingFileUpdates"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task ProcessFileUpdates(List<PendingFileUpdate> pendingFileUpdates, CancellationToken token)
    {
      // now try and process the files.
      try
      {
        var completedPendingFileUpdates = new HashSet<CompletedPendingFileUpdate>();
        foreach (var pendingFileUpdate in pendingFileUpdates)
        {
          switch (pendingFileUpdate.PendingUpdateType)
          {
            case UpdateType.Created:
              // only add the pending updates where there are actual words to add.
              var cpfu = await WorkCreatedAsync(pendingFileUpdate, token).ConfigureAwait(false);
              if (cpfu.Words?.Any() ?? false)
              {
                completedPendingFileUpdates.Add(cpfu);
              }
              break;

            case UpdateType.Deleted:
              completedPendingFileUpdates.Add(await WorkDeletedAsync(pendingFileUpdate, token).ConfigureAwait(false));
              break;

            case UpdateType.Changed:
              // renamed or content/settingss changed
              completedPendingFileUpdates.Add(await WorkChangedAsync(pendingFileUpdate, token).ConfigureAwait(false));
              break;

            default:
              throw new ArgumentOutOfRangeException();
          }
        }

        // the last step is to pass all the words that we found.
        // this will be within it's own transaction
        // but the parsing has been done already.
        await CompletePendingFileUpdates(completedPendingFileUpdates, token).ConfigureAwait(false);
      }
      catch (Exception)
      {
        // something did not work ... re-touch the files
        await TouchFileAsync(pendingFileUpdates, token).ConfigureAwait(false);

        // done
        throw;
      }
    }

    #region Workers
    /// <summary>
    /// A folder was deleted
    /// </summary>
    /// <param name="pendingFileUpdate"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task<CompletedPendingFileUpdate> WorkDeletedAsync(PendingFileUpdate pendingFileUpdate, CancellationToken token)
    {
      // because the file was deleted from the db we must remove the words for it.
      // we could technically end up with orphan words, (words with no file ids for it)
      // but that's not really that important, maybe one day we will vacuum those words?.
      return Task.FromResult(new CompletedPendingFileUpdate( pendingFileUpdate, null ));
    }

    /// <summary>
    /// A folder was created
    /// </summary>
    /// <param name="pendingFileUpdate"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<CompletedPendingFileUpdate> WorkCreatedAsync(PendingFileUpdate pendingFileUpdate, CancellationToken token)
    {
      // get the file we just created.
      var file = pendingFileUpdate.File;
      if (null == file)
      {
        _logger.Warning( $"Unable to find, and process created file id: {pendingFileUpdate.FileId}.");
        return null;
      }

      //  process it...
      return await ProcessFile( pendingFileUpdate, token ).ConfigureAwait(false);
    }

    /// <summary>
    /// A folder was changed
    /// </summary>
    /// <param name="pendingFileUpdate"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<CompletedPendingFileUpdate> WorkChangedAsync(PendingFileUpdate pendingFileUpdate, CancellationToken token)
    {
      var file = pendingFileUpdate.File;
      if (null == file)
      {
        _logger.Warning($"Unable to find, and process changed id: {pendingFileUpdate.FileId}.");
        return null;
      }

      //  process it...
      return await ProcessFile(pendingFileUpdate, token).ConfigureAwait(false);
    }
    #endregion

    #region Processors
    /// <summary>
    /// Process a file.
    /// </summary>
    /// <param name="pendingFileUpdate"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<CompletedPendingFileUpdate> ProcessFile(PendingFileUpdate pendingFileUpdate, CancellationToken token)
    {
      // start allt he tasks
      var tasks = new HashSet<Task<Words>>();
      _parsers.ForEach(parser => { tasks.Add(ProcessFile(parser, pendingFileUpdate.File, token)); });

      // do we have any work to do?
      if (!tasks.Any())
      {
        // nothing to do...
        return new CompletedPendingFileUpdate(pendingFileUpdate, null );
      }

      // wait for all the parsers to do their work
      var totalWords = await Task.WhenAll( tasks.ToArray() ).ConfigureAwait(false);

      // merge them all into one.
      return new CompletedPendingFileUpdate(pendingFileUpdate, new Words(totalWords));
    }

    /// <summary>
    /// Process the file with the given parser.
    /// </summary>
    /// <param name="parser"></param>
    /// <param name="file"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<Words> ProcessFile(IFileParser parser, FileInfo file, CancellationToken token)
    {
      if (!parser.Supported(file))
      {
        // nothing to do.
        return new Words();
      }

      // look for the words
      var words = await parser.ParseAsync(file, _logger, token).ConfigureAwait(false);
      if (words != null && words.Any() )
      {
        // if we found any, log it.
        _logger.Verbose($"Parser : {parser.Name} processed {words.Count} words in {file.FullName}.");
      }

      // null values are ignored.
      return words;
    }
    #endregion

    #region Database calls.
    /// <summary>
    /// Complete all the pending updates and send all the words to the file updates.
    /// </summary>
    /// <param name="completedPendingFileUpdates"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task CompletePendingFileUpdates(HashSet<CompletedPendingFileUpdate> completedPendingFileUpdates, CancellationToken token)
    {
      // do some smart checking...
      if (!completedPendingFileUpdates.Any())
      {
        // nothing to do.
        return;
      }

      foreach (var pendingFileUpdate in completedPendingFileUpdates )
      {
        var tsActual = DateTime.UtcNow;

        // complete the update
        await CompleteWordsUpdate(pendingFileUpdate.Words, token).ConfigureAwait(false);

        // complete the update
        await CompletePendingFileUpdate(pendingFileUpdate, token).ConfigureAwait( false );

        // Did this all take more than 5 seconds?
        var tsDiff = (DateTime.UtcNow - tsActual);
        if (tsDiff.TotalSeconds > 5)
        {
          _logger.Verbose($"Processing of file: {pendingFileUpdate.File.FullName} took {tsDiff:g} ({pendingFileUpdate.Words?.Count ?? 0} words)");
        }
      }
    }

    /// <summary>
    /// Complete an update, open a transaction and insert all the parts.
    /// </summary>
    /// <param name="words"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task CompleteWordsUpdate(Words words, CancellationToken token)
    {
      var transaction = await _persister.BeginWrite(token).ConfigureAwait(false);
      if (null == transaction)
      {
        throw new Exception("Unable to get transaction!");
      }

      try
      {
        // get out if we are cancelling
        token.ThrowIfCancellationRequested();

        // add all the words...
        await _persister.AddOrUpdateWordsAsync(words, transaction, token).ConfigureAwait(false);

        // we are done
        _persister.Commit(transaction);
      }
      catch (Exception)
      {
        _persister.Rollback(transaction);
        throw;
      }
    }

    /// <summary>
    /// Complete an update, open a transaction and insert all the parts.
    /// </summary>
    /// <param name="pendingFileUpdate"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task CompletePendingFileUpdate(CompletedPendingFileUpdate pendingFileUpdate, CancellationToken token)
    {
      var transaction = await _persister.BeginWrite(token).ConfigureAwait(false);
      if (null == transaction)
      {
        throw new Exception("Unable to get transaction!");
      }

      try
      {
        // try and process it.
        await CompletePendingFileUpdate(pendingFileUpdate, transaction, token).ConfigureAwait(false);

        // get out if we are cancelling
        token.ThrowIfCancellationRequested();

        // we are done
        _persister.Commit(transaction);
        transaction = null;
      }
      catch (Exception)
      {
        _persister.Rollback(transaction);
        throw;
      }
    }

    /// <summary>
    /// Complete a single pending task.
    /// </summary>
    /// <param name="pendingFileUpdate"></param>
    /// <param name="transaction"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task CompletePendingFileUpdate(CompletedPendingFileUpdate pendingFileUpdate, IDbTransaction transaction, CancellationToken token)
    {
      var words = pendingFileUpdate.Words;
      var fileId = pendingFileUpdate.FileId;

      switch (pendingFileUpdate.PendingUpdateType)
      {
        case UpdateType.Created:
          // this was newly created, so we just want to add the words.
          // there should be nothing to delete.
          if (words != null)
          {
            await _persister.AddOrUpdateWordsToFileAsync(words, fileId, transaction, token).ConfigureAwait(false);
          }
          break;

        case UpdateType.Deleted:
          // we deleted the file, so remove the words.
          await _persister.DeleteFileFromFilesAndWordsAsync(fileId, transaction, token).ConfigureAwait(false);
          break;

        case UpdateType.Changed:
          //  we changed the file, so we have to delete the old words
          // as well as add the new ones.
          await _persister.DeleteFileFromFilesAndWordsAsync(fileId, transaction, token).ConfigureAwait(false);
          if (words != null)
          {
            await _persister.AddOrUpdateWordsToFileAsync(words, fileId, transaction, token).ConfigureAwait(false);
          }
          break;

        case UpdateType.None:
          throw new ArgumentException( "Cannot process an update of type 'none'" );

        default:
          throw new ArgumentOutOfRangeException();
      }
    }

    /// <summary>
    /// Mark the given file as processed.
    /// </summary>
    /// <param name="pendingFileUpdates"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task TouchFileAsync(List<PendingFileUpdate> pendingFileUpdates, CancellationToken token)
    {
      var transaction = await _persister.BeginWrite(token).ConfigureAwait(false);
      if (null == transaction)
      {
        throw new Exception("Unable to get transaction!");
      }

      try
      {
        foreach (var pendingFileUpdate in pendingFileUpdates)
        {
          // mark it as persisted.
          await _persister.TouchFileAsync(pendingFileUpdate.FileId, pendingFileUpdate.PendingUpdateType, transaction, token).ConfigureAwait(false);
        }

        // we are done
        _persister.Commit(transaction);
      }
      catch (Exception)
      {
        _persister.Rollback(transaction);
        throw;
      }
    }

    /// <summary>
    /// Get the pending update
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<List<PendingFileUpdate>> GetPendingFileUpdatesAndMarkFileProcessedAsync(CancellationToken token)
    {
      // get the transaction
      var transaction = await _persister.BeginWrite(token).ConfigureAwait(false);
      if (null == transaction)
      {
        throw new Exception("Unable to get transaction!");
      }

      try
      {
        var pendingUpdates = await _persister.GetPendingFileUpdatesAsync(MaxUpdatesToProcess, transaction, token).ConfigureAwait(false);
        if (null == pendingUpdates)
        {
          _logger.Error("Unable to get any pending file updates.");

          // we will now return null
          _persister.Commit(transaction);
          return null;
        }

        if (!pendingUpdates.Any())
        {
          // we found nothing so we just return null.
          _persister.Commit(transaction);
          return null;
        }

        // the first thing we will do is mark the file as processed.
        // if anything goes wrong _after_ that we will try and 'touch' it again.
        // by doing it that way around we ensure that we never keep the transaction.
        // and we don't run the risk of someone else trying to process this again.
        await _persister.MarkFilesProcessedAsync(pendingUpdates.Select( p => p.FileId), transaction, token).ConfigureAwait(false);

        // we are done with this list.
        _persister.Commit(transaction);
        return pendingUpdates;
      }
      catch (Exception)
      {
        _persister.Rollback(transaction);
        throw;
      }
    }
    #endregion
  }
}
