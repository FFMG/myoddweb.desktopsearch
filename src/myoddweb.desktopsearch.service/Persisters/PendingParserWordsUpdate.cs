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
using System.Collections.Generic;
using System.Linq;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal class PendingParserWordsUpdate : IPendingParserWordsUpdate
  {
    /// <inheritdoc />
    public IList<long> FileIds { get; }

    /// <inheritdoc />
    public IWord Word { get; }

    /// <inheritdoc />
    public long Id { get; }

    public PendingParserWordsUpdate( long id, IWord word, IList<long> fileIds)
    {
      // set the id
      Id = id;
      FileIds = fileIds ?? throw new ArgumentNullException(nameof(fileIds));
      if (!fileIds.Any())
      {
        throw new ArgumentException("The list of ids/fileid cannot be empty");
      }

      // save the word.
      Word = word ?? throw new ArgumentNullException( nameof(word));
    }
  }
}
