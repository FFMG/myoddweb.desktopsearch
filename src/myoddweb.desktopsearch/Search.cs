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
using System.Windows.Forms;
using myoddweb.desktopsearch.helper.Models;
using myoddweb.desktopsearch.Helpers;
using myoddweb.desktopsearch.interfaces.Models;
using Newtonsoft.Json;

namespace myoddweb.desktopsearch
{
  public partial class Search : Form
  {
    private const int ColumnName = 0;
    private const int ColumnFullName = 1;
    private const int ColumnActual = 2;

    #region Member variable
    /// <summary>
    /// The timer so we do not call the api while the user is typing.
    /// </summary>
    private readonly Timer _timer;

    /// <summary>
    /// The path of the url
    /// </summary>
    private readonly string _url;

    /// <summary>
    /// The minimum number of characters before we do a seatch
    /// </summary>
    private readonly int _minimumSearchLenght;
    #endregion

    public Search( string url, int port, int minimumSearchLenght )
    {
      // rebuild the search url
      _url = $"{url}:{port}/Search";

      // the minimum size of the characters.
      _minimumSearchLenght = minimumSearchLenght;

      // create everything
      InitializeComponent();

      // initialize te list control.
      InitializeListControl();

      // and size it all the first time.
      OnResize();

      //  give the search box focus
      ActiveControl = searchBox;

      // create start the timer, make sure that it is not running.
      _timer = new Timer
      {
        Interval = 450,
        Enabled = false
      };
      _timer.Tick += OnTimer;
    }

    private void InitializeListControl()
    {
      searchList.FullRowSelect = true;
      searchList.Columns.Add("Name", -2, HorizontalAlignment.Left);
      searchList.Columns.Add("Path", -2, HorizontalAlignment.Left);
      searchList.Columns.Add("Actual", -2, HorizontalAlignment.Left);
    }

    private void OnSizeChanged(object sender, EventArgs e)
    {
      OnResize();
    }

    /// <summary>
    /// Called when we want to resize the current text box and list view
    /// </summary>
    private void OnResize()
    {
      var left = Margin.Left;
      var width = ClientSize.Width - left - Margin.Right;
      var height = ClientSize.Height - searchList.Top - Margin.Bottom;
      searchBox.Left = left;
      searchList.Left = left;
      searchBox.Width = width;
      searchList.Width = width;
      searchList.Height = height;

      searchList.Columns[ColumnName].Width = (int) (width * .25);
      searchList.Columns[ColumnFullName].Width = (int)(width * .60) - Margin.Left;
      searchList.Columns[ColumnActual].Width = (int)(width * .15); 
    }

    /// <summary>
    /// When the text is updated, (and long enough), we will search
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnTextChanged(object sender, EventArgs e)
    {
      // clear the current content
      SetSearchResponse(null);

      // restart the timer
      _timer.Stop();
      _timer.Start();
    }

    /// <summary>
    /// Output the list view
    /// </summary>
    /// <param name="searchResponse"></param>
    private void SetSearchResponse(ISearchResponse searchResponse)
    {
      searchList.Update(searchResponse?.Words);
    }

    /// <summary>
    /// Called when the timer is elapsed.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnTimer(object sender, EventArgs e)
    {
      // stop the timer
      _timer.Stop();

      // search for the current text.
      var text = searchBox.Text;
      if (text.Length < _minimumSearchLenght)
      {
        return;
      }

      try
      {
        //  build the request
        var request = new SearchRequest(text, 100);
        var content = JsonConvert.SerializeObject(request);

        // query the service
        var response = Http.PostAsync(_url, content).GetAwaiter().GetResult();

        //  output the result.
        var searchResponse = JsonConvert.DeserializeObject<SearchResponse>(response);
        SetSearchResponse(searchResponse);
      }
      catch
      {
        // to do.
      }
    }
  }
}
