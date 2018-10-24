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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using myoddweb.desktopsearch.interfaces.IO;

namespace myoddweb.desktopsearch.helper.IO
{
  public class Parts : IParts
  {
    #region Member variables
    /// <summary>
    /// The actual data
    /// </summary>
    private string[] _data;

    /// <summary>
    /// The word we are working with.
    /// </summary>
    private readonly string _word;

    /// <summary>
    /// The maximum length of the part.
    /// </summary>
    private readonly int _maxPartLength;

    /// <summary>
    /// The length of the word.
    /// </summary>
    private readonly int _wordLength;

    /// <summary>
    /// Our current position
    /// </summary>
    private int _position;
    #endregion

    #region Properties
    /// <inheritdoc />
    public string Current
    {
      get
      {
        Prepare();
        return _data[_position];
      }
    }

    /// <inheritdoc />
    object IEnumerator.Current => Current;

    /// <inheritdoc/>
    public int Count
    {
      get
      {
        Prepare();
        return _data.Length;
      }
    }
    #endregion

    /// <inheritdoc/>
    public bool Any()
    {
      return _wordLength > 0;
    }

    public Parts(string word) : this(word, -1 )
    {
    }

    public Parts(string word, int maxPartLength )
    {
      // save the word
      _word = word ?? throw new ArgumentNullException(nameof(word));

      // the max length
      _maxPartLength = maxPartLength;

      // sanity check
      if (_maxPartLength == 0)
      {
        throw new ArgumentException( "The max part size cannot be zero." );
      }
      if (_maxPartLength < 0 && _maxPartLength != -1 )
      {
        throw new ArgumentException("The max part size cannot be -ve");
      }

      // set the length of the word.
      _wordLength = _word.Length;

      // start at the begining.
      _position = -1;
    }

    /// <summary>
    /// Prepare our data for usage.
    /// </summary>
    private void Prepare()
    {
      // are we done?
      if (_data != null)
      {
        return;
      }

      // in our array, get the actuall position.
      var actualPosition = 0;

      // get the max posible array length
      var maxLength = _wordLength * (_wordLength + 1) / 2;

      // then if we have a max word length, then we can drop the value further
      if (_maxPartLength > 0)
      {
        var excludedLength = _wordLength - _maxPartLength;
        maxLength -= excludedLength * (excludedLength + 1) / 2;
      }

      var data = new string[maxLength];

      for (var start = 0; start < _wordLength; ++start)
      {
        for( var length = 1; length  <= _wordLength - start; length++ )
        {
          // check if we have reached the max length allowed. 
          if (_maxPartLength != -1 && length > _maxPartLength)
          {
            break;
          }
          data[actualPosition++] = _word.Substring(start, length );
        }
      }
      _data = data.Where( d => d != null ).Distinct().ToArray();
    }

    /// <inheritdoc/>
    public bool MoveNext()
    {
      Prepare();
      _position++;
      return _position < Count;
    }

    /// <inheritdoc/>
    public void Reset()
    {
      _position = 0;
    }

    /// <inheritdoc/>
    IEnumerator<string> IEnumerable<string>.GetEnumerator()
    {
      return this;
    }

    /// <inheritdoc/>
    public IEnumerator GetEnumerator()
    {
      return this;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
      _position = -1;
      _data = null;
    }
  }
}
