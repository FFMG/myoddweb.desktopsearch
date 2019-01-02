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
using System.Windows.Forms;
using myoddweb.desktopsearch.helper.Models;
using Newtonsoft.Json;

namespace myoddweb.desktopsearch
{
  public partial class Search : Form
  {
    private readonly string _url;

    public Search( string url, int port)
    {
      _url = $"{url}:{port}/Search";

      // create everything
      InitializeComponent();

      // and resize it all.
      OnResize();

      var request = new SearchRequest("Hello", 10);
      var content = JsonConvert.SerializeObject(request);
      var response = Http.PostAsync(_url, content).GetAwaiter().GetResult();
      var searchResponse = JsonConvert.DeserializeObject<SearchResponse>(response);
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
      var right = ClientSize.Width - left - Margin.Right;
      var height = ClientSize.Height - searchList.Top - Margin.Bottom;
      searchBox.Left = left;
      searchList.Left = left;
      searchBox.Width = right;
      searchList.Width = right;
      searchList.Height = height;
    }
  }
}
