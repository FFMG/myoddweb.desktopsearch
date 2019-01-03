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
namespace myoddweb.desktopsearch.Interfaces
{
  internal interface IConfig
  {
    /// <summary>
    /// The Url of the API
    /// </summary>
    string Url { get; }

    /// <summary>
    /// The port number
    /// </summary>
    int Port { get; }

    /// <summary>
    /// The minimum number of characters before we do a search
    /// </summary>
    int MinimumSearchLength { get; }

    /// <summary>
    /// Where we will save some of the values, (screen size/position and so on).
    /// </summary>
    string Save { get; }

    /// <summary>
    /// How long do we wait, in ms before calling the API
    /// </summary>
    int KeyDownIntervalMs { get; }

    /// <summary>
    /// How many items we want to get back from the API
    /// </summary>
    int MaxNumberOfItemsToFetch { get; }
  }
}
