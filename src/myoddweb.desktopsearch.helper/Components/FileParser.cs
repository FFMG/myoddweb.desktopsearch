﻿//This file is part of Myoddweb.DesktopSearch.
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

    /// <summary>
    /// The maximum number of characters we want to read.
    /// We _might_ read more or less characters depending on the file size.
    /// </summary>
    private readonly int _maxNumberReadCharacters;

    /// <inheritdoc />
    /// <summary>
    /// Contructor with a string pattern.
    /// </summary>
    /// <param name="pattern"></param>
    /// <param name="maxNumberReadCharacters"></param>
    public FileParser(string pattern, int maxNumberReadCharacters) : this(new Regex(pattern), maxNumberReadCharacters )
    {
    }

    /// <summary>
    /// The constructor with a regex.
    /// </summary>
    /// <param name="regex"></param>
    /// <param name="maxNumberReadCharacters"></param>
    public FileParser(Regex regex, int maxNumberReadCharacters)
    {
      _reg = regex;
      _maxNumberReadCharacters = maxNumberReadCharacters;
    }

    /// <summary>
    /// Parse a given file.
    /// </summary>
    /// <param name="helper"></param>
    /// <param name="token"></param>
    /// <param name="func">The function that tells us if we can add a word or not.</param>
    /// <returns></returns>
    public async Task<long> ParserAsync(IParserHelper helper, Func<string, bool> func, CancellationToken token )
    {
      using (var sr = new StreamReader(helper.File.FullName))
      {
        return await ParserAsync(helper, sr, func, token);
      }
    }

    /// <summary>
    /// Parse a text reader one word at a time.
    /// </summary>
    /// <param name="helper"></param>
    /// <param name="text"></param>
    /// <param name="func"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<long> ParserAsync(IParserHelper helper, string text, Func<string, bool> func, CancellationToken token)
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
    public async Task<long> ParserAsync(IParserHelper helper, TextReader sr, Func<string, bool> func, CancellationToken token)
    {
      // the number of words we added.
      long added = 0;

      // the word
      string text;
      while ((text = await ReadTextAsync(sr, token).ConfigureAwait(false)) != null)
      {
        added += await ParserAsync(helper, text, func, token).ConfigureAwait(false);
      }

      // did we find anything?
      return added;
    }

    /// <summary>
    /// Read 
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<string> ReadTextAsync(TextReader stream, CancellationToken token)
    {
      // try and read up to the max
      var buffer = new char[_maxNumberReadCharacters];
      var len = await stream.ReadAsync(buffer, 0, _maxNumberReadCharacters).ConfigureAwait(false);
      if (len == 0)
      {
        return null;
      }
      if (len < _maxNumberReadCharacters )
      {
        return new string(buffer);
      }

      // we have to read to the next char.
      var text = new string(buffer);

      // recreate the buffer
      const int count = 1;
      buffer = new char[1];

      while (true)
      {
        // try and read the stream.
        var read = await stream.ReadAsync(buffer, 0, count).ConfigureAwait(false);

        // anything else to read?
        // if not return what we have, (we might have nothing)
        if (read == 0)
        {
          return text;
        }

        // get out if we cancelled.
        token.ThrowIfCancellationRequested();

        var letter = new string(buffer, 0, read);
        if (!_reg.IsMatch(letter))
        {
          // if we are here we found a 'bad' character straight away.
          return text;
        }
        text += letter;
      }
    }
  }
}
