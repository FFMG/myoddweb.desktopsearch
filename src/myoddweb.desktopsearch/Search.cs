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
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using myoddweb.desktopsearch.helper.Models;
using myoddweb.desktopsearch.interfaces.Models;
using Newtonsoft.Json;

namespace myoddweb.desktopsearch
{
  public partial class Search : Form
  {
    private const int ColumnName = 0;
    private const int ColumnFullName = 1;
    private const int ColumnActual = 2;

    private readonly string _url;

    public Search( string url, int port)
    {
      _url = $"{url}:{port}/Search";

      // create everything
      InitializeComponent();

      searchList.FullRowSelect = true;
      searchList.Columns.Add("Name", -2, HorizontalAlignment.Left);
      searchList.Columns.Add("Path", -2, HorizontalAlignment.Left);
      searchList.Columns.Add("Actual", -2, HorizontalAlignment.Left);

      // and resize it all.
      OnResize();

      //  give the search box focus
      ActiveControl = searchBox;
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

    private void OnTextChanged(object sender, EventArgs e)
    {
      // clear the current content
      SetSearchResponse(null);
      var text = searchBox.Text;
      if (text.Length < 3)
      {
        return;
      }

      try
      {
        //  build the request
        var request = new SearchRequest( text, 100 );
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
    /// Output the list view
    /// </summary>
    /// <param name="searchResponse"></param>
    private void SetSearchResponse(ISearchResponse searchResponse)
    {
      // empty the list.
      searchList.Items.Clear();
      if (null == searchResponse)
      {
        return;
      }

      // create the image list.
      searchList.SmallImageList = CreateImageList(searchResponse.Words);

      // start the update
      searchList.BeginUpdate();
      foreach (var word in searchResponse.Words)
      {
        var ext = Path.GetExtension(word.FullName);
        string[] row = { word.FullName, word.Actual};
        var item = searchList.Items.Add(word.Name);
        item.ImageKey = ext;
        item.SubItems.AddRange( row );
      }
      searchList.EndUpdate();
    }

    /// <summary>
    /// Get all the icons for all the file extensions.
    /// </summary>
    /// <param name="words"></param>
    /// <returns></returns>
    private static ImageList CreateImageList(IEnumerable<IWord> words)
    {
      var imageList = new ImageList();
      imageList.Images.Add("", SystemIcons.WinLogo);
      foreach (var word in words)
      {
        var ext = Path.GetExtension(word.FullName);
        if (imageList.Images.ContainsKey(ext ?? ""))
        {
          continue;
        }

        // get the icon
        try
        {
          var icon = Icon.ExtractAssociatedIcon(word.FullName);
          if (icon == null)
          {
            imageList.Images.Add(ext, SystemIcons.WinLogo);
            continue;
          }
          imageList.Images.Add(ext, icon);
        }
        catch (Exception)
        {
          imageList.Images.Add(ext, SystemIcons.WinLogo );
        }
      }
      return imageList;
    }
  }
}
