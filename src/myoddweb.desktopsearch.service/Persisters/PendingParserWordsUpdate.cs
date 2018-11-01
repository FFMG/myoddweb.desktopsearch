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
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal class PendingParserWordsUpdate : IPendingParserWordsUpdate
  {
    /// <inheritdoc />
    public long Id { get; }

    /// <inheritdoc />
    public long FileId { get; }

    /// <inheritdoc />
    public IWord Word { get; }

    public PendingParserWordsUpdate(long id, long fileId, string word) :
      this( id, fileId, new helper.IO.Word(word))
    {
    }

    public PendingParserWordsUpdate( long id, long fileId, IWord word)
    {
      // set the id
      Id = id;
      if (id < 0)
      {
        throw new ArgumentException("The parsed word id cannot be -ve!");
      }

      // set the file id.
      FileId = fileId;
      if (fileId < 0)
      {
        throw new ArgumentException( "The file id cannot be -ve!");
      }

      // save the word.
      Word = word ?? throw new ArgumentNullException( nameof(word));
    }
  }
}
