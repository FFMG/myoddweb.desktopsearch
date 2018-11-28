using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.helper.IO;
using myoddweb.desktopsearch.helper.Persisters;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.processor.Processors
{
  internal class ParserWordsAndFilesHelper : IDisposable
  {
    #region Member variable
    private readonly IPersister _persister;
    private readonly ILogger _logger;

    private readonly IWordsHelper _wordsHelper;
    private readonly IPartsHelper _partsHelper;
    private readonly IFilesWordsHelper _filesWordsHelper;
    private readonly IWordsPartsHelper _wordsPartsHelper;
    private readonly IParserWordsHelper _parserWordsHelper;
    private readonly IParserFilesWordsHelper _parserFilesWordsHelper;
    #endregion

    public ParserWordsAndFilesHelper( IConnectionFactory factory, IPersister persister, ILogger logger )
    {
      _persister = persister ?? throw new ArgumentNullException(nameof(persister));
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      _wordsHelper = new WordsHelper(factory, persister.Words.TableName);
      _partsHelper = new PartsHelper(factory, persister.Parts.TableName);
      _filesWordsHelper = new FilesWordsHelper(factory, persister.FilesWords.TableName);
      _wordsPartsHelper = new WordsPartsHelper(factory, persister.WordsParts.TableName);
      _parserWordsHelper = new ParserWordsHelper(factory, persister.ParserWords.TableName, persister.ParserFilesWords.TableName);
      _parserFilesWordsHelper = new ParserFilesWordsHelper(factory, persister.ParserFilesWords.TableName);
    }

    public void Dispose()
    {
      _wordsHelper.Dispose();
      _partsHelper.Dispose();
      _filesWordsHelper.Dispose();
      _wordsPartsHelper.Dispose();
      _parserWordsHelper.Dispose();
      _parserFilesWordsHelper.Dispose();
    }
    
    /// <summary>
    /// Process a single parser word
    /// </summary>
    /// <param name="word"></param>
    /// <param name="fileIds"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task ProcessPendingParserWordAsync
    (
      IWord word,
      IList<long> fileIds, 
      CancellationToken token
    )
    {
      // is this a valid word?
      if (!_persister.Words.IsValidWord(word))
      {
        // no, it is not, so just get rid of it.
        var id = await _parserWordsHelper.GetIdAsync(word.Value, token).ConfigureAwait(false);
        await _parserWordsHelper.DeleteWordAsync(id, token).ConfigureAwait(false);
        await _parserFilesWordsHelper.DeleteWordAsync(id, token).ConfigureAwait(false);

        // if we found any, log it.
        _logger.Verbose($"Parser processor: Deleted word {word.Value} for {fileIds.Count} file(s) as it is not a valid word.");

        return;
      }

      if (!await _persister.FilesWords.AddParserWordsAsync(
          word,
          fileIds,
          _wordsHelper,
          _filesWordsHelper,
          _partsHelper,
          _wordsPartsHelper,
          token)
        .ConfigureAwait(false))
      {
        // there was an issue adding those words for that file id.
        return;
      }

      // get the id of this word.
      var parserWordId = await _parserWordsHelper.GetIdAsync(word.Value, token).ConfigureAwait(false);

      // delete all the file ids that used that parser word id.
      // because we have just processed them all at once.
      await _persister.ParserWords.DeleteFileIds(parserWordId, fileIds, _parserFilesWordsHelper, token).ConfigureAwait(false);

      // process the parts.
      await ProcessPartsAsync(word, token).ConfigureAwait(false);

      // if we found any, log it.
      _logger.Verbose($"Parser processor: {word.Value} processed for {fileIds.Count} file(s).");
    }

    private async Task ProcessPartsAsync(IWord word, CancellationToken token)
    {
      foreach (var part in word.Parts)
      {
        // do do the word we are currently working on...
        if (part == word.Value)
        {
          continue;
        }

        // if this part exists as a word ... then we can add it 
        var wordId = await _parserWordsHelper.GetIdAsync(part, token).ConfigureAwait(false);
        if (-1 == wordId)
        {
          continue;
        }

        // that word does exist... so we can add it to the words list.
        var fileIds = await _parserFilesWordsHelper.GetFileIdsAsync(wordId, token).ConfigureAwait(false);
        if (!fileIds.Any())
        {
          await _parserWordsHelper.DeleteWordAsync(wordId, token).ConfigureAwait(false);
          _logger.Verbose($"Parser processor: Deleted word {word.Value} as we found no file ids for it.");
          continue;
        }

        // go back around again.
        // NB: We know that this is a part of a larger word.
        //     So, "maxNumCharactersPerParts", (second param of Word(...))
        //     Cannot be longer than the word itself.
        await ProcessPendingParserWordAsync(new Word( part, part.Length ), fileIds, token).ConfigureAwait(false);
      }
    }
  }
}
