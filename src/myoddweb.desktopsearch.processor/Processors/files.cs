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
    /// The current directory.
    /// </summary>
    private IDirectory _directory;

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

    /// <summary>
    /// The number of files we want to do per processing events.
    /// Don't make that number too small as it will take forever to parse
    /// But also not too big as it blocks the database when/if there is work to do.
    /// </summary>
    private int UpdatesFilesPerEvent { get; }

    /// <summary>
    /// The performance counter.
    /// </summary>
    private readonly ICounter _counter;
    #endregion

    public Files(
      ICounter counter,
      int updatesFilesPerEvent,
      IList<IFileParser> parsers, 
      IList<IIgnoreFile> ignoreFiles,
      IPersister persister, 
      ILogger logger,
      IDirectory directory
      )
    {
      // save the counter
      _counter = counter ?? throw new ArgumentNullException(nameof(counter));

      // check for ignored directories.
      _directory = directory ?? throw new ArgumentNullException(nameof(directory));

      if (updatesFilesPerEvent <= 0)
      {
        throw new ArgumentException($"The total number of words for all files to try per events cannot be -ve or zero, ({updatesFilesPerEvent})");
      }
      UpdatesFilesPerEvent = updatesFilesPerEvent;

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
    public async Task<long> WorkAsync( IConnectionFactory factory, CancellationToken token)
    {
      try
      {
        // Make sure that we do not kill the IO 
        // process '4* Environment.ProcessorCount' files at a time.
        var mumberOfFiles = 4 * Environment.ProcessorCount;
        long totalnumberOfFilesProcessed = 0;
        for (long fileEvents = 0; fileEvents < UpdatesFilesPerEvent; fileEvents += mumberOfFiles )
        {
          // then get _all_ the file updates that we want to do.
          var pendingUpdates = await GetPendingFileUpdatesAndMarkFileProcessedAsync(mumberOfFiles, factory, token).ConfigureAwait(false);
          if (null == pendingUpdates)
          {
            // no more words...
            break;
          }

          // process those words.
          totalnumberOfFilesProcessed += await ProcessFileAndWordsUpdates(pendingUpdates, token).ConfigureAwait(false);
        }
        return totalnumberOfFilesProcessed;
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Files processor: Received cancellation request - Files Processor - Work");
        throw;
      }
      catch (Exception e)
      {
        _logger.Exception("Files processor: ", e);
        throw;
      }
    }

    private async Task<long> ProcessFileAndWordsUpdates(ICollection<IPendingFileUpdate> pendingFileUpdates, CancellationToken token)
    {
      // process the files
      var completedUpdates = await ProcessFileUpdates( pendingFileUpdates, token).ConfigureAwait(false);

      // how many files did we process, (that are not null)
      return completedUpdates.Count( p => p != null );
    }

    /// <summary>
    /// Process a single list update
    /// </summary>
    /// <param name="pendingFileUpdates"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<ICollection<IPendingFileUpdate>> ProcessFileUpdates( ICollection<IPendingFileUpdate> pendingFileUpdates, CancellationToken token)
    {
      // the first thing we will do is mark the file as processed.
      // if anything goes wrong _after_ that we will try and 'touch' it again.
      // by doing it that way around we ensure that we never keep the transaction.
      // and we don't run the risk of someone else trying to process this again.
      await _persister.Folders.Files.FileUpdates.MarkFilesProcessedAsync(pendingFileUpdates.Select(p => p.FileId), token).ConfigureAwait(false);

      var tasks = new List<Task<IPendingFileUpdate>>(pendingFileUpdates.Count);
      foreach (var pendingFileUpdate in pendingFileUpdates)
      {
        // throw if need be.
        token.ThrowIfCancellationRequested();

        switch (pendingFileUpdate.PendingUpdateType)
        {
          case UpdateType.Created:
            // only add the pending updates where there are actual words to add.
            tasks.Add(WorkCreatedAsync( pendingFileUpdate, token));
            break;

          case UpdateType.Deleted:
            tasks.Add(WorkDeletedAsync( pendingFileUpdate, token));
            break;

          case UpdateType.Changed:
            // renamed or content/settingss changed
            tasks.Add(WorkChangedAsync( pendingFileUpdate, token));
            break;

          default:
            throw new ArgumentOutOfRangeException();
        }
      }

      // and return the completed updates.
      return await helper.Wait.WhenAll(tasks, _logger, token).ConfigureAwait(false);
    }

    #region Workers
    /// <summary>
    /// A folder was deleted
    /// </summary>
    /// <param name="pendingFileUpdate"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<IPendingFileUpdate> WorkDeletedAsync( IPendingFileUpdate pendingFileUpdate, CancellationToken token)
    {
      var fileId = pendingFileUpdate.FileId;

      // because the file was deleted from the db we must remove the words for it.
      // we could technically end up with orphan words, (words with no file ids for it)
      // but that's not really that important, maybe one day we will vacuum those words?.
      if (!await _persister.FilesWords.DeleteFileAsync(fileId, token).ConfigureAwait(false))
      {
        return null;
      }
      return pendingFileUpdate;
    }

    /// <summary>
    /// A folder was created
    /// </summary>
    /// <param name="pendingFileUpdate"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task<IPendingFileUpdate> WorkCreatedAsync(IPendingFileUpdate pendingFileUpdate, CancellationToken token)
    {
      // get the file we just created.
      var file = pendingFileUpdate.File;
      if (null == file)
      {
        _logger.Warning( $"Files processor: Unable to find, and process created file id: {pendingFileUpdate.FileId}.");
        return null;
      }

      //  process it...
      return ProcessFile( pendingFileUpdate, token );
    }

    /// <summary>
    /// A folder was changed
    /// </summary>
    /// <param name="pendingFileUpdate"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<IPendingFileUpdate> WorkChangedAsync(
      IPendingFileUpdate pendingFileUpdate, 
      CancellationToken token)
    {
      var file = pendingFileUpdate.File;
      if (null == file)
      {
        _logger.Warning($"Files processor: Unable to find, and process changed id: {pendingFileUpdate.FileId}.");
        return null;
      }

      // the file we are processing
      var fileId = pendingFileUpdate.FileId;

      // before we process the file, we need to delete all
      // the words that are attached to it.
      await _persister.FilesWords.DeleteFileAsync(fileId, token).ConfigureAwait(false);

      // then we can process it...
      return await ProcessFile( pendingFileUpdate, token).ConfigureAwait(false);
    }
    #endregion

    #region Processors
    /// <summary>
    /// Process a file.
    /// </summary>
    /// <param name="pendingFileUpdate"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<IPendingFileUpdate> ProcessFile( IPendingFileUpdate pendingFileUpdate, CancellationToken token )
    {
      // is this file ignored?
      if (IsIgnored(pendingFileUpdate))
      {
        _logger.Information( $"Files processor: Ignoring file: {pendingFileUpdate.File.FullName} as per IgnoreFiles rule(s).");
        return null;
      }

      // start all the parser tasks
      var tasks = new List<Task<long>>();

      // the tasks that will be inserting words.
      long[] totalWords;

      // create the helper.
      using (var parserHelper = new PrarserHelper( pendingFileUpdate.File, _persister, pendingFileUpdate.FileId))
      {
        tasks.AddRange(_parsers.Select(parser => ProcessFile(parserHelper, parser, pendingFileUpdate.File, token)));

        // do we have any work to do?
        if (!tasks.Any())
        {
          // nothing to do...
          return null;
        }

        // wait for all the parsers to do their work
        totalWords = await helper.Wait.WhenAll(tasks, _logger, token).ConfigureAwait(false);
      }

      if (totalWords == null || totalWords.Sum() == 0)
      {
        // nothing was done, nothing to do...
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
      // check for 'pending' files
      // that might now be in an ignored directory.
      if (_directory.IsIgnored(pendingFileUpdate.File.Directory))
      {
        return true;
      }
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
        try
        {
          // look for the words
          var numberOfWords = await parser.ParseAsync(helper, _logger, token).ConfigureAwait(false);
          if (numberOfWords > 0)
          {
            // if we found any, log it.
            _logger.Verbose($"Files processor: {parser.Name} processed {helper.Count} words in {file.FullName}.");
          }

          // null values are ignored.
          return numberOfWords;
        }
        catch (DirectoryNotFoundException)
        {
          // the directory does not exist anymore
          var directory = Path.GetDirectoryName(file.FullName);
          if (directory != null)
          {
            var directoryInfo = new DirectoryInfo(directory);
            await _persister.Folders.DeleteDirectoryAsync( directoryInfo, token).ConfigureAwait(false);
          }
          _logger.Warning( $"The directory {directory} does not exist" );

          // in any case, we found nothing in that file
          return 0;
        }
        catch (FileNotFoundException)
        {
          // file does not exist anymore...
          await _persister.Folders.Files.DeleteFileAsync(file as FileInfo, token).ConfigureAwait(false);
          _logger.Warning($"The file {file.FullName} does not exist");

          // in any case, we found nothing in that file
          return 0;
        }
      }
    }
    #endregion

    #region Database calls.

    /// <summary>
    /// Get the pending update
    /// </summary>
    /// <param name="numberOfIles">The number of files we will check at a time.</param>
    /// <param name="factory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<IList<IPendingFileUpdate>> GetPendingFileUpdatesAndMarkFileProcessedAsync(long numberOfIles, IConnectionFactory factory, CancellationToken token)
    {
      var pendingUpdates = await _persister.Folders.Files.FileUpdates.GetPendingFileUpdatesAsync(numberOfIles, factory, token).ConfigureAwait(false);
      if (null != pendingUpdates)
      {
        // return null if we found nothing
        return !pendingUpdates.Any() ? null : pendingUpdates;
      }

      _logger.Error("Files processor: Unable to get any pending file updates.");
      return null;
    }
    #endregion
  }
}
