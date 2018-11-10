using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.interfaces.IO
{
  public interface IParserHelper : IDisposable
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
    /// Add multiple words.
    /// </summary>
    /// <param name="words"></param>
    /// <param name="token"></param>
    /// <returns>The number of words actually added.</returns>
    Task<long> AddWordsAsync(IReadOnlyList<string> words, CancellationToken token );
  }
}