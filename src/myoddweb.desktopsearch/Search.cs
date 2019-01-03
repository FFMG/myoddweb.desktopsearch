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
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using myoddweb.desktopsearch.Config;
using myoddweb.desktopsearch.helper.Models;
using myoddweb.desktopsearch.Helpers;
using myoddweb.desktopsearch.interfaces.Models;
using myoddweb.desktopsearch.Interfaces;
using Newtonsoft.Json;

namespace myoddweb.desktopsearch
{
  internal partial class Search : Form
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
    /// All the arguments.
    /// </summary>
    private readonly IConfig _config;
    #endregion

    public Search(IConfig config )
    {
      // set the arguments.
      _config = config;

      // rebuild the search url
      _url = BuildSearchUrl();

      // create everything
      InitializeComponent();

      // initialize te list control.
      InitializeListControl();

      // set the form position and so on
      InitializeForm();

      // clear the status bar result
      ResetStatusBar();

      // and size it all the first time.
      RecalculateSize();

      //  give the search box focus
      ActiveControl = searchBox;

      // create start the timer, make sure that it is not running.
      _timer = new Timer
      {
        Interval = config.KeyDownIntervalMs,
        Enabled = false
      };
      _timer.Tick += OnTimer;
    }

    /// <summary>
    /// Create the path to the search method.
    /// </summary>
    /// <returns></returns>
    private string BuildSearchUrl()
    {
      var url = _config.Url;
      url = url.TrimEnd('/', '\\');

      var port = _config.Port;
      return $"{url}:{port}/Search";
    }

    /// <summary>
    /// Restore the form position size.
    /// </summary>
    private void InitializeForm()
    {
      if (!File.Exists(_config.Save))
      {
        return;
      }
      var json = File.ReadAllText(_config.Save);
      var save = JsonConvert.DeserializeObject<Save>(json);
      if (save.Width == -1 || save.Height == -1)
      {
        return;
      }

      Size = new Size(save.Width, save.Height);
      Location = save.Location;
      WindowState = save.FullScreen ? FormWindowState.Maximized : FormWindowState.Normal;
    }

    /// <summary>
    /// Create the list view control.
    /// </summary>
    private void InitializeListControl()
    {
      searchList.FullRowSelect = true;
      searchList.Columns.Add("Name", -2, HorizontalAlignment.Left);
      searchList.Columns.Add("Path", -2, HorizontalAlignment.Left);
      searchList.Columns.Add("Actual", -2, HorizontalAlignment.Left);
    }

    /// <summary>
    /// Remove all the values in the status bar.
    /// </summary>
    private void ResetStatusBar()
    {
      searchStatusBar.Text = _url;
    }

    /// <summary>
    /// Called when we want to resize the current text box and list view
    /// </summary>
    private void RecalculateSize()
    {
      var left = Margin.Left;
      var width = ClientSize.Width - left - Margin.Right;
      var height = ClientSize.Height - searchList.Top - searchStatusBar.Height - Margin.Bottom;
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
    /// Output the list view
    /// </summary>
    /// <param name="searchResponse"></param>
    private void SetSearchResponse(ISearchResponse searchResponse)
    {
      if( searchResponse == null )
      {
        ResetStatusBar();
      }
      else
      {
        var status = searchResponse.Status;
        var percent = (((status.Files - status.PendingUpdates) / (double)status.Files) * 100.0);
        searchStatusBar.Text = $@"{searchResponse.Words.Count} Items - {percent:F4}% Complete (Time Elapsed: {TimeSpan.FromMilliseconds(searchResponse.ElapsedMilliseconds):g})";
      }

      searchList.Update(searchResponse?.Words);
    }

    #region Events
    /// <summary>
    /// Event when the form is resized.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnSizeChanged(object sender, EventArgs e)
    {
      RecalculateSize();
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
      if (text.Length < _config.MinimumSearchLength)
      {
        return;
      }

      try
      {
        //  build the request
        var request = new SearchRequest(text, _config.MaxNumberOfItemsToFetch);
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

    /// <summary>
    /// Save the screen size/potions when we close.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnClosing(object sender, FormClosingEventArgs e)
    {
      try
      {
        var full = (WindowState == FormWindowState.Maximized);
        var save = new Save
        {
          Width = full ? RestoreBounds.Size.Width : Size.Width,
          Height = full ? RestoreBounds.Size.Height : Size.Height,
          FullScreen = full,
          Location = full ? RestoreBounds.Location : Location
        };
        var json = JsonConvert.SerializeObject(save, Formatting.Indented);
        File.WriteAllText(_config.Save, json);
      }
      catch
      {
        //  to do...
      }
    }
    #endregion
  }
}
