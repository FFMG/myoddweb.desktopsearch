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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.helper.Performance;
using myoddweb.desktopsearch.interfaces.Configs;
using myoddweb.desktopsearch.interfaces.Enums;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Persisters;
using ILogger = myoddweb.desktopsearch.interfaces.Logging.ILogger;

namespace myoddweb.desktopsearch.processor.Processors
{
  internal class Files : IProcessor
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

    /// <summary>
    /// All the file parsers.
    /// </summary>
    private readonly IList<IFileParser> _parsers;

    /// <summary>
    /// The files we are choosing to ignore.
    /// </summary>
    private readonly IList<IIgnoreFile> _ignoreFiles;

    /// <inheritdoc />
    /// <summary>
    /// The number of updates we want to try and do at a time.
    /// </summary>
    public int MaxUpdatesToProcess { get; }

    /// <summary>
    /// The performance counter.
    /// </summary>
    private readonly ICounter _counter;
    #endregion

    public Files(
      ICounter counter, 
      int updatesPerFilesEvent, 
      IList<IFileParser> parsers, 
      IList<IIgnoreFile> ignoreFiles, 
      IPersister persister, 
      ILogger logger)
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

      // the files we are ignoring.
      _ignoreFiles = ignoreFiles ?? throw new ArgumentNullException(nameof(ignoreFiles));

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
    private async Task ProcessFileUpdates(ICollection<IPendingFileUpdate> pendingFileUpdates, CancellationToken token)
    {
      // now try and process the files.
      var factory = await _persister.BeginWrite(token).ConfigureAwait( false );
      try
      {
        // the first thing we will do is mark the file as processed.
        // if anything goes wrong _after_ that we will try and 'touch' it again.
        // by doing it that way around we ensure that we never keep the transaction.
        // and we don't run the risk of someone else trying to process this again.
        await _persister.Folders.Files.FileUpdates.MarkFilesProcessedAsync(pendingFileUpdates.Select(p => p.FileId), factory, token).ConfigureAwait(false);

        var tasks = new List<Task>( pendingFileUpdates.Count );
        foreach (var pendingFileUpdate in pendingFileUpdates)
        {
          // throw if need be.
          token.ThrowIfCancellationRequested();

          switch (pendingFileUpdate.PendingUpdateType)
          {
            case UpdateType.Created:
              // only add the pending updates where there are actual words to add.
              tasks.Add(WorkCreatedAsync(factory, pendingFileUpdate, token).ContinueWith(
                t => CompletePendingFileUpdate(factory, t.Result, token), token ));
              break;

            case UpdateType.Deleted:
              tasks.Add( WorkDeletedAsync( factory, pendingFileUpdate, token) );
              break;

            case UpdateType.Changed:
              // renamed or content/settingss changed
              tasks.Add( WorkChangedAsync(factory, pendingFileUpdate, token).ContinueWith(
                t => CompletePendingFileUpdate(factory, t.Result, token), token));
              break;

            default:
              throw new ArgumentOutOfRangeException();
          }
        }

        // the 'continuewith' step is to pass all the words that we found.
        // this will be within it's own transaction
        // but the parsing has been done already.
        await helper.Wait.WhenAll( tasks, _logger, token ).ConfigureAwait(false);

        // commit the changes we made.
        _persister.Commit(factory);
      }
      catch
      {
        _persister.Rollback(factory);

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
    /// <param name="factory"></param>
    /// <param name="pendingFileUpdate"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task WorkDeletedAsync(
      IConnectionFactory factory,
      IPendingFileUpdate pendingFileUpdate, CancellationToken token)
    {
      var fileId = pendingFileUpdate.FileId;

      // because the file was deleted from the db we must remove the words for it.
      // we could technically end up with orphan words, (words with no file ids for it)
      // but that's not really that important, maybe one day we will vacuum those words?.
      await _persister.FilesWords.DeleteWordsAsync(fileId, factory, token).ConfigureAwait(false);
    }

    /// <summary>
    /// A folder was created
    /// </summary>
    /// <param name="factory"></param>
    /// <param name="pendingFileUpdate"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<IPendingFileUpdate> WorkCreatedAsync(
      IConnectionFactory factory,
      IPendingFileUpdate pendingFileUpdate, 
      CancellationToken token)
    {
      // get the file we just created.
      var file = pendingFileUpdate.File;
      if (null == file)
      {
        _logger.Warning( $"Unable to find, and process created file id: {pendingFileUpdate.FileId}.");
        return null;
      }

      //  process it...
      return await ProcessFile( factory, pendingFileUpdate, token ).ConfigureAwait(false);
    }

