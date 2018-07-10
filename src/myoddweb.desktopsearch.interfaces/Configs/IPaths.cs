using System.Collections.Generic;

namespace myoddweb.desktopsearch.interfaces.Configs
{
  public interface IPaths
  {
    /// <summary>
    /// Do we want to parse all the Fixed drives or now?
    /// </summary>
    bool ParseFixedDrives { get; }

    /// <summary>
    /// Do we want to parse all the removable drives or not?
    /// </summary>
    bool ParseRemovableDrives { get; }

    /// <summary>
    /// Extra paths that we want to parse
    /// </summary>
    List<string> Paths { get; }

    /// <summary>
    /// Path that we want to ignore by default.
    /// </summary>
    List<string> IgnoredPaths { get; }
  }
}