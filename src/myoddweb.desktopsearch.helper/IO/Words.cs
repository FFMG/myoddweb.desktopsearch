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
using System.Linq;
using System.Threading;
using myoddweb.desktopsearch.interfaces.IO;

namespace myoddweb.desktopsearch.helper.IO
{
  public class Words : HashSet<IWord>, IWords
  {
    #region Contructors
    /// <inheritdoc />
    /// <summary>
    /// Base constructor.
    /// </summary>
    public Words() : base( new WordEqualityComparer() )
    {
    }

    /// <inheritdoc />
    /// <summary>
    /// Constructor with a single word.
    /// </summary>
    /// <param name="word"></param>
    public Words(IWord word) : this()
    {
      // just add this word.
      Add(word);
    }

    /// <inheritdoc />
    /// <summary>
    /// Constructor with a list of words.
    /// </summary>
    /// <param name="words"></param>
    public Words(IWords[] words) : this()
    {
      // Add all he words into one.
      Add(words);
    }

    public Words(IWord[] words) : this()
    {
      // Add all he words into one.
      Add(words);
    }

    public Words(IReadOnlyCollection<string> words, int maxNumCharactersPerParts) : this()
    {
      // Add all he words into one.
      Add(words, maxNumCharactersPerParts );
    }
    #endregion

    #region Public Manipulator

    public IWord this[int index]
    {
      get
      {
        var i = 0;
        foreach (var t in this)
        {
          if (i == index)
          {
            return t;
          }

          i++;
        }

        throw new IndexOutOfRangeException();
      }
    }

    public bool Any()
    {
      return Count > 0;
    }

    /// <summary>
    /// Add a single word... but not if null.
    /// </summary>
    /// <param name="item"></param>
    public new void Add(IWord item)
    {
      if (null == item)
      {
        return;
      }
      base.Add(item);
    }

    /// <summary>
    /// Add a single string word to our list.
    /// </summary>
    /// <param name="word"></param>
    /// <param name="maxNumCharactersPerParts"></param>
    public void Add(string word, int maxNumCharactersPerParts)
    {
      if (null == word)
      {
        return;
      }
      Add(new Word(word, maxNumCharactersPerParts));
    }

    /// <summary>
    /// Join multiple list of words together.
    /// </summary>
    /// <param name="words"></param>
    /// <param name="token"></param>
    public void Add(IWord[] words, CancellationToken token = default(CancellationToken))
    {
      var sum = words?.Length ?? Count;
      if (sum == 0 || words == null)
      {
        return;
      }

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
    }

    /// <summary>
    /// Join multiple list of words together.
    /// </summary>
    /// <param name="words"></param>
    /// <param name="maxNumCharactersPerParts"></param>
    /// <param name="token"></param>
    public void Add(IReadOnlyCollection<string> words, int maxNumCharactersPerParts, CancellationToken token = default(CancellationToken))
    {
      var sum = words?.Count ?? 0;
      if (sum == 0 || words == null)
      {
        return;
      }

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
        base.Add(new Word(w, maxNumCharactersPerParts));
      }
    }
    #endregion

    #region Private Manipulators
    /// <summary>
    /// Join multiple list of words together.
    /// </summary>
    /// <param name="words"></param>
    /// <param name="token"></param>
    private void Add(IWords[] words, CancellationToken token = default(CancellationToken))
    {
      var sum = words?.Where( w=> w != null ).Sum(w => w.Count) ?? 0;
      if (sum == 0 || words == null )
      {
        return;
      }

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
        UnionWith( w );
      }
    }
    #endregion
  }
}