    /// <summary>
    /// A folder was changed
    /// </summary>
    /// <param name="factory"></param>
    /// <param name="pendingFileUpdate"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<IPendingFileUpdate> WorkChangedAsync(
      IConnectionFactory factory,
      IPendingFileUpdate pendingFileUpdate, 
      CancellationToken token)
    {
      var file = pendingFileUpdate.File;
      if (null == file)
      {
        _logger.Warning($"Unable to find, and process changed id: {pendingFileUpdate.FileId}.");
        return null;
      }

      // the file we are processing
      var fileId = pendingFileUpdate.FileId;

      // before we process the file, we need to delete all
      // the words that are attached to it.
      await _persister.FilesWords.DeleteWordsAsync(fileId, factory, token).ConfigureAwait(false);

      // then we can process it...
      return await ProcessFile(factory, pendingFileUpdate, token).ConfigureAwait(false);
    }
    #endregion

    #region Processors

    /// <summary>
    /// Process a file.
    /// </summary>
    /// <param name="factory"></param>
    /// <param name="pendingFileUpdate"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<IPendingFileUpdate> ProcessFile(
      IConnectionFactory factory,
      IPendingFileUpdate pendingFileUpdate, 
      CancellationToken token
    )
    {
      // is this file ignored?
      if (IsIgnored(pendingFileUpdate))
      {
        _logger.Information( $"Ignoring file: {pendingFileUpdate.File.FullName} as per IgnoreFiles rule(s).");
        return null;
      }

      // create the helper.
      var parserHelper = new PrarserHelper( pendingFileUpdate.File, _persister, factory, pendingFileUpdate.FileId);

      // start all the parser tasks
      var tasks = new List<Task<long>>();
      foreach (var parser in _parsers)
      {
        tasks.Add( ProcessFile( parserHelper, parser, pendingFileUpdate.File, token));
      }

      // do we have any work to do?
      if (!tasks.Any())
      {
        // nothing to do...
        return null;
      }

      // wait for all the parsers to do their work
      var totalWords = await helper.Wait.WhenAll( tasks, _logger, token ).ConfigureAwait(false);
      if (totalWords == null || totalWords.Sum() == 0)
      {
        // nothing was done.
        // nothing to do...
        return null;
      }

      // merge them all into one.
      return pendingFileUpdate;
    }

    /// <summary>
    /// Check if the file is ignored or not.
    /// </summary>
    /// <param name="pendingFileUpdate"></param>
    /// <returns></returns>
    private bool IsIgnored(IPendingFileUpdate pendingFileUpdate)
    {
      return _ignoreFiles.Any(ignoreFile => ignoreFile.Match(pendingFileUpdate.File));
    }

    /// <summary>
    /// Process the file with the given parser.
    /// </summary>
    /// <param name="helper">The file helper.</param>
    /// <param name="parser">The component that will parse this file</param>
    /// <param name="file">The file we are parsing</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns></returns>
    private async Task<long> ProcessFile(IParserHelper helper, IFileParser parser, FileSystemInfo file, CancellationToken token)
    {
      if (!parser.Supported(file))
      {
        // nothing to do.
        return 0;
      }

      using (_counter.Start() )
      {
        // look for the words
        var numberOfWords = await parser.ParseAsync(helper, _logger, token).ConfigureAwait(false);
        if (numberOfWords > 0)
        {
          // if we found any, log it.
          _logger.Verbose($"Parser : {parser.Name} processed {helper.Count} words in {file.FullName}.");
        }

        // null values are ignored.
        return numberOfWords;
      }
    }
    #endregion

    #region Database calls.
    /// <summary>
    /// Complete a single pending task.
    /// </summary>
    /// <param name="connectionFactory"></param>
    /// <param name="pendingFileUpdate"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task CompletePendingFileUpdate(IConnectionFactory connectionFactory, IPendingFileUpdate pendingFileUpdate, CancellationToken token)
    {
      // null checking
      if (null == pendingFileUpdate)
      {
        return;
      }

      // the file id we are trying to update.
      var fileId = pendingFileUpdate.FileId;

      switch (pendingFileUpdate.PendingUpdateType)
      {
        case UpdateType.Created:
          // this was newly created, so we just want to add the words.
          // there should be nothing to delete.
          await _persister.FilesWords.AddParserWordsAsync(fileId, connectionFactory, token).ConfigureAwait(false);
          break;

        case UpdateType.Changed:
          // We changed the file, so we have to delete
          // the old words as well as add the new ones.
          await _persister.FilesWords.AddParserWordsAsync( fileId, connectionFactory, token).ConfigureAwait(false);
          break;

        case UpdateType.Deleted:
          throw new InvalidOperationException("Deleted files should not be processed here.");

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
        // mark it as persisted.
        await _persister.Folders.Files.AddOrUpdateFilesAsync(pendingFileUpdates.Select( f => f.File ).ToList(), transaction, token).ConfigureAwait(false);

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
      var transaction = await _persister.BeginRead(token).ConfigureAwait(false);
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

        // close the transaction.
        _persister.Commit(transaction);

        // return null if we found nothing
        return !pendingUpdates.Any() ? null : pendingUpdates;
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
