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
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.helper.Models;
using myoddweb.desktopsearch.interfaces.Models;
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
    public Search(IPersister persister, interfaces.Logging.ILogger logger) : 
      base(new[] { "Search" }, Method.Post, persister, logger )
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
        var response = BuildResponse(search).GetAwaiter().GetResult();

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

    private Task<IList<IWord>> GetWords(ISearchRequest search, CancellationToken token)
    {
      return Persister.Query.FindAsync(search.What, search.Count, token); 
    }

    private async Task<IStatusResponse> GetStatus( CancellationToken token)
    {
      return new StatusResponse
      {
        PendingUpdates = await Persister.Counts.GetPendingUpdatesCountAsync(token ).ConfigureAwait(false),
        Files = await Persister.Counts.GetFilesCountAsync( token).ConfigureAwait(false)
      };
    }

    /// <summary>
    /// Given the search request, build the response.
    /// </summary>
    /// <param name="search"></param>
    /// <returns></returns>
    private async Task<ISearchResponse> BuildResponse(SearchRequest search)
    {
      // we want the stopwatch to include the getting of the transaction as well.
      var stopwatch = new Stopwatch();
      stopwatch.Start();
      var log = new StringBuilder();
      var token = CancellationToken.None;
      var transaction = await Persister.BeginRead(token).ConfigureAwait(false);

      log.AppendLine( $"  > Got transaction        Time Elapsed: {stopwatch.Elapsed:g}");
      try
      {
        // search the words.
        var words = await GetWords(search, token).ConfigureAwait(false);
        log.AppendLine($"  > Got Words              Time Elapsed: {stopwatch.Elapsed:g}");

        // get the percent complete
        var status = await GetStatus( token).ConfigureAwait(false);
        log.AppendLine($"  > Got Status             Time Elapsed: {stopwatch.Elapsed:g}");

        // we are done here.
        Persister.Commit(transaction);
        log.AppendLine($"  > Committed              Time Elapsed: {stopwatch.Elapsed:g}");

        // log it.
        stopwatch.Stop();
        log.Append($"Completed search for '{search.What}' found {words.Count} result(s) (Time Elapsed: {stopwatch.Elapsed:g})");
        Logger.Information( log.ToString());

        // we can now build the response model.
        return new SearchResponse(words,
          stopwatch.ElapsedMilliseconds,
          status );
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
