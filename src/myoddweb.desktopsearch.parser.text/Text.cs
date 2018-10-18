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
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.helper.Components;
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
    /// The file parser
    /// </summary>
    private readonly FileParser _parser;

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
      const int maxNumberReadCharacters = 5000000; // (char = 2bytes * 5000000 = ~10MB)
      _parser = new FileParser(@"[^\p{Z}\t\r\n\v\f\p{P}\p{C}\p{S}]+", maxNumberReadCharacters);
    }

    /// <inheritdoc />
    public bool Supported(FileSystemInfo file)
    {
      //  if this is a valid extension ... say yes.
      return helper.File.IsExtension(file, Extenstions);
    }

    /// <inheritdoc />
    public async Task<long> ParseAsync(IPrarserHelper helper, ILogger logger, CancellationToken token)
    {
      try
      {
        return await _parser.ParserAsync(helper, (s) => true, token ).ConfigureAwait( false );
      }
      catch (OperationCanceledException )
      {
        logger.Warning( $"Received cancellation request - {Name}");
        throw;
      }
      catch (IOException)
      {
        logger.Error($"IO error trying to read the file, {helper.File.FullName}, might be locked/protected ({Name}).");
        return 0;
      }
      catch (OutOfMemoryException)
      {
        logger.Error($"Out of Memory: reading file, {helper.File.FullName} ({Name})");
        return 0;
      }
      catch (Exception ex )
      {
        logger.Exception(ex);
        return 0;
      }
    }
  }
}
