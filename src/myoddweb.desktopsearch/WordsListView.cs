using myoddweb.desktopsearch.interfaces.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

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

      var word = GetCurrentWord();
      var cm = new ContextMenuStrip();

      var tt = cm.Items.Add( "Open");
      tt.Font = new Font(tt.Font, FontStyle.Bold);
      cm.Items.Add("Open Path");
      cm.Items.Add("Copy full path to clipboard");
      cm.Items.Add("Open With");
      ContextMenuStrip = cm;
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
