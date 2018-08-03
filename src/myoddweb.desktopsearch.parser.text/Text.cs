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
    public async Task<HashSet<IWord>> ParseAsync(FileInfo file, ILogger logger, CancellationToken token)
    {
      var textWord = new HashSet<IWord>( new TextWordComparer() );
      try
      {
        using (var sr = new StreamReader(file.FullName))
        {
          string line;
          while ((line = await sr.ReadLineAsync().ConfigureAwait(false)) != null)
          {
            // get out if we cancelled.
            token.ThrowIfCancellationRequested();

            // split the line into words.
            var words = _reg.Matches(line).OfType<Match>().Select(m => new TextWord( m.Groups[0].Value ));
            textWord.UnionWith(words);
          }
        }
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
      return textWord;
    }
  }
}
