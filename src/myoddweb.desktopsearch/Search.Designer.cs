namespace myoddweb.desktopsearch
{
  partial class Search
  {
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null))
      {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
      this.searchBox = new System.Windows.Forms.TextBox();
      this.searchList = new System.Windows.Forms.ListView();
      this.SuspendLayout();
      // 
      // searchBox
      // 
      this.searchBox.Location = new System.Drawing.Point(12, 3);
      this.searchBox.Name = "searchBox";
      this.searchBox.Size = new System.Drawing.Size(776, 20);
      this.searchBox.TabIndex = 0;
      // 
      // searchList
      // 
      this.searchList.Location = new System.Drawing.Point(12, 29);
      this.searchList.Name = "searchList";
      this.searchList.Size = new System.Drawing.Size(776, 220);
      this.searchList.TabIndex = 1;
      this.searchList.UseCompatibleStateImageBehavior = false;
      // 
      // Search
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(798, 261);
      this.Controls.Add(this.searchList);
      this.Controls.Add(this.searchBox);
      this.Name = "Search";
      this.Text = "Search Desktop";
      this.SizeChanged += new System.EventHandler(this.OnSizeChanged);
      this.ResumeLayout(false);
      this.PerformLayout();

    }

    #endregion

    private System.Windows.Forms.TextBox searchBox;
    private System.Windows.Forms.ListView searchList;
  }
}

