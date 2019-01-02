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
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.helper.Performance;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Logging;

namespace myoddweb.desktopsearch.processor.Processors
{
  internal class Parser : IProcessor
  {
    #region Member variables
    private readonly ICounter _counter;
    private readonly IParser _parser;
    private readonly ILogger _logger;
    #endregion

    public Parser(ICounter counter, IParser parser, ILogger logger)
    {
      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      // save the files/folders parser
      _parser = parser ?? throw new ArgumentNullException(nameof(parser));

      // our performance counter.
      _counter = counter ?? throw new ArgumentNullException(nameof(counter));
    }

    /// <inheritdoc />
    public async Task<long> WorkAsync(CancellationToken token)
    {
      using (_counter.Start())
      {
        await _parser.WorkAsync(token).ConfigureAwait(false);
        _logger.Verbose("Files/Folder parser processor: processed all files/folders");
      }
      return 0;
    }

    /// <inheritdoc />
    public void Stop()
    {
      _counter?.Dispose();
    }
  }
}
