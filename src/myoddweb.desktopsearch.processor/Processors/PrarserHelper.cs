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
using myoddweb.desktopsearch.helper.Persisters;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.processor.Processors
{
  internal class PrarserHelper : IParserHelper
  {
    #region Member variables
    /// <inheritdoc />
    public FileSystemInfo File { get; }

    /// <inheritdoc />
    public long Count { get; protected set; }

    /// <summary>
    /// The file id we are busy parsing.
    /// </summary>
    private readonly long _fileId;

    /// <summary>
    /// The persister so we can save the words.
    /// </summary>
    private readonly IParserWords _parserWords;

    /// <summary>
    /// The words helper.
    /// </summary>
    private readonly IWordsHelper _wordsHelper;

    /// <summary>
    /// The files words helper.
    /// </summary>
    private readonly IFilesWordsHelper _filesWordsHelper;

    /// <summary>
    /// The parser words helper.
    /// </summary>
    private readonly IParserWordsHelper _parserWordsHelper;

    /// <summary>
    /// The file words helper
    /// </summary>
    private readonly IParserFilesWordsHelper _parserFilesWordsHelper;

    /// <summary>
    /// The max number of words we want to add at a time.
    /// </summary>
    private readonly long _updatesFilesPerEvent;

    private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);
    #endregion

    public PrarserHelper(
      long updatesFilesPerEvent,
      FileSystemInfo file, IPersister persister, IConnectionFactory factory, long fileid) :
      this
      (
        updatesFilesPerEvent,
        file, 
        persister.ParserWords, 
        new WordsHelper( factory, persister.Words.TableName),
        new FilesWordsHelper(factory, persister.FilesWords.TableName),
        new ParserWordsHelper(factory, persister.ParserWords.TableName), 
        new ParserFilesWordsHelper(factory, persister.ParserFilesWords.TableName), 
        fileid 
      )
    {
    }

    public PrarserHelper(
      long updatesFilesPerEvent,
      FileSystemInfo file, 
      IParserWords parserWords,
      IWordsHelper wordsHelper,
      IFilesWordsHelper filesWordsHelper,
      IParserWordsHelper parserWordsHelper,
      IParserFilesWordsHelper parserFilesWordsHelper, 
      long fileid 
    )
    {
      _updatesFilesPerEvent = updatesFilesPerEvent;
      _fileId = fileid;

      // set the perister and the transaction.
      _parserWords = parserWords ?? throw new ArgumentNullException(nameof(parserWords));
      _wordsHelper = wordsHelper ?? throw new ArgumentNullException(nameof(wordsHelper));
      _filesWordsHelper = filesWordsHelper ?? throw new ArgumentNullException(nameof(filesWordsHelper));
      _parserWordsHelper = parserWordsHelper ?? throw new ArgumentNullException(nameof(parserWordsHelper));
      _parserFilesWordsHelper = parserFilesWordsHelper ?? throw new ArgumentNullException(nameof(parserFilesWordsHelper));

      // set the file being worked on.
      File = file ?? throw new ArgumentNullException(nameof(file));

      // we added nothing yet.
      Count = 0;
    }

    /// <inheritdoc /> 
    public void Dispose()
    {
      // dispose of word helper.
      _wordsHelper?.Dispose();
      _filesWordsHelper?.Dispose();
      _parserFilesWordsHelper?.Dispose();
      _parserWordsHelper?.Dispose();
    }

    private async Task<long> AddWordsDirectlyAsync(IEnumerable<string> words, CancellationToken token)
    {
      var added = 0;
      foreach (var word in words)
      {
        // get the id
        var wordId = await _wordsHelper.InsertAndGetIdAsync(word, token).ConfigureAwait(false);

        // get the other files.
        var fileIds = await _parserFilesWordsHelper.GetFileIdsAsync(wordId, token);

        if (fileIds != null && fileIds.Any())
        {
          fileIds.Add(_fileId);
        }
        else
        {
          fileIds = new List<long> { _fileId };
        }

        foreach (var fileId in fileIds)
        {
          // link the id to that file.
          await _filesWordsHelper.InsertAsync(wordId, fileId, token).ConfigureAwait(false);
        }

        // we only added one word.
        ++added;
      }

      // we 'added' the word.
      // technically the word might already exist.
      await SafeAddAsync( added, token ).ConfigureAwait(false);
      return added;
    }

    private async Task SafeAddAsync(long added, CancellationToken token)
    {
      await _semaphoreSlim.WaitAsync(token).ConfigureAwait(false);
      try
      {
        Count += added;
      }
      finally
      {
        _semaphoreSlim.Release();
      }
    }

    /// <inheritdoc /> 
    public async Task<long> AddWordsAsync(IReadOnlyList<string> words, CancellationToken token)
    {
      if (Count < _updatesFilesPerEvent)
      {
        return await AddWordsDirectlyAsync(words, token).ConfigureAwait(false);
      }

      return await AddWordsToParsersAsync(words, token).ConfigureAwait(false);
    }


    private async Task<long> AddWordsToParsersAsync(IReadOnlyList<string> words, CancellationToken token)
    {
      // then we just try and add the word.
      var added = await _parserWords.AddWordsAsync(
        _fileId, 
        words,
        _parserWordsHelper,
        _parserFilesWordsHelper,
        token).ConfigureAwait(false);

      // we 'added' the word.
      // technically the word might already exist.
      await SafeAddAsync( added, token ).ConfigureAwait(false);

      // success.
      return added;
    }
  }
}
