using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.helper.Persisters;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal class SqlitePersisterWordsParts : IWordsParts
  {
    #region Member variables
    /// <summary>
    /// The word parts helper used durring transaction.
    /// </summary>
    private IWordsPartsHelper _wordsPartsHelper;

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;
    #endregion

    /// <inheritdoc />
    public string TableName => Tables.WordsParts;

    public SqlitePersisterWordsParts(ILogger logger)
    {
      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task AddOrUpdateWordParts
    (
      long wordId, 
      HashSet<long> partIds, 
      CancellationToken token
    )
    {
      //  sanity check
      Contract.Assert( _wordsPartsHelper != null );

      // first we need to get the part ids for this word.
      var currentIds = await GetWordParts(wordId, token).ConfigureAwait(false);

      // remove the ones that are in the current list but not in the new one
      // and add the words that are in the new list but not in the old one.
      // we want to add all the files that are on disk but not on record.
      var idsToAdd = helper.Collection.RelativeComplement(currentIds, partIds);

      // we want to remove all the files that are on record but not on file.
      var idsToRemove = helper.Collection.RelativeComplement(partIds, currentIds);

      // add the words now
      try
      {
        // try and insert the ids directly
        await InsertWordParts(wordId, idsToAdd, token).ConfigureAwait(false);

        // and try and remove the ids directly
        await DeleteWordParts(wordId, idsToRemove, token).ConfigureAwait(false);
      }
      catch (OperationCanceledException e)
      {
        _logger.Warning("Received cancellation request - Add or update word parts in word");
        // is it my token?
        if (e.CancellationToken != token)
        {
          _logger.Exception(e);
        }

        throw;
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        throw;
      }
    }

    /// <inheritdoc />
    public void Prepare(IPersister persister, IConnectionFactory factory)
    {
      // sanity check.
      Contract.Assert(_wordsPartsHelper != null);

      _wordsPartsHelper = new WordsPartsHelper(factory, TableName);
    }

    /// <inheritdoc />
    public void Complete(bool success)
    {
      _wordsPartsHelper?.Dispose();
      _wordsPartsHelper = null;
    }

    #region Private parts function
    /// <summary>
    /// Remove a bunch of word parts ascociated to a word id.
    /// </summary>
    /// <param name="wordId"></param>
    /// <param name="partIds"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task DeleteWordParts( long wordId, IReadOnlyCollection<long> partIds, CancellationToken token )
    {
      // we might not have anything to do
      if (!partIds.Any())
      {
        return;
      }

      try
      {
        Contract.Assert(_wordsPartsHelper != null );
        foreach (var partId in partIds)
        {
          // get out if needed.
          token.ThrowIfCancellationRequested();

          if (!await _wordsPartsHelper.DeleteAsync(wordId, partId, token).ConfigureAwait(false))
          {
            _logger.Error($"There was an issue deleting part from word: {partId}/{wordId} to persister");
          }
        }
      }
      catch (OperationCanceledException e)
      {
        _logger.Warning( $"Received cancellation request - Deletting parts for word id {wordId}");
        // is it my token?
        if (e.CancellationToken != token)
        {
          _logger.Exception(e);
        }

        throw;
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        throw;
      }
    }

    /// <summary>
    /// Add a list of ids/words to the table if there are any to add.
    /// </summary>
    /// <param name="wordId"></param>
    /// <param name="partIds"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task InsertWordParts( long wordId, IReadOnlyCollection<long> partIds, CancellationToken token )
    {
      // we might not have anything to do
      if (!partIds.Any())
      {
        return;
      }

      try
      {
        Contract.Assert(_wordsPartsHelper != null);
        foreach (var partId in partIds)
        {
          token.ThrowIfCancellationRequested();
          if (await _wordsPartsHelper.InsertAsync(wordId, partId, token).ConfigureAwait(false))
          {
            continue;
          }
          _logger.Error($"There was an issue adding part to word: {partId}/{wordId} to persister");
        }
      }
      catch (OperationCanceledException e)
      {
        _logger.Warning( $"Received cancellation request - Insert parts of word {wordId}.");
        // is it my token?
        if (e.CancellationToken != token)
        {
          _logger.Exception(e);
        }
        throw;
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        throw;
      }
    }

    /// <summary>
    /// Get all the part ids for the given word.
    /// </summary>
    /// <param name="wordId"></param>
    /// <param name="token"></param>
    private async Task<HashSet<long>> GetWordParts( long wordId, CancellationToken token )
    {
      try
      {
        //  sanity check
        Contract.Assert(_wordsPartsHelper != null);

        // just get the values directly.
        return new HashSet<long>(await _wordsPartsHelper.GetPartIdsAsync(wordId, token ).ConfigureAwait(false));
      }
      catch (OperationCanceledException)
      {
        _logger.Warning("Received cancellation request - Get Parts id for word");
        throw;
      }
      catch (Exception e)
      {
        _logger.Exception(e);
        throw;
      }
    }
    #endregion
  }
}
