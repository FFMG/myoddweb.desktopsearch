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
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.http
{
  public class HttpServer
  {
    /// <summary>
    /// The Http listner
    /// </summary>
    private readonly HttpListener _listener;

    /// <summary>
    /// The currently running thread ... if it is runnnin
    /// </summary>
    private Task _thread;

    /// <summary>
    /// The cancellation token.
    /// </summary>
    private CancellationToken _token;

    /// <summary>
    /// Are we running or not?
    /// </summary>
    private bool _running;

    /// <summary>
    /// All the paths we will be looking for.
    /// </summary>
    private List<Route.Route> _routes;

    /// <summary>
    /// The handle we need to use to de-register our cancellation function.
    /// </summary>
    private CancellationTokenRegistration _cancelationToken;

    public HttpServer()
    {
      if (!HttpListener.IsSupported)
      {
        throw new NotSupportedException("The Http Listener is not supported.");
      }
      _listener = new HttpListener();
    }

    public void Start(CancellationToken token)
    {
      _token = token;
      _cancelationToken = _token.Register(Cancel);
      _running = true;

      _routes = new List<Route.Route> { 
        new Route.Search()
      };

      _listener.Prefixes.Add("http://*:9123/");
      _listener.Start();

      _thread = Task.Factory.StartNew( 
        async () => await ListenAsync().ConfigureAwait(false),
        _token 
        );
    }

    public void Stop()
    {
      if (!_running)
      {
        return;
      }

      // free the resources.
      _cancelationToken.Dispose();

      // then we try and stop everything.
      _running = false;
      _listener?.Stop();

      if (null != _thread)
      {
        helper.Wait.UntilAsync( () => _thread.IsCompleted, _token).Wait(_token);
      }

      _thread = null;
    }

    private void Cancel()
    {
      Stop();
    }

    private async Task ListenAsync()
    {
      try
      {
        while (_running)
        {
          var ctx = await _listener.GetContextAsync().ConfigureAwait( false );
          ThreadPool.QueueUserWorkItem(_ =>
          {
            foreach (var route in _routes)
            {
              // check if this is a match or not.
              if (!route.IsMarch(ctx.Request.Url.Segments, ctx.Request.HttpMethod))
              {
                continue;
              }

              var ret = route.Process(ctx.Request);
              var response = ctx.Response;

              response.StatusCode = (int)route.StatusCode;
              response.ContentType = route.ContentType;

              // build the response stream.
              // only start writting to it now as the OS is allowed to send the response
              // as soon as we get something from it.
              var bOutput = System.Text.Encoding.UTF8.GetBytes(ret);
              response.ContentLength64 = bOutput.Length;
              var outputStream = response.OutputStream;
              outputStream.Write(bOutput, 0, bOutput.Length);
              outputStream.Close();

              // we are done now.
              return;
            }

            // if we are here, we never found a match
            ctx.Response.StatusCode = 404;
            ctx.Response.Close();
          });
        }
      }
      catch (OperationCanceledException)
      {
        throw;
      }
      catch (HttpListenerException)
      {
      }
    }
  }
}
