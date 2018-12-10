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
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.helper.IO;
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
    /// The persister
    /// </summary>
    private readonly IPersister _persister;

    /// <summary>
    /// The files words helper.
    /// </summary>
    private readonly IFilesWordsHelper _filesWordsHelper;

    /// <summary>
    /// The lock to allow us to update the word counter.
    /// </summary>
    private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);
    #endregion

    public PrarserHelper(
      FileSystemInfo file, IPersister persister, IConnectionFactory factory, long fileid) :
      this
      (
        file, 
        persister,
        new FilesWordsHelper(factory, persister.FilesWords.TableName),
        fileid
      )
    {
    }

    public PrarserHelper(
      FileSystemInfo file, 
      IPersister persister,
      IFilesWordsHelper filesWordsHelper,
      long fileid 
    )
    {
      _fileId = fileid;

      // save the persister
      _persister = persister ?? throw new ArgumentNullException(nameof(persister));

      // set the perister and the transaction.
      _filesWordsHelper = filesWordsHelper ?? throw new ArgumentNullException(nameof(filesWordsHelper));

      // set the file being worked on.
      File = file ?? throw new ArgumentNullException(nameof(file));

      // we added nothing yet.
      Count = 0;
    }

    /// <inheritdoc /> 
    public void Dispose()
    {
      // dispose of the helper.
      _filesWordsHelper?.Dispose();
    }

    /// <inheritdoc /> 
    public async Task<long> AddWordsAsync(IReadOnlyList<string> words, CancellationToken token)
    {
      // add all the words
      var wordIds = await _persister.Words.AddOrGetWordsAsync(new Words(words, _persister.Parts.MaxNumCharactersPerParts ),
        token
      ).ConfigureAwait(false);
      var added = 0;
      foreach (var wordId in wordIds)
      {
        // link the id to that file.
        await _filesWordsHelper.InsertAsync(wordId, _fileId, token).ConfigureAwait(false);

        // we only added one word.
        ++added;
      }

      // we 'added' the word.
      // technically the word might already exist.
      await SafeAddAsync(added, token).ConfigureAwait(false);
      return added;
    }

    #region Private Functions
    /// <summary>
    /// Add a number to the counter making sure that we are within lock
    /// </summary>
    /// <param name="added"></param>
    /// <param name="token"></param>
    /// <returns></returns>
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
    #endregion
  }
}
