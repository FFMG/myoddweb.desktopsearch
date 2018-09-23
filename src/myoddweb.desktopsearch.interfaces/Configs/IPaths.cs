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
    IList<string> Paths { get; }

    /// <summary>
    /// Path that we want to ignore by default.
    /// </summary>
    IList<string> IgnoredPaths { get; }

    /// <summary>
    /// If we want to ignore the internet path or not.
    /// </summary>
    bool IgnoreInternetCache { get; }

    /// <summary>
    /// If we want to ignore the recycle bins or not.
    /// </summary>
    bool IgnoreRecycleBins { get; }

    /// <summary>
    /// Do we want to exclude the current path from parsing.
    /// </summary>
    bool IgnoreCurrentPath { get; }

    /// <summary>
    /// Paths where all the components are located.
    /// </summary>
    IList<string> ComponentsPaths { get; }
  }
}