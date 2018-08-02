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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.IO;

namespace myoddweb.desktopsearch.parser.text
{
  public class Text : IFileParser
  {
    /// <inheritdoc />
    public string Name => "TextParser";

    /// <inheritdoc />
    public string[] Extenstions => new[] {"txt"};

    /// <inheritdoc />
    public Task<List<string>> ParseAsync(FileInfo file, CancellationToken token)
    {
      var reg = new Regex(@"[^\p{L}]*\p{Z}[^\p{L}]*");
      var current = new List<string>();
      using (var sr = new StreamReader(file.FullName))
      {
        string line;
        while ((line = sr.ReadLine()) != null)
        {
          var x = reg.Split(line.ToLowerInvariant());
          current.AddRange(x);
          current = current.Distinct().ToList();
        }
      }
      return Task.FromResult(current);
    }
  }
}
