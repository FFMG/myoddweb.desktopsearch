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
using System.Collections.Generic;
using System.Threading;

namespace myoddweb.desktopsearch.interfaces.IO
{
  public class Words : HashSet<IWord>
  {
    /// <inheritdoc />
    /// <summary>
    /// Internal class to allow us to manage the sting inserts.
    /// </summary>
    internal class InternalWord : IWord
    {
      public string Word { get; set; }
    }

    /// <summary>
    /// Create a empty words list.
    /// </summary>
    public static Words None => default(Words);

    /// <inheritdoc />
    /// <summary>
    /// Base constructor.
    /// </summary>
    public Words() : base(new WordEqualityComparer())
    {
    }

    /// <inheritdoc />
    /// <summary>
    /// Constructor with a single word.
    /// </summary>
    /// <param name="word"></param>
    public Words(IWord word ) : this()
    {
      // just add this word.
      Add(word);
    }

    /// <inheritdoc />
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
