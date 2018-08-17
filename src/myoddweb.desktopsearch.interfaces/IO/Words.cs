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
using System.Linq;
using System.Threading;

namespace myoddweb.desktopsearch.interfaces.IO
{
  public class Words : List<Word>
  {
    /// <inheritdoc />
    /// <summary>
    /// Base constructor.
    /// </summary>
    public Words()
    {
    }

    /// <inheritdoc />
    /// <summary>
    /// Constructor with a single word.
    /// </summary>
    /// <param name="word"></param>
    public Words(Word word ) : this()
    {
      // just add this word.
      Add(word);
    }

    /// <inheritdoc />
    /// <summary>
    /// Constructor with a list of words.
    /// </summary>
    /// <param name="words"></param>
    public Words(Words[] words ) : base( words.Sum( w => w.Count) )
    {
      // Add all he words into one.
      Add(words);
    }

    public Words(Word[] words) : base( words.Count() )
    {
      // Add all he words into one.

      Add(words);
    }

    public Words(IReadOnlyCollection<string> words) : base(words.Count)
    {
      // Add all he words into one.
      Add(words);
    }

    #region Private Manipulators
    /// <summary>
    /// Join multiple list of words together.
    /// </summary>
    /// <param name="words"></param>
    /// <param name="token"></param>
    private void AddAllowDuplicates(Words words, CancellationToken token = default(CancellationToken))
    {
      foreach (var word in words)
      {
        // check if we need to get out.
        token.ThrowIfCancellationRequested();

        // ignore null or empty sets.
        if (word == null)
        {
          continue;
        }

        // check the union
        Add(word);
      }
    }

    /// <summary>
    /// Join multiple list of words together.
    /// </summary>
    /// <param name="words"></param>
    /// <param name="token"></param>
    private void Add(Words[] words, CancellationToken token = default(CancellationToken))
    {
      var sum = words.Sum(w => w.Count());
      if (sum == 0)
      {
        return;
      }
      // reset the capacity
      ResizeCapacity(sum);

      foreach (var w in words)
      {
        // check if we need to get out.
        token.ThrowIfCancellationRequested();

        // ignore null or empty sets.
        if (w == null || !w.Any())
        {
          continue;
        }

        // check the union
        AddAllowDuplicates(w, token);
      }

      // make it distinct
      Distinct();
    }

    /// <summary>
    /// Create destinct List.
    /// </summary>
    private void Distinct()
    {
      var distinct = this.Distinct(new WordEqualityComparer()).ToArray();
      Clear();
      Capacity = distinct.Count();
      AddRange( distinct );
    }
    
    /// <summary>
    /// Reset the capacity.
    /// </summary>
    /// <param name="newSize"></param>
    private void ResizeCapacity(int newSize)
    {
      Capacity += (newSize - (Capacity - Count));
    }
    #endregion

    /// <summary>
    /// Add a single string word to our list.
    /// </summary>
    /// <param name="word"></param>
    public void Add(string word)
    {
      Add(new Word(word));
    }

    /// <summary>
    /// Join multiple list of words together.
    /// </summary>
    /// <param name="words"></param>
    /// <param name="token"></param>
    public void Add(Word[] words, CancellationToken token = default(CancellationToken))
    {
      var sum = words.Count();
      if (sum == 0)
      {
        return;
      }

      // reset the capacity
      ResizeCapacity(sum);
      foreach (var w in words)
      {
        // check if we need to get out.
        token.ThrowIfCancellationRequested();

        // ignore null or empty sets.
        if (w == null)
        {
          continue;
        }

        // check the union
        Add(w);
      }

      Distinct();
    }

    /// <summary>
    /// Join multiple list of words together.
    /// </summary>
    /// <param name="words"></param>
    /// <param name="token"></param>
    public void Add(IReadOnlyCollection<string> words, CancellationToken token = default(CancellationToken))
    {
      var sum = words.Count();
      if (sum == 0)
      {
        return;
      }
      // reset the capacity
      ResizeCapacity(sum);
      foreach (var w in words)
      {
        // check if we need to get out.
        token.ThrowIfCancellationRequested();

        // ignore null or empty sets.
        if (w == null )
        {
          continue;
        }

        // check the union
        Add(w);
      }
    }
  }
}
