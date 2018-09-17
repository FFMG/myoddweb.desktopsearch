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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Logging;

namespace myoddweb.desktopsearch.parser.text
{
  public class Text : IFileParser
  {
    /// <inheritdoc />
    public string Name => "TextParser";

    /// <inheritdoc />
    public string[] Extenstions => new[] {"txt"};

    /// <summary>
    /// The regex we will be using over and over.
    /// </summary>
    private readonly Regex _reg;

    public Text()
    {
      // @see https://www.regular-expressions.info/unicode.html
      // But we basically split any words by ...
      //   \p{Z}\t\r\n\v\f  - All the blank spaces
      //   \p{P}            - Punctuation
      //   \p{C}            - Invisible characters.
      //   \p{S}            - All the symbols, (currency/maths)
      //
      // So we allow Numbers and Words together.
      _reg = new Regex(@"[^\p{Z}\t\r\n\v\f\p{P}\p{C}\p{S}]+");
    }

    /// <inheritdoc />
    public bool Supported(FileInfo file)
    {
      //  if this is a valid extension ... say yes.
      return helper.File.IsExtension(file, Extenstions);
    }

    /// <inheritdoc />
    public async Task<Words> ParseAsync(FileInfo file, ILogger logger, CancellationToken token)
    {
      try
      {
        var textWord = new List<string>();
        using (var sr = new StreamReader(file.FullName))
        {
          const int maxFileLength = 1000000;
          if ((sr.BaseStream as FileStream)?.Length <= maxFileLength)
          {
            // read everything in one go.
            var text = sr.ReadToEnd();

            // split the line into words.
            var words = _reg.Matches(text).OfType<Match>().Select(m => m.Groups[0].Value).ToArray();
            textWord.AddRange(words);
          }
          else
          {
            string word;
            while ((word = await ReadWordAsync(sr, token).ConfigureAwait(false)) != null)
            {
              textWord.Add(word);
            }
          }
        }
        return new Words(textWord);
      }
      catch (OperationCanceledException )
      {
        logger.Warning("Received cancellation request - Text parser");
        throw;
      }
      catch (IOException)
      {
        logger.Error($"IO error trying to read the file, {file.FullName}, might be locked/protected.");
        return null;
      }
      catch (OutOfMemoryException)
      {
        logger.Error($"Out of Memory: reading file, {file.FullName}");
        return null;
      }
      catch (Exception ex )
      {
        logger.Exception(ex);
        return null;
      }
    }

    private async Task<string> ReadWordAsync(TextReader stream, CancellationToken token )
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
