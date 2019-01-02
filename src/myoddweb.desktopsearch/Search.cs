using System;
using System.Windows.Forms;

namespace myoddweb.desktopsearch
{
  public partial class Search : Form
  {
    public Search()
    {
      // create everything
      InitializeComponent();

      // and resize it all.
      OnResize();
    }

    private void OnSizeChanged(object sender, EventArgs e)
    {
      OnResize();
    }

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
