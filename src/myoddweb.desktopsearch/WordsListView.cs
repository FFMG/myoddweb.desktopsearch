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
using myoddweb.desktopsearch.interfaces.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using myoddweb.desktopsearch.Helpers;

namespace myoddweb.desktopsearch
{
  internal class WordsListView : ListView
  {
    #region Member variable
    /// <summary>
    /// The images we will be adding to show the icons for each file/folder.
    /// </summary>
    private readonly ImageList _imageList;

    /// <summary>
    /// The current list of words.
    /// </summary>
    private IList<IWord> _words;
    #endregion

    public WordsListView()
    {
      _imageList = new ImageList();
      _imageList.Images.Add("", SystemIcons.WinLogo);

      MouseDown += OnMouseDown;
    }

    #region Private functions
    /// <summary>
    /// Get all the icons for all the file extensions.
    /// </summary>
    /// <returns></returns>
    private ImageList GetUpdatedImageList()
    {
      // do we have any words?
      if (null == _words)
      {
        return _imageList;
      }

      foreach (var word in _words)
      {
        var ext = Path.GetExtension(word.FullName);
        if (_imageList.Images.ContainsKey(ext ?? ""))
        {
          continue;
        }

        // get the icon
        try
        {
          var icon = Icon.ExtractAssociatedIcon(word.FullName);
          if (icon == null)
          {
            _imageList.Images.Add(ext, SystemIcons.WinLogo);
            continue;
          }
          _imageList.Images.Add(ext, icon);
        }
        catch (Exception)
        {
          _imageList.Images.Add(ext, SystemIcons.WinLogo);
        }
      }
      return _imageList;
    }

    /// <summary>
    /// Reset all the items.
    /// </summary>
    private void ResetItems()
    {
      // empty the list.
      Items.Clear();

      // remove the context menu.
      ContextMenuStrip = null;

      // reset the words
      _words = null;
    }

    /// <summary>
    /// On mouse down.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnMouseDown(object sender, MouseEventArgs e)
    {
      if (e.Button != MouseButtons.Right)
      {
        return;
      }

      var lstHitTestInfo = HitTest(e.X, e.Y);
      if (lstHitTestInfo.Item == null)
      {
        return;
      }

      // make sure that the item is selected.
      foreach (ListViewItem item in Items)
      {
        item.Selected = false;
      }
      Items[lstHitTestInfo.Item.Index].Selected = true;
      Select();

      var cm = new ContextMenuStrip();

      // open
      var ttOpen = cm.Items.Add( "Open");
      ttOpen.Font = new Font(ttOpen.Font, FontStyle.Bold);
      ttOpen.Click += OnOpen;
      cm.Items.Add("Open Path");

      // copy top clipboard
      var ttCopy = cm.Items.Add("Copy full path to clipboard");
      ttCopy.Click += CopyToClipboard;

      // open with
      var ttOpenWith = cm.Items.Add("Open With");
      ttOpenWith.Click += OnOpenWith;
      ContextMenuStrip = cm;
    }

    /// <summary>
    /// Copy the full path tot he clipboard.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void CopyToClipboard(object sender, EventArgs e)
    {
      var word = GetCurrentWord();
      if (null == word)
      {
        return;
      }
      Clipboard.SetText(word.FullName);
    }

    /// <summary>
    /// Open a selected file 'with'
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnOpenWith(object sender, EventArgs e)
    {
      var word = GetCurrentWord();
      if (null == word)
      {
        return;
      }

      // open the file 'with'
      Shell.OpenWith(word.FullName );
    }

    /// <summary>
    /// When we select an item
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnOpen(object sender, EventArgs e)
    {
      var word = GetCurrentWord();
      if( null == word )
      {
        return;
      }

      using (var pProcess = new Process())
      {
        pProcess.StartInfo.FileName = word.FullName;
        pProcess.StartInfo.Arguments = ""; //argument
        pProcess.StartInfo.UseShellExecute = true;
        pProcess.StartInfo.RedirectStandardOutput = false;
        pProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
        pProcess.StartInfo.CreateNoWindow = true; //not diplay a windows
        pProcess.Start();
      }
    }

    #endregion

    #region Public functions
    /// <summary>
    /// Get the currently selected word, if we have one.
    /// </summary>
    /// <returns></returns>
    public IWord GetCurrentWord()
    {
      if (null == _words)
      {
        return null;
      }
      var idx = SelectedItems.Count > 0 ? 0 : -1;
      if (-1 == idx)
      {
        return null;
      }

      var selectedItemIndex = SelectedItems[idx].Index;
      return _words.Count >= selectedItemIndex ? _words[selectedItemIndex] : null;
    }

    /// <summary>
    /// Update the current list of words.
    /// </summary>
    /// <param name="words"></param>
    public void Update(IList<IWord> words)
    {
      // reset everything
      ResetItems();
      if (null == words)
      {
        return;
      }

      // set the words.
      _words = words;

      // create the image list.
      SmallImageList = GetUpdatedImageList();

      // start the update
      BeginUpdate();
      foreach (var word in words)
      {
        var ext = Path.GetExtension(word.FullName);
        string[] row = { word.FullName, word.Actual };
        var item = Items.Add(word.Name);
        item.ImageKey = ext;
        item.SubItems.AddRange(row);
      }
      EndUpdate();
    }
    #endregion
  }
}
