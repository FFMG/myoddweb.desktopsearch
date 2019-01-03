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
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.Helpers
{
  internal class Http
  {
    private Http()
    {
    }

    public static Task<string> PostAsync(string uri, string content)
    {
      return AnyAsync( uri, content, "POST" );
    }

    public static async Task<string> AnyAsync(string uri, string content, string method )
    { 
      var request = (HttpWebRequest)WebRequest.Create(uri);
      request.Method = method;
      if (method == "POST")
      {
        // Convert POST data to a byte array.
        var byteArray = Encoding.UTF8.GetBytes(content);
        // Set the ContentType property of the WebRequest.
        request.ContentType = "application/x-www-form-urlencoded";
        // Set the ContentLength property of the WebRequest.
        request.ContentLength = byteArray.Length;
        // Get the request stream.
        var dataStreamRequest = request.GetRequestStream();
        // Write the data to the request stream.
        dataStreamRequest.Write(byteArray, 0, byteArray.Length);
        // Close the Stream object.
        dataStreamRequest.Close();
      }
      var response = await request.GetResponseAsync().ConfigureAwait(false);

      Stream dataStream = null;
      StreamReader reader = null;
      string responseFromServer = null;

      try
      {
        // Get the stream containing content returned by the server.
        dataStream = response.GetResponseStream();
        // Open the stream using a StreamReader for easy access.
        reader = new StreamReader(dataStream ?? throw new InvalidOperationException());
        // Read the content.
        responseFromServer = reader.ReadToEnd();
        // Cleanup the streams and the response.
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.Message);
      }
      finally
      {
        reader?.Close();
        dataStream?.Close();
        response.Close();
      }
      return responseFromServer;
    }
  }
}
