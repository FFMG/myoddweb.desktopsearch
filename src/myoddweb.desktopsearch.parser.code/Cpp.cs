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
  public class Cpp : IFileParser
  {
    public string Name => "CppParser";

    public string[] Extenstions => new[] {"cpp", "c", "cc", "c++", "cxx", "h", "hh", "hpp", "hxx", "h++" };

    /// <summary>
    /// The file parser
    /// </summary>
    private readonly FileParser _parser;

    /// <summary>
    /// List of reserverd keywords
    /// </summary>
    private readonly List<string> _keyWords = new List<string>
    {
      "asm","else", "define", "DEFINE", "new","this","auto","enum","operator","throw","bool","explicit","private",
      "true","break","export","protected","try","case","extern","public","typedef","catch",
      "false","register","typeid","char","float","reinterpret_cast","typename","class","for",
      "return","union","const","friend","short","unsigned","const_cast","goto","signed","using",
      "continue","if","sizeof","virtual","default","inline","static","void","delete","int",
      "static_cast","volatile","do","long","struct","wchar_t","double","mutable","switch","while",
      "dynamic_cast","namespace","template"
    };

    public Cpp()
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
      return await _parser.ParserAsync(helper, Contains, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Remove words that are too common for the CSharp language
    /// </summary>
    /// <param name="word"></param>
    /// <returns></returns>
    private bool Contains(string word)
    {
      // return false if we cannot use that keyword.
      return !_keyWords.Any(s => s.Equals(word, StringComparison.OrdinalIgnoreCase));
    }
  }
}
