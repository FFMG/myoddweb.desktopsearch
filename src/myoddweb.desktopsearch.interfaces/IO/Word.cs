using System;
using System.Collections.Generic;

namespace myoddweb.desktopsearch.interfaces.IO
{
  public class Word
  {
    /// <summary>
    /// The word value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// All the parts of the word.
    /// </summary>
    private HashSet<string> _parts;

    /// <summary>
    /// Get all the parts of our word.
    /// </summary>
    public HashSet<string> Parts
    {
      get
      {
        if (_parts != null)
        {
          return _parts;
        }

        _parts = new HashSet<string>();
        return _parts;
      }
    }
    
    /// <summary>
    /// The word we are adding.
    /// </summary>
    /// <param name="value"></param>
    public Word(string value)
    {
      // the name cannot be null
      Value = Value ?? throw new ArgumentNullException(nameof(value));
    }
  }
}
