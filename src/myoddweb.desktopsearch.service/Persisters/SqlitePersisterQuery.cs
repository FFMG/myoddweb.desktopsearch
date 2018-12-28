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
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.helper.Models;
using myoddweb.desktopsearch.interfaces.Models;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal class SqlitePersisterQuery : IQuery
  {
    /// <summary>
    /// The maximum number of characters per query
    /// </summary>
    private readonly int _maxNumCharactersPerParts;

    public SqlitePersisterQuery(int maxNumCharactersPerParts)
    {
      _maxNumCharactersPerParts = maxNumCharactersPerParts;
    }

    public async Task<IList<IWord>> FindAsync(string what, int count, IConnectionFactory factory,  CancellationToken token)
    {
      var sql = $@"
      select 
	      w.word as word,
	      f.name as name,
	      fo.path as path 
      from 
	      {Tables.PartsSearch} ps, 
	      {Tables.WordsParts} wp, 
	      {Tables.Words} w,
	      {Tables.FilesWords} fw, 
	      {Tables.Files} f, 
	      {Tables.Folders} fo
      Where ps.part MATCH @search
        AND wp.partid = ps.id
        AND w.id = wp.wordid
        AND fw.wordid = w.id
        AND f.id = fw.fileid
        AND fo.id = f.folderid
      LIMIT {count};";

      var words = new List<IWord>(count);
      using (var cmd = factory.CreateCommand(sql))
      {
        var pSearch = cmd.CreateParameter();
        pSearch.DbType = DbType.String;
        pSearch.ParameterName = "@search";
        cmd.Parameters.Add(pSearch);
        pSearch.Value = $"^{what}*";
        using (var reader = await factory.ExecuteReadAsync(cmd, token).ConfigureAwait(false))
        {
          var wordPos = reader.GetOrdinal("word");
          var namePos = reader.GetOrdinal("name");
          var pathPos = reader.GetOrdinal("path");

          while (reader.Read())
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // add this update
            var word = (string)reader[wordPos];
            var name = (string)reader[namePos];
            var path = (string)reader[pathPos];
            words.Add(new Word
            {
              FullName = System.IO.Path.Combine(path, name),
              Directory = path,
              Name = name,
              Actual = word
            });
          }
        }
      }

      return words;
    }

  }
}
