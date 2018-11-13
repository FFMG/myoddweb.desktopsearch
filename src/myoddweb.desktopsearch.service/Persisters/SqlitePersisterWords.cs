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
using myoddweb.desktopsearch.helper.IO;
using myoddweb.desktopsearch.interfaces.Configs;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Persisters;
using ILogger = myoddweb.desktopsearch.interfaces.Logging.ILogger;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal class SqlitePersisterWords : interfaces.Persisters.IWords, IDisposable
  {
    #region Member variable
    /// <summary>
    /// The counter for adding new words
    /// </summary>
    private readonly SqlPerformanceCounter _counterAddOrUpdate;

    /// <summary>
    /// The counter for inserting new words.
    /// </summary>
    private readonly SqlPerformanceCounter _counterInsertedWords;

    /// <summary>
    /// The maximum number of characters per words parts...
    /// This is not the max word lenght, but the part lenght
    /// The user cannot enter anything longer in a seatch box.
    /// So search queries longer than that... are ignored.
    /// </summary>
    private readonly int _maxNumCharactersPerParts;

    /// <summary>
    /// The maximum word size, words that are longer ... are ignored.
    /// </summary>
    private readonly int _maxNumCharactersPerWords;

    /// <summary>
    /// The words parts interface
    /// </summary>
    private readonly IWordsParts _wordsParts;

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;
    #endregion

    public SqlitePersisterWords(IPerformance performance, IWordsParts wordsParts, int maxNumCharactersPerWords, int maxNumCharactersPerParts, ILogger logger)
    {
      // the number of characters per parts.
      _maxNumCharactersPerParts = maxNumCharactersPerParts;

      // the maximum word len
      _maxNumCharactersPerWords = maxNumCharactersPerWords;

      // the words parts interfaces
      _wordsParts = wordsParts ?? throw new ArgumentNullException(nameof(wordsParts));

      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      // create the counter,
      _counterAddOrUpdate = new SqlPerformanceCounter(performance, "Database: Add Or Update word", _logger);
      _counterInsertedWords = new SqlPerformanceCounter(performance, "Database: Inserted new Words", _logger);
    }

    public void Dispose()
    {
      _counterAddOrUpdate?.Dispose();
      _counterInsertedWords?.Dispose();
    }

    /// <inheritdoc />
    public string TableName => Tables.Words;

    /// <inheritdoc />
    public async Task<long> AddOrUpdateWordAsync(
      IWordsHelper wordsHelper,
      IPartsHelper partsHelper,
      IWordsPartsHelper wordsPartsHelper,
      IWord word, 
      CancellationToken token)
    {
      var ids = await AddOrUpdateWordsAsync( wordsHelper, partsHelper, wordsPartsHelper, new Words(word), token ).ConfigureAwait(false);
      return ids.Any() ? ids.First() : -1;
    }

    /// <inheritdoc />
    public async Task<IList<long>> AddOrUpdateWordsAsync(
      IWordsHelper wordsHelper,
      IPartsHelper partsHelper,
      IWordsPartsHelper wordsPartsHelper,
      interfaces.IO.IWords words, 
      CancellationToken token)
    {
      using (_counterAddOrUpdate.Start())
      {
        using (_counterInsertedWords.Start())
        {
          return await InsertWordsAsync(wordsHelper, partsHelper, words, wordsPartsHelper, token).ConfigureAwait(false);
        }
      }
    }
    
    #region Private word functions

    /// <summary>
    /// Given a list of words, re-create the ones that we need to insert.
    /// </summary>
    /// <param name="wordsHelper"></param>
    /// <param name="partsHelper"></param>
    /// <param name="words"></param>
    /// <param name="wordsPartsHelper"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<IList<long>> InsertWordsAsync(
      IWordsHelper wordsHelper,
      IPartsHelper partsHelper,
      interfaces.IO.IWords words,
      IWordsPartsHelper wordsPartsHelper, 
      CancellationToken token)
    {
      // if we have nothing to do... we are done.
      if (!words.Any())
      {
        return new List<long>();
      }

      // the ids of the words we just added
      var ids = new List<long>( words.Count );

      try
      {
        foreach (var word in words)
        {
          // get out if needed.
          token.ThrowIfCancellationRequested();

          // the word is crazy long, so we are ignoring it...
          if (word.Value.Length > _maxNumCharactersPerWords)
          {
            continue;
          }

          // the word we want to insert.
          var wordId = await InsertWordAsync(word, wordsHelper, partsHelper, wordsPartsHelper, token).ConfigureAwait(false);
          if (-1 == wordId)
          {
            // we log errors in the insert function
            continue;
          }

          // we added this id.
          ids.Add(wordId);
        }

        // all done, return whatever we did.
        return ids;
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Insert multiple words");
        throw;
      }
      catch (Exception ex)
      {
        _logger.Exception(ex);
        throw;
      }
    }

    /// <summary>
    /// Insert a word in the database and then insert all the parts
    /// </summary>
    /// <param name="word"></param>
    /// <param name="wordsHelper"></param>
    /// <param name="partsHelper"></param>
    /// <param name="wordsPartsHelper"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<long> InsertWordAsync(
      IWord word,
      IWordsHelper wordsHelper,
      IPartsHelper partsHelper,
      IWordsPartsHelper wordsPartsHelper,
      CancellationToken token)
    {
      var wordId = await wordsHelper.InsertAndGetIdAsync(word.Value, token).ConfigureAwait(false);
      if ( wordId == -1 )
      {
        _logger.Error($"There was an issue getting the word id: {word.Value} from the persister");
        return -1;
      }

      // we now can add/find the parts for that word.
      var partIds = await GetOrInsertParts( word, partsHelper, token).ConfigureAwait(false);

      // marry the word id, (that we just added).
      // with the partIds, (that we just added).
      await _wordsParts.AddOrUpdateWordParts(wordId, new HashSet<long>(partIds), wordsPartsHelper, token).ConfigureAwait(false);

      // all done return the id of the word.
      return wordId;
    }

    private async Task<IList<long>> GetOrInsertParts(
      IWord word,
      IPartsHelper partsHelper,
      CancellationToken token)
    {
      // get the parts.
      var parts = word.Parts(_maxNumCharactersPerParts);

      // if we have not words... then move on.
      if (!parts.Any())
      {
        return new List<long>();
      }

      // the ids of all the parts, (added or otherwise).
      var partIds = new List<long>(parts.Count);

      foreach (var part in parts)
      {
        // get out if needed.
        token.ThrowIfCancellationRequested();

        // we will do an insert regadless if the value exists or not.
        // this is because we don't just insert if the value exist.
        // there is no point in just geting the value.
        var partId = await partsHelper.InsertAndGetIdAsync(part, token).ConfigureAwait(false);
        if (-1 != partId)
        {
          partIds.Add(partId);
          continue;
        }

        _logger.Error($"There was an issue adding part: {part} to persister");
      }

      // return all the ids that we either
      // added or that already exist.
      return partIds;
    }
    #endregion
  }
}
