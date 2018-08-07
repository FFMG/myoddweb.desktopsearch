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
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;

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
    private readonly List<IFileParser> _parsers;
    #endregion

    public Files(List<IFileParser> parsers, IPersister persister, ILogger logger)
    {
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
        var pendingUpdate = await GetPendingFileUpdateAndMarkFileProcessedAsync(token).ConfigureAwait(false);
        if (null == pendingUpdate)
        {
          //  probably was canceled.
          return 0;
        }

        // process the update.
        await ProcessFileUpdate(pendingUpdate, token).ConfigureAwait(false);

        // we processed one update
        return 1;
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
    /// <param name="pendingFileUpdate"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task ProcessFileUpdate(PendingFileUpdate pendingFileUpdate, CancellationToken token)
    {
      // now try and process the files.
      try
      {
        switch (pendingFileUpdate.PendingUpdateType)
        {
          case UpdateType.Created:
            await WorkCreatedAsync(pendingFileUpdate.FileId, token).ConfigureAwait(false);
            break;

          case UpdateType.Deleted:
            await WorkDeletedAsync(pendingFileUpdate.FileId, token).ConfigureAwait(false);
            break;

          case UpdateType.Changed:
            // renamed or content/settingss changed
            await WorkChangedAsync(pendingFileUpdate.FileId, token).ConfigureAwait(false);
            break;

          default:
            throw new ArgumentOutOfRangeException();
        }
      }
      catch (Exception)
      {
        // something did not work ... re-touch the files
        await TouchFileAsync(pendingFileUpdate.FileId, pendingFileUpdate.PendingUpdateType, token).ConfigureAwait(false);

        // done
        throw;
      }
    }

    #region Workers
    /// <summary>
    /// A folder was deleted
    /// </summary>
    /// <param name="fileId"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task WorkDeletedAsync(long fileId, CancellationToken token)
    {
      // because the file was deleted from the db we must remove the words for it.
      // we could technically end up with orphan words, (words with no file ids for it)
      // but that's not really that important, maybe one day we will vacuum those words?.
      await DeleteFileFromFilesAndWordsAsync(fileId, token).ConfigureAwait(false);
    }

    /// <summary>
    /// A folder was created
    /// </summary>
    /// <param name="fileId"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<bool> WorkCreatedAsync(long fileId, CancellationToken token)
    {
      // get the file we just created.
      var file = await GetFileAsync(fileId, token).ConfigureAwait(false);
      if (null == file)
      {
        _logger.Warning( $"Unable to find, and process created file id: {fileId}.");
        return true;
      }

      //  process it...
      await ProcessFile( file, fileId, token ).ConfigureAwait(false);

      // return that the work was complete.
      return true;
    }

    /// <summary>
    /// A folder was changed
    /// </summary>
    /// <param name="fileId"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<bool> WorkChangedAsync(long fileId, CancellationToken token)
    {
      var file = await GetFileAsync(fileId, token).ConfigureAwait(false);
      if (null == file)
      {
        _logger.Warning($"Unable to find, and process changed id: {fileId}.");
        return true;
      }

      //  process it...
      await ProcessFile(file, fileId, token).ConfigureAwait(false);

      // return if we cancelled.
      return true;
    }
    #endregion

    #region Processors
    /// <summary>
    /// Process a file.
    /// </summary>
    /// <param name="file"></param>
    /// <param name="fileId"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task ProcessFile(FileInfo file, long fileId, CancellationToken token)
    {
      var tasks = new List<Task<Words>>();
      foreach (var parser in _parsers)
      {
        tasks.Add( ProcessFile(parser, file, token));
      }

      // do we have any work to do?
      if (!tasks.Any())
      {
        await DeleteFileFromFilesAndWordsAsync(fileId, token).ConfigureAwait(false);
        return;
      }

      // wait for all the parsers to do their work
      var totalWords = await Task.WhenAll(tasks.ToArray()).ConfigureAwait(false);

      // merge them all into one.
      var words = new Words( totalWords );
  
      // do we have any work to do?
      if (!words.Any())
      {
        // we found no words ... so remove whatever we might have.
        await DeleteFileFromFilesAndWordsAsync(fileId, token).ConfigureAwait(false);
        return;
      }

      // add all the words of that file
      await AddOrUpdateWordsToFileAsync( words, fileId, token).ConfigureAwait(false);
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
      if (words.Any())
      {
        // if we found any, log it.
        _logger.Verbose($"Parser : {parser.Name} processed {words.Count} words.");
      }
      return words;
    }
    #endregion

    #region Database calls.
    /// <summary>
    /// Mark the given file as processed.
    /// </summary>
    /// <param name="fileId"></param>
    /// <param name="type"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task TouchFileAsync(long fileId, UpdateType type, CancellationToken token)
    {
      var transaction = await _persister.Begin(token).ConfigureAwait(false);
      if (null == transaction)
      {
        throw new Exception("Unable to get transaction!");
      }

      try
      {
        // mark it as persisted.
        await _persister.TouchFileAsync(fileId, type, transaction, token).ConfigureAwait(false);

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
    /// Add or update words to the database for this file.
    /// </summary>
    /// <param name="words"></param>
    /// <param name="fileId"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task AddOrUpdateWordsToFileAsync(Words words, long fileId, CancellationToken token)
    {
      var transaction = await _persister.Begin(token).ConfigureAwait(false);
      if (null == transaction)
      {
        throw new Exception("Unable to get transaction!");
      }

      try
      {
        // remove the words
        await _persister.AddOrUpdateWordsToFileAsync(words, fileId, transaction, token).ConfigureAwait(false);

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
    /// Delete all the wods for that file, (if there were any)
    /// </summary>
    /// <param name="fileId"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task DeleteFileFromFilesAndWordsAsync(long fileId, CancellationToken token)
    {
      var transaction = await _persister.Begin(token).ConfigureAwait(false);
      if (null == transaction)
      {
        throw new Exception("Unable to get transaction!");
      }

      try
      {
        // remove the words
        await _persister.DeleteFileFromFilesAndWordsAsync(fileId, transaction, token).ConfigureAwait(false);

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
    /// Get a File from the persister.
    /// </summary>
    /// <param name="fileId"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<FileInfo> GetFileAsync( long fileId, CancellationToken token)
    {
      var transaction = await _persister.Begin(token).ConfigureAwait(false);
      if (null == transaction)
      {
        throw new Exception("Unable to get transaction!");
      }

      try
      {
        // get the file that changed.
        var file = await _persister.GetFileAsync(fileId, transaction, token).ConfigureAwait(false);

        // we are done
        _persister.Commit(transaction);

        // return the file we found
        return file;
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
    private async Task<PendingFileUpdate> GetPendingFileUpdateAndMarkFileProcessedAsync(CancellationToken token)
    {
      // get the transaction
      var transaction = await _persister.Begin(token).ConfigureAwait(false);
      if (null == transaction)
      {
        throw new Exception("Unable to get transaction!");
      }

      try
      {
        var pendingUpdates = await _persister.GetPendingFileUpdatesAsync(1, transaction, token).ConfigureAwait(false);
        if (null == pendingUpdates)
        {
          _logger.Error("Unable to get any pending file updates.");

          // we will now return null
          _persister.Commit(transaction);
          return null;
        }

        var pendingUpdate = pendingUpdates.FirstOrDefault();
        if (null == pendingUpdate)
        {
          return null;
        }

        // the first thing we will do is mark the file as processed.
        // if anything goes wrong _after_ that we will try and 'touch' it again.
        // by doing it that way around we ensure that we never keep the transaction.
        // and we don't run the risk of someone else trying to process this again.
        await _persister.MarkFileProcessedAsync(pendingUpdate.FileId, transaction, token).ConfigureAwait(false);

        _persister.Commit(transaction);
        return pendingUpdate;
      }
      catch (Exception e)
      {
        _persister.Rollback(transaction);
        throw;
      }
    }
    #endregion
  }
}
