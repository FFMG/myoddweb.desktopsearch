using System.Collections.Generic;
using System.Threading;

namespace myoddweb.desktopsearch.interfaces.IO
{
  public class Words : HashSet<IWord>
  {
    /// <inheritdoc />
    internal class InternalWord : IWord
    {
      public string Word { get; set; }
    }

    /// <summary>
    /// Create a empty words list.
    /// </summary>
    public static Words None => default(Words);

    /// <summary>
    /// Base constructor.
    /// </summary>
    public Words() : base(new WordEqualityComparer())
    {
    }

    /// <summary>
    /// Constructor with a single word.
    /// </summary>
    /// <param name="word"></param>
    public Words(IWord word ) : this()
    {
      // just add this word.
      Add(word);
    }

    /// <summary>
    /// Constructor with a list of words.
    /// </summary>
    /// <param name="words"></param>
    public Words(IEnumerable<Words> words ) : this()
    {
      // Add all he words into one.
      UnionWith(words);
    }

    /// <summary>
    /// Add a single string word to our list.
    /// </summary>
    /// <param name="word"></param>
    public void UnionWith(string word )
    {
      Add(new InternalWord {Word = word});
    }

    /// <summary>
    /// Join multiple list of words together.
    /// </summary>
    /// <param name="words"></param>
    /// <param name="token"></param>
    public void UnionWith(IEnumerable<Words> words, CancellationToken token = default(CancellationToken))
    {
      foreach (var w in words)
      {
        // check if we need to get out.
        token.ThrowIfCancellationRequested();

        // check the union
        UnionWith(w);
      }
    }
  }
}
