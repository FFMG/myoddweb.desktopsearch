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
namespace myoddweb.desktopsearch.interfaces.Configs
{
  public interface IProcessors
  {
    /// <summary>
    /// How many folders we will be processing at a time.
    /// The numbers are dynamically adjusted.
    /// </summary>
    long NumberOfFoldersToProcessPerEvent { get; }

    /// <summary>
    /// How many files we will be processing at a time.
    /// The numbers are dynamically adjusted.
    /// </summary>
    long NumberOfFilesToProcessPerEvent { get; }
  }
}