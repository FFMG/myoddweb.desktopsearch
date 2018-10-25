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
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.helper.Components;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Logging;

namespace myoddweb.desktopsearch.parser.code
{
  public class Sql : IFileParser
  {
    public string Name => "SqlParser";

    public string[] Extenstions => new[] {"sql"  };

    /// <summary>
    /// The file parser
    /// </summary>
    private readonly FileParser _parser;

    /// <summary>
    /// List of reserverd keywords
    /// This is NOT an exhaustive list of all the keywords... just the common ones.
    /// </summary>
    private readonly List<string> _keyWords = new List<string>
    {
      "ADD", "ALTER", "AND", "AS", "BIGINT", "CASE", "COALESCE", "COMMIT", "CREATE", "COLUMN", "CONSTRAINT",
      "CURSOR", "DATABASE", "DECLARE", "DELETE", "DROP", "ELSE", "EXECUTE", "EXPLAIN", "FUNCTION", "FROM",
      "GOTO", "GROUP", "HAVING", "IF", "IN", "INDEX", "INNER", "INSERT", "INT", "INTEGER", "INTO", "ISNULL",
      "JOIN", "KEY", "LEFT", "LIKE", "NOT", "NULL", "NULLIF", "OPEN", "OR", "ORDER", "PRINT", "PROCEDURE",
      "RIGHT", "ROLLBACK", "ROWCOUNT", "RETURNS", "SELECT", "SCHEMA", "TABLE", "TRAN", "TRANSACTION", "TRIGGER",
      "TRUNCATE", "TINYINT", "UNION", "UNIQUE", "UPDATE", "VARCHAR", "VIEW", "WHEN", "WHERE", "WITH"
    };

    public Sql()
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
    public async Task<long> ParseAsync(IParserHelper helper, ILogger logger, CancellationToken token)
    {
      return await _parser.ParserAsync(helper, Contains, token).ConfigureAwait(false);
    }
    
    /// <summary>
    /// Check if a word should be removed from the list of words.
    /// </summary>
    /// <param name="word">The word we are comparing.</param>
    /// <returns></returns>
    private bool Contains( string word)
    {
      return !_keyWords.Any(keyWord => string.Equals(keyWord, word, StringComparison.OrdinalIgnoreCase));
    }
  }
}
