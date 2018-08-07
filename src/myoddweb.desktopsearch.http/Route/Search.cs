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
using System.Data;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.http.Models;
using myoddweb.desktopsearch.interfaces.Persisters;
using Newtonsoft.Json;

namespace myoddweb.desktopsearch.http.Route
{
  internal class Search : Route
  {
    /// <summary>
    /// This is the search route
    /// The method is 'search' while the value is the query. 
    /// </summary>
    public Search(IPersister persister, interfaces.Logging.ILogger logger) : base(new[] { "Search" }, Method.Post, persister, logger )
    {
    }

    /// <inheritdoc />
    protected override RouteResponse OnProcess(string raw, Dictionary<string, string> parameters, HttpListenerRequest request)
    {
      try
      {
        // get the search query.
        var search = JsonConvert.DeserializeObject<SearchRequest>(raw);

        // build the response packet.
        var response = BuildResponse(search).Result;

        return new RouteResponse
        {
          Response = JsonConvert.SerializeObject(response),
          StatusCode = HttpStatusCode.OK,
          ContentType = "application/json"
        };
      }
      catch (Exception e)
      {
        return new RouteResponse
        {
          Response = e.Message,
          StatusCode = HttpStatusCode.InternalServerError
        };
      }
    }

    /// <summary>
    /// Given the search request, build the response.
    /// </summary>
    /// <param name="search"></param>
    /// <returns></returns>
    private async Task<SearchResponse> BuildResponse(SearchRequest search)
    {
      // we want the stopwatch to include the getting of the transaction as well.
      var stopwatch = new Stopwatch();
      stopwatch.Start();

      var token = CancellationToken.None;
      var transaction = await Persister.Begin(token).ConfigureAwait(false);
      try
      {
        var sql = $@"select 
	                w.word as word,
	                f.name as name,
	                fo.path as path
                from words w, FilesWords fw, Files f, Folders fo where 
	                w.word like printf( ""%s%s"",@search,""%"" )
                  AND fw.wordid = w.id
	                AND f.id = fw.fileid
	                AND fo.id = f.folderid
                  LIMIT {search.Count} 
                ";

        var words = new List<Word>();
        using (var cmd = Persister.CreateDbCommand(sql, transaction))
        {
          var pSearch = cmd.CreateParameter();
          pSearch.DbType = DbType.String;
          pSearch.ParameterName = "@search";
          cmd.Parameters.Add(pSearch);
          pSearch.Value = search.What;
          var reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
          while (reader.Read())
          {
            // get out if needed.
            token.ThrowIfCancellationRequested();

            // add this update
            var word = (string)reader["word"];
            var name = (string)reader["name"];
            var path = (string)reader["path"];
            words.Add(new Word { FullName = System.IO.Path.Combine(path, name), Directory = path, Name = name, Actual = word });
          }
        }

        // we are done here.
        Persister.Commit(transaction);

        // log it.
        stopwatch.Stop();
        Logger.Information( $"Completed search for '{search.What}' found {words.Count} result(s) (Time Elapsed: {stopwatch.Elapsed:g})");

        // we can now build the response model.
        return new SearchResponse
        {
          Words = words,
          ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
        };
      }
      catch (Exception e)
      {
        Persister.Rollback(transaction);
        Logger.Exception(e);
      }

      // return nothing
      return new SearchResponse();
    }
  }
}