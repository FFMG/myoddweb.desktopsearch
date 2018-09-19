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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.helper.IO;
using myoddweb.desktopsearch.interfaces.Enums;
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
    internal class CompletedPendingFileUpdate : IPendingFileUpdate
    {
      /// <inheritdoc />
      public long FileId { get; }

      /// <inheritdoc />
      public FileInfo File { get; }

      /// <inheritdoc />
      public UpdateType PendingUpdateType { get; }

      /// <summary>
      /// The words we found for the pending updates.
      /// </summary>
      public interfaces.IO.IWords Words { get; }

      public CompletedPendingFileUpdate(
        IPendingFileUpdate pu, interfaces.IO.IWords words )
      {
        FileId = pu.FileId;
        File = pu.File;
        PendingUpdateType = pu.PendingUpdateType;
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
    private readonly IList<IFileParser> _parsers;

    /// <inheritdoc />
    /// <summary>
    /// The number of updates we want to try and do at a time.
    /// </summary>
    public int MaxUpdatesToProcess { get; }

    /// <summary>
    /// The performance counter.
    /// </summary>
    private readonly IPerformanceCounter _counter;
    #endregion

    public Files(IPerformanceCounter counter, int updatesPerFilesEvent, IList<IFileParser> parsers, IPersister persister, ILogger logger)
    {
      // save the counter
      _counter = counter ?? throw new ArgumentNullException(nameof(counter));

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
    public void Stop()
    {
      _counter?.Dispose();
    }

    /// <inheritdoc />
    public async Task<int> WorkAsync(CancellationToken token)
    {
      var tsActual = DateTime.UtcNow;
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
      finally
      {
        _counter?.IncremenFromUtcTime(tsActual);
      }
    }

    /// <summary>
    /// Process a single list update
    /// </summary>
    /// <param name="pendingFileUpdates"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task ProcessFileUpdates(IList<IPendingFileUpdate> pendingFileUpdates, CancellationToken token)
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
    public Task<CompletedPendingFileUpdate> WorkDeletedAsync(IPendingFileUpdate pendingFileUpdate, CancellationToken token)
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
    public async Task<CompletedPendingFileUpdate> WorkCreatedAsync(IPendingFileUpdate pendingFileUpdate, CancellationToken token)
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
    public async Task<CompletedPendingFileUpdate> WorkChangedAsync(IPendingFileUpdate pendingFileUpdate, CancellationToken token)
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
    private async Task<CompletedPendingFileUpdate> ProcessFile(IPendingFileUpdate pendingFileUpdate, CancellationToken token)
    {
      // start allt he tasks
      var tasks = new HashSet<Task<interfaces.IO.IWords>>();
      foreach (var parser in _parsers)
      {
        tasks.Add(ProcessFile(parser, pendingFileUpdate.File, token));
      }

      // do we have any work to do?
      if (!tasks.Any())
      {
        // nothing to do...
        return new CompletedPendingFileUpdate(pendingFileUpdate, null );
      }

      // wait for all the parsers to do their work
      var totalWords = await helper.Wait.WhenAll( tasks, _logger, token ).ConfigureAwait(false);

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
    private async Task<interfaces.IO.IWords> ProcessFile(IFileParser parser, FileInfo file, CancellationToken token)
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
    private async Task CompletePendingFileUpdates(IReadOnlyCollection<CompletedPendingFileUpdate> completedPendingFileUpdates, CancellationToken token)
    {
      // do some smart checking...
      if (!completedPendingFileUpdates.Any())
      {
        // nothing to do.
        return;
      }

      var transaction = await _persister.BeginWrite(token).ConfigureAwait(false);
      if (null == transaction)
      {
        throw new Exception("Unable to get transaction!");
      }

      try
      {
        foreach (var pendingFileUpdate in completedPendingFileUpdates)
        {
          // complete the update
          await CompletePendingFileUpdate(pendingFileUpdate, transaction, token).ConfigureAwait(false);
        }

        // all done
        _persister.Commit(transaction);
      }
      catch (OperationCanceledException e)
      {
        _persister.Rollback(transaction);
        if (e.CancellationToken != token)
        {
          // was not my token that cancelled!
          _logger.Exception(e);
        }
        throw;
      }
      catch ( Exception e )
      {
        _persister.Rollback(transaction);
        _logger.Exception(e);
        throw;
      }
    }

    /// <summary>
    /// Complete a single pending task.
    /// </summary>
    /// <param name="pendingFileUpdate"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task CompletePendingFileUpdate(CompletedPendingFileUpdate pendingFileUpdate, IConnectionFactory connectionFactory, CancellationToken token)
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
            await _persister.FilesWords.AddOrUpdateWordsToFileAsync(words, fileId, connectionFactory, token).ConfigureAwait(false);
          }
          break;

        case UpdateType.Deleted:
          // we deleted the file, so remove the words.
          await _persister.FilesWords.DeleteFileFromFilesAndWordsAsync(fileId, connectionFactory, token).ConfigureAwait(false);
          break;

        case UpdateType.Changed:
          //  we changed the file, so we have to delete the old words
          // as well as add the new ones.
          await _persister.FilesWords.DeleteFileFromFilesAndWordsAsync(fileId, connectionFactory, token).ConfigureAwait(false);
          if (words != null)
          {
            await _persister.FilesWords.AddOrUpdateWordsToFileAsync(words, fileId, connectionFactory, token).ConfigureAwait(false);
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
    private async Task TouchFileAsync(IEnumerable<IPendingFileUpdate> pendingFileUpdates, CancellationToken token)
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
          await _persister.Folders.Files.FileUpdates.TouchFileAsync(pendingFileUpdate.FileId, pendingFileUpdate.PendingUpdateType, transaction, token).ConfigureAwait(false);
        }

        // we are done
        _persister.Commit(transaction);
      }
      catch
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
    private async Task<IList<IPendingFileUpdate>> GetPendingFileUpdatesAndMarkFileProcessedAsync(CancellationToken token)
    {
      // get the transaction
      var transaction = await _persister.BeginWrite(token).ConfigureAwait(false);
      if (null == transaction)
      {
        throw new Exception("Unable to get transaction!");
      }

      try
      {
        var pendingUpdates = await _persister.Folders.Files.FileUpdates.GetPendingFileUpdatesAsync(MaxUpdatesToProcess, transaction, token).ConfigureAwait(false);
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
        await _persister.Folders.Files.FileUpdates.MarkFilesProcessedAsync(pendingUpdates.Select( p => p.FileId), transaction, token).ConfigureAwait(false);

        // we are done with this list.
        _persister.Commit(transaction);
        return pendingUpdates;
      }
      catch
      {
        _persister.Rollback(transaction);
        throw;
      }
    }
    #endregion
  }
}
