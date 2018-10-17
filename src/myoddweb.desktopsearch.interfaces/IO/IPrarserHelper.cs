using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.interfaces.IO
{
  public interface IPrarserHelper
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
    /// <returns></returns>
    Task<bool> AddWordAsync(IReadOnlyList<string> words, CancellationToken token );

    /// <summary>
    /// 
    /// </summary>
    void Commit();
  }
}