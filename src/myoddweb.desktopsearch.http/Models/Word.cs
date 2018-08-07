using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace myoddweb.desktopsearch.http.Models
{
  internal class Word
  {
    /// <summary>
    /// The file name only
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The directory path
    /// </summary>
    public string Directory { get; set; }

    /// <summary>
    /// The file full name
    /// </summary>
    public string FullName { get; set; }

    /// <summary>
    /// The actual word that was matched.
    /// </summary>
    public string Actual { get; set; }
  }
}
