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
using myoddweb.desktopsearch.helper.Persisters;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.processor
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
    /// The connection factory, if we used one.
    /// </summary>
    private readonly IConnectionFactory _factory;

    /// <summary>
    /// The persister so we can save the words.
    /// </summary>
    private readonly IPersister _persister;

    /// <summary>
    /// The words helper.
    /// </summary>
    private readonly IWordsHelper _wordsHelper;

    /// <summary>
    /// The files words helper.
    /// </summary>
    private readonly IFilesWordsHelper _filesWordsHelper;
    #endregion

    public PrarserHelper(FileSystemInfo file, IPersister persister, IConnectionFactory factory, long fileid) :
      this(file, persister, 
        new Words( factory, persister.Words.TableName),
        new FilesWordsHelper(factory, persister.FilesWords.TableName),
        factory, fileid )
    {
    }

    public PrarserHelper(
      FileSystemInfo file, 
      IPersister persister,
      IWordsHelper wordsHelper,
      IFilesWordsHelper filesWordsHelper,
      IConnectionFactory factory, 
      long fileid 
    )
    {
      _fileId = fileid;

      // set the perister and the transaction.
      _persister = persister ?? throw new ArgumentNullException(nameof(persister));
      _factory = factory ?? throw new ArgumentNullException(nameof(factory));
      _wordsHelper = wordsHelper ?? throw new ArgumentNullException(nameof(wordsHelper));
      _filesWordsHelper = filesWordsHelper ?? throw new ArgumentNullException(nameof(filesWordsHelper));

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
    }

    /// <inheritdoc /> 
    public async Task<long> AddWordsAsync(IReadOnlyList<string> words, CancellationToken token)
    {
      // then we just try and add the word.
      var added = await _persister.ParserWords.AddWordsAsync(
        _fileId, 
        words,
        _wordsHelper,
        _filesWordsHelper,
        _factory, 
        token).ConfigureAwait(false);

      // we 'added' the word.
      // technically the word might already exist.
      Count += added;

      // success.
      return added;
    }
  }
}
