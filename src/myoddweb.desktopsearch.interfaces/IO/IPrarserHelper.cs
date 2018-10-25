using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.interfaces.IO
{
  public interface IParserHelper
  {
    /// <summary>
    /// The file that is being processed.
    /// </summary>
    FileSystemInfo File { get; }

    /// <summary>
    /// The number of words added.
    /// </summary>
    long Count { get; }

    /// <summary>
    /// Add a word to the list.
    /// </summary>
    /// <param name="words"></param>
    /// <param name="token"></param>
    /// <returns>The number of words actually added.</returns>
    Task<long> AddWordAsync(IReadOnlyList<string> words, CancellationToken token );

    /// <summary>
    /// commit the transaction, if we have one.
    /// </summary>
    void Commit();

    /// <summary>
    /// Rollback the transaction if we have one.
    /// </summary>
    void Rollback();
  }
}