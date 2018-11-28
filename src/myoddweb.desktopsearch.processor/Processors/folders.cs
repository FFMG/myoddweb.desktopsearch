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
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.helper.Performance;
using myoddweb.desktopsearch.interfaces.Enums;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.processor.Processors
{
  internal class Folders : IProcessor
  {
    #region Member Variables
    /// <summary>
    /// The logger that we will be using to log messages.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The directory parser we will be using.
    /// </summary>
    private readonly IDirectory _directory;

    /// <summary>
    /// The persister.
    /// </summary>
    private readonly IPersister _persister;

    /// <summary>
    /// The number of updates we want to try and do at a time.
    /// </summary>
    public int MaxNumberFoldersToProcess { get; }

    /// <summary>
    /// The performance counter.
    /// </summary>
    private readonly ICounter _counter;
    #endregion

    public Folders(ICounter counter,
      int maxNumberFoldersToProcess,
      IPersister persister, 
      ILogger logger, 
      IDirectory directory)
    {
      if (maxNumberFoldersToProcess <= 0)
      {
        throw new ArgumentException($"The number of folders to try per events cannot be -ve or zero, ({maxNumberFoldersToProcess})");
      }
      MaxNumberFoldersToProcess = maxNumberFoldersToProcess;

      // save the counter
      _counter = counter ?? throw new ArgumentNullException(nameof(counter));

      // set the persister.
      _persister = persister ?? throw new ArgumentNullException(nameof(persister));

      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      // save the directory parser
      _directory = directory ?? throw new ArgumentNullException(nameof(directory));
    }

    /// <inheritdoc />
    public void Stop()
    {
      _counter?.Dispose();
    }

    /// <inheritdoc />
    public async Task<long> WorkAsync(IConnectionFactory factory, CancellationToken token)
    {
      using (_counter.Start())
      { 
        try
        {
          // then get _all_ the file updates that we want to do.
          var pendingUpdates = await GetPendingFolderUpdatesAndMarkDirectoryProcessedAsync(factory, token).ConfigureAwait(false);
          if (null == pendingUpdates)
          {
            //  probably was canceled.
            return 0;
          }

          // process the update.
          await ProcessFolderUpdatesAsync(factory, pendingUpdates, token).ConfigureAwait(false);

          // we processed one update
          return pendingUpdates.Count;
        }
        catch (OperationCanceledException)
        {
          _logger.Warning("Directory processor: Received cancellation request - Directories Processor - Work");
          throw;
        }
        catch (Exception e)
        {
          _logger.Exception("Directory processor: ", e);
          throw;
        }
      }
    }

    private async Task ProcessFolderUpdatesAsync(IConnectionFactory factory, ICollection<IPendingFolderUpdate> pendingFolderUpdates, CancellationToken token)
    {
      // the first thing we will do is mark the folder as processed.
      // if anything goes wrong _after_ that we will try and 'touch' it again.
      // by doing it that way around we ensure that we never keep the transaction.
      // and we don't run the risk of someone else trying to process this again.
      await _persister.Folders.FolderUpdates.MarkDirectoriesProcessedAsync(pendingFolderUpdates.Select( f => f.FolderId ), factory, token).ConfigureAwait(false);

      var tasks = new List<Task>(pendingFolderUpdates.Count);
      foreach (var pendingFolderUpdate in pendingFolderUpdates)
      {
        // throw if need be.
        token.ThrowIfCancellationRequested();

        tasks.Add(ProcessFolderUpdateAsync(factory, pendingFolderUpdate, token));
      }

      // the 'continuewith' step is to pass all the words that we found.
      // this will be within it's own transaction
      // but the parsing has been done already.
      await helper.Wait.WhenAll(tasks, _logger, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Process a single folder update
    /// </summary>
    /// <param name="factory"></param>
    /// <param name="pendingFolderUpdate"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private Task ProcessFolderUpdateAsync(IConnectionFactory factory, IPendingFolderUpdate pendingFolderUpdate, CancellationToken token)
    {
      switch (pendingFolderUpdate.PendingUpdateType)
      {
        case UpdateType.Created:
          return WorkCreatedAsync(factory, pendingFolderUpdate, token);

        case UpdateType.Deleted:
          return WorkDeletedAsync(factory, pendingFolderUpdate, token);

        case UpdateType.Changed:
          // renamed or content/settingss changed
          return WorkChangedAsync(factory, pendingFolderUpdate, token);

        case UpdateType.None:
          throw new InvalidEnumArgumentException( "The 'none' argument cannot be used here." );

        default:
          throw new ArgumentOutOfRangeException();
      }
    }

    #region Workers

    /// <summary>
    /// A folder was created
    /// </summary>
    /// <param name="factory"></param>
    /// <param name="pendingUpdate"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task WorkCreatedAsync(IConnectionFactory factory, IPendingFolderUpdate pendingUpdate, CancellationToken token)
    {
      var directory = pendingUpdate.Directory;
      if (null == directory)
      {
        // we are done 
        return;
      }

      // get the files in that directory.
      var files = await _directory.ParseDirectoryAsync(directory, token).ConfigureAwait(false);
      if (files == null)
      {
        // there is nothing to add to this directory so we are done.
        _logger.Verbose($"Directory processor: Did not find any files in the new directory: {directory.FullName}.");
        return;
      }

      // and add them to the persiser
      if (await _persister.Folders.Files.AddOrUpdateFilesAsync(files, factory, token).ConfigureAwait(false))
      {
        // log what we just did
        _logger.Verbose($"Directory processor: Found {files.Count} file(s) in the new directory: {directory.FullName}.");
      }
      else
      {
        _logger.Error($"Directory processor: Unable to add {files.Count} file(s) from the new directory: {directory.FullName}.");
      }
    }

    /// <summary>
    /// A folder was deleted
    /// </summary>
    /// <param name="factory"></param>
    /// <param name="pendingUpdate"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task WorkDeletedAsync(IConnectionFactory factory, IPendingFolderUpdate pendingUpdate, CancellationToken token)
    {
      var directory = pendingUpdate.Directory;
      if (null == directory)
      {
        // we are done 
        return;
      }

      // using the foler id is the fastest.
      if (!await _persister.Folders.Files.DeleteFilesAsync(pendingUpdate.FolderId, factory, token))
      {
        _logger.Verbose($"Directory processor: Unable to delete directory: {directory.FullName}.");
        return;
      }
      _logger.Verbose($"Directory processor: {directory.FullName} was deleted.");
    }

    /// <summary>
    /// A folder was changed
    /// </summary>
    /// <param name="factory"></param>
    /// <param name="pendingUpdate"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task WorkChangedAsync(IConnectionFactory factory, IPendingFolderUpdate pendingUpdate, CancellationToken token)
    {
      // look for the directory name, if we found nothing on record
      // then we will have to go and look in the database.
      var directory = pendingUpdate.Directory;

      // if we have no directory then we have nothing...
      if (directory == null)
      {
        // we are done 
        return;
      }

      // Get the files currently on record
      // this can be null if we have nothing.
      var filesOnRecord = pendingUpdate.Files;

      // then get the ones in the file.
      var filesOnFile = await _directory.ParseDirectoryAsync(directory, token).ConfigureAwait(false);

      // we want to add all the files that are on disk but not on record.
      var filesToAdd = helper.File.RelativeComplement(filesOnRecord, filesOnFile);

      // we want to remove all the files that are on record but not on file.
      var filesToRemove = helper.File.RelativeComplement(filesOnFile, filesOnRecord);

      // if we have nothing to do... then get out
      if (!filesToRemove.Any() && !filesToAdd.Any())
      {
        _logger.Verbose($"Directory processor: {directory.FullName} was changed but no files were changed.");
        return;
      }

      // We know that the helper functions never return anything null...
      if (filesToRemove.Any())
      {
        await _persister.Folders.Files.DeleteFilesAsync(filesToRemove, factory, token).ConfigureAwait(false);
      }

      // anything to add?
      if (filesToAdd.Any())
      {
        await _persister.Folders.Files.AddOrUpdateFilesAsync(filesToAdd, factory, token).ConfigureAwait(false);
      }

      _logger.Verbose($"Directory processor: {directory.FullName} was changed {filesToRemove.Count} file(s) removed and {filesToAdd.Count} file(s) added.");
    }
    #endregion

    #region Database calls

    /// <summary>
    /// Get the folder updates
    /// </summary>
    /// <param name="factory"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<IList<IPendingFolderUpdate>> GetPendingFolderUpdatesAndMarkDirectoryProcessedAsync(IConnectionFactory factory, CancellationToken token)
    {
      var pendingUpdates = await _persister.Folders.FolderUpdates.GetPendingFolderUpdatesAsync(MaxNumberFoldersToProcess, factory, token).ConfigureAwait(false);
      if (null == pendingUpdates)
      {
        _logger.Error("Directory processor: Unable to get any pending folder updates.");
        return null;
      }

      // return null if we have nothing.
      return !pendingUpdates.Any() ? null : pendingUpdates;
    }
    #endregion
  }
}
