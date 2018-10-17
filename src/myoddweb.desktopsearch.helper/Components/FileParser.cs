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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.IO;

namespace myoddweb.desktopsearch.helper.Components
{
  public class FileParser
  {
    /// <summary>
    /// The regex we will be using over and over.
    /// </summary>
    private readonly Regex _reg;

    /// <inheritdoc />
    /// <summary>
    /// Contructor with a string pattern.
    /// </summary>
    /// <param name="pattern"></param>
    public FileParser(string pattern ) : this(new Regex(pattern) )
    {
    }

    /// <summary>
    /// The constructor with a regex.
    /// </summary>
    /// <param name="regex"></param>
    public FileParser(Regex regex )
    {
      _reg = regex;
    }

    /// <summary>
    /// Parse a given file.
    /// </summary>
    /// <param name="helper"></param>
    /// <param name="maxFileLength"></param>
    /// <param name="token"></param>
    /// <param name="func">The function that tells us if we can add a word or not.</param>
    /// <returns></returns>
    public async Task<bool> ParserAsync(IPrarserHelper helper, long maxFileLength, Func<string, bool> func, CancellationToken token )
    {
      string text;
      using (var sr = new StreamReader(helper.File.FullName))
      {
        // the file is too big for us to read at once
        // so we have to parse word by word.
        if ((sr.BaseStream as FileStream)?.Length > maxFileLength)
        {
          return await ParserAsync(helper, sr, func, token);
        }

        // read everything in one go.
        // then close the file and build the list of words.
        text = sr.ReadToEnd();
      }
      return await ParserAsync(helper, text, func, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Parse a text reader one word at a time.
    /// </summary>
    /// <param name="helper"></param>
    /// <param name="text"></param>
    /// <param name="func"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<bool> ParserAsync(IPrarserHelper helper, string text, Func<string, bool> func, CancellationToken token)
    {
      // split the line into words.
      var words = _reg.Matches(text).OfType<Match>().Select(m => m.Groups[0].Value).ToArray();
      return await helper.AddWordAsync(words.Where(func).ToList(), token).ConfigureAwait(false);
    }

    /// <summary>
    /// Parse a text reader one word at a time.
    /// </summary>
    /// <param name="helper"></param>
    /// <param name="sr"></param>
    /// <param name="func"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<bool> ParserAsync(IPrarserHelper helper, TextReader sr, Func<string, bool> func, CancellationToken token)
    {
      // did we find a word?
      var added = false;

      // the word
      string word;
      while ((word = await ReadWordAsync(sr, token).ConfigureAwait(false)) != null)
      {
        if( !func(word))
        {
          continue;
        }
        if (await helper.AddWordAsync( new []{word}, token).ConfigureAwait(false))
        {
          added = true;
        }
      }

      // did we find anything?
      return added;
    }

    /// <summary>
    /// Read a word from the file and move the pointer forward.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<string> ReadWordAsync(TextReader stream, CancellationToken token)
    {
      string word = null;
      const int count = 1;
      var buffer = new char[count];

      while (true)
      {
        // try and read the stream.
        var read = await stream.ReadAsync(buffer, 0, count).ConfigureAwait(false);

        // anything else to read?
        // if not return what we have, (we might have nothing)
        if (read == 0)
        {
          return word;
        }

        // get out if we cancelled.
        token.ThrowIfCancellationRequested();

        var letter = new string(buffer, 0, read);
        if (!_reg.IsMatch(letter))
        {
          // do we have anything?
          if (!string.IsNullOrEmpty(word))
          {
            return word;
          }

          // if we are here we found a 'bad' character first.
          continue;
        }

        if (word == null)
        {
          word = letter;
          continue;
        }
        word += letter;
      }
    }
  }
}
