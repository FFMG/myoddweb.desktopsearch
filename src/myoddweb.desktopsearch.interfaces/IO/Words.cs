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
    #region Contructors
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
    public Words(Words[] words ) : base( words?.Where( w => w != null ).Sum( w => w.Count) ?? 0 )
    {
      // Add all he words into one.
      Add(words);
    }

    public Words(Word[] words) : base( words?.Length ?? 0 )
    {
      // Add all he words into one.
      Add(words);
    }

    public Words(IReadOnlyCollection<string> words) : base(words?.Count ?? 0)
    {
      // Add all he words into one.
      Add(words);
    }
    #endregion

    #region Public Manipulator
    /// <summary>
    /// Add a single word... but not if null.
    /// </summary>
    /// <param name="item"></param>
    public new void Add(Word item)
    {
      if (null == item)
      {
        return;
      }
      base.Add(item);

      // make sure that we don't have duplicates
      Distinct();
    }

    /// <summary>
    /// Add a single string word to our list.
    /// </summary>
    /// <param name="word"></param>
    public void Add(string word)
    {
      if (null == word)
      {
        return;
      }
      Add(new Word(word));
    }

    /// <summary>
    /// Join multiple list of words together.
    /// </summary>
    /// <param name="words"></param>
    /// <param name="token"></param>
    public void Add(Word[] words, CancellationToken token = default(CancellationToken))
    {
      var sum = words?.Length ?? Count;
      if (sum == 0 || words == null)
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

        // add this item using the base
        // as we will call distinct() later.
        base.Add(w);
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
      var sum = words?.Count ?? 0;
      if (sum == 0 || words == null)
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

        // add this item
        base.Add(new Word(w));
      }

      // make suree that the list is distinct
      Distinct();
    }
    #endregion

    #region Private Manipulators
    /// <summary>
    /// Join multiple list of words together.
    /// </summary>
    /// <param name="words"></param>
    /// <param name="token"></param>
    private void AddWordsAndAllowDuplicates(Words words, CancellationToken token = default(CancellationToken))
    {
      foreach (var word in words)
      {
        // check if we need to get out.
        token.ThrowIfCancellationRequested();

        // add this word to the list
        // we should have no nulls from the calling function
        // we use the base function as we allow duplicates.
        base.Add(word);
      }
    }

    /// <summary>
    /// Join multiple list of words together.
    /// </summary>
    /// <param name="words"></param>
    /// <param name="token"></param>
    private void Add(Words[] words, CancellationToken token = default(CancellationToken))
    {
      var sum = words?.Where( w=> w != null ).Sum(w => w.Count) ?? 0;
      if (sum == 0 || words == null )
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
        AddWordsAndAllowDuplicates(w, token);
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
      if (distinct.Length == Count)
      {
        // nothing changed ... it is already distinct.
        return;
      }
      Clear();
      Capacity = distinct.Length;
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
  }
}
