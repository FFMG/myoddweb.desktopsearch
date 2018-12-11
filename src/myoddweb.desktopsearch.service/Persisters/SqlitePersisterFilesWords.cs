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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.helper.Persisters;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;
using IWords = myoddweb.desktopsearch.interfaces.Persisters.IWords;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal class SqlitePersisterFilesWords : IFilesWords
  {
    #region Member variables

    /// <summary>
    /// The files helper per transaction.
    /// </summary>
    private IFilesWordsHelper _fileWordsHelper;

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The words interface
    /// </summary>
    private readonly IWords _words;

    #endregion

    public SqlitePersisterFilesWords( IWords words, ILogger logger)
    {
      // the words manager
      _words = words ?? throw new ArgumentNullException(nameof(words));

      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string TableName => Tables.FilesWords;

    /// <inheritdoc />
    public async Task<bool> AddWordToFilesAsync( IWord wordToAdd, IList<long> fileIdsToAddWordTo, CancellationToken token)
    {
      // if we have no files, we don't actually want to add the word.
      if (!fileIdsToAddWordTo.Any())
      {
        return false;
      }

      try
      {
        // try and insert the word into the words table.
        // if the word already exists, we will get the id for it.
        // if the word does not exist, we will add it and get the id for it...
        var wordId = await _words.AddOrUpdateWordAsync( wordToAdd, token ).ConfigureAwait(false);
        if (-1 == wordId)
        {
          _logger.Error($"There was an issue inserting/finding the word : {wordToAdd.Value}.");
          return false;
        }

        // then add the id to the files.
        return await AddWordToFilesAsync(wordId, fileIdsToAddWordTo, token).ConfigureAwait(false);
      }
      catch (OperationCanceledException)
      {
        _logger.Warning( "Received cancellation request - Add pending parser words");
        throw;
      }
      catch (Exception ex)
      {
        _logger.Exception("There was an exception adding parser words", ex);
        throw;
      }
    }

    /// <inheritdoc />
    public async Task<bool> AddWordToFilesAsync(long wordId, IList<long> fileIdsToAddWordTo, CancellationToken token)
    {
      Contract.Assert(_fileWordsHelper != null );

      // if we have no files, we don't actually want to add the word.
      if (!fileIdsToAddWordTo.Any())
      {
        return false;
      }

      try
      {

        // we then go around and 'attach' that word id to that file id.
        // so that when we locate that word, we will have a valid id for it.
        foreach (var fileId in fileIdsToAddWordTo)
        {
          if (!await _fileWordsHelper.InsertAsync(wordId, fileId, token).ConfigureAwait(false))
          {
            _logger.Error($"There was an issue inserting word : {wordId} for file : {fileId}");
          }
        }

        // we are done
        return true;
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Add pending parser words");
        throw;
      }
      catch (Exception ex)
      {
        _logger.Exception("There was an exception adding parser words", ex);
        throw;
      }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteFileAsync(long fileId, CancellationToken token)
    {
      try
      {
        Contract.Assert(_fileWordsHelper != null);

        // all good.
        return await _fileWordsHelper.DeleteFileAsync( fileId, token ).ConfigureAwait( false );
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Deleting File Id from Files/Word.");
        throw;
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        return false;
      }
    }

    /// <inheritdoc />
    public void Prepare(IPersister persister, IConnectionFactory factory)
    {
      // sanity check.
      Contract.Assert(_fileWordsHelper == null);

      _fileWordsHelper = new FilesWordsHelper(factory, TableName);
    }

    /// <inheritdoc />
    public void Complete(bool success)
    {
      _fileWordsHelper?.Dispose();
      _fileWordsHelper = null;
    }
  }
}
