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
using System.Data;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.helper.Models;
using myoddweb.desktopsearch.interfaces.Logging;
using myoddweb.desktopsearch.interfaces.Models;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service.Persisters
{
  internal class SqlitePersisterQuery : IQuery
  {
    #region
    public IConnectionFactory Factory { get; set; }

    /// <summary>
    /// The maximum number of characters per query
    /// </summary>
    private readonly int _maxNumCharactersPerParts;

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Readonly factory.
    /// </summary>
    private IConnectionFactory _readOnlyFactory;

    /// <summary>
    /// Return readonly factory if we have one ... otherwise, return the factory.
    /// </summary>
    private IConnectionFactory ReadonlyFactory => _readOnlyFactory ?? Factory;
    #endregion

    public SqlitePersisterQuery(int maxNumCharactersPerParts, ILogger logger )
    {
      // the maximum number of characters allowed.
      _maxNumCharactersPerParts = maxNumCharactersPerParts;

      // save the logger.
      _logger = logger ?? throw new ArgumentNullException( nameof(logger) );
    }

    /// <summary>
    /// Make sure that the word we are looking for fits within the limits we have.
    /// </summary>
    /// <param name="what"></param>
    /// <returns></returns>
    private string ResziedWord(string what)
    {
      if (what.Length < _maxNumCharactersPerParts)
      {
        return what;
      }
      return what.Substring(0, _maxNumCharactersPerParts);
    }

    /// <inheritdoc />
    public async Task<IList<IWord>> FindAsync(string what, int count, CancellationToken token)
    {
      Contract.Assert( ReadonlyFactory != null );

      try
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
          AND wp.partid = ps.docid
          AND w.id = wp.wordid
          AND fw.wordid = w.id
          AND f.id = fw.fileid
          AND fo.id = f.folderid
        LIMIT {count};";

        var words = new List<IWord>(count);
        using (var cmd = ReadonlyFactory.CreateCommand(sql))
        {
          var pSearch = cmd.CreateParameter();
          pSearch.DbType = DbType.String;
          pSearch.ParameterName = "@search";
          cmd.Parameters.Add(pSearch);
          pSearch.Value = $"^{ResziedWord(what)}*";
          using (var reader = await ReadonlyFactory.ExecuteReadAsync(cmd, token).ConfigureAwait(false))
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
      catch (OperationCanceledException e)
      {
        _logger.Warning("Received cancellation request durring Query/Find.");
        // is it my token?
        if (e.CancellationToken != token)
        {
          _logger.Exception(e);
        }
        throw;
      }
      catch (Exception e)
      {
        _logger.Exception( e );
        throw;
      }
    }

    /// <inheritdoc />
    public void Prepare(IPersister persister, IConnectionFactory factory)
    {
      // save non readonly factory
      if (Factory == null && !factory.IsReadOnly)
      {
        Factory = factory;
      }

      // save the readonly factory
      if (_readOnlyFactory == null && factory.IsReadOnly)
      {
        _readOnlyFactory = factory;
      }
    }

    /// <inheritdoc />
    public void Complete(IConnectionFactory factory, bool success)
    {
      if (factory == Factory)
      {
        Factory = null;
      }

      if (factory == _readOnlyFactory)
      {
        _readOnlyFactory = null;
      }
    }
  }
}
