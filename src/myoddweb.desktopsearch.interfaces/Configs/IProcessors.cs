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
    /// When the file/folders updates are compelte
    /// the is the amount of time we want to wait
    /// before we check again...
    /// </summary>
    int QuietEventsProcessorMs { get; }

    /// <summary>
    /// If we still have files/folders/... to check
    /// this is the amount of time between processing we want to wai.t
    /// </summary>
    int BusyEventsProcessorMs { get; }

    /// <summary>
    /// The number of concurrent directories processor
    /// If that number is too high... things might break.
    /// </summary>
    int ConcurrentDirectoriesProcessor { get; }

    /// <summary>
    /// The number of concurrent file processor
    /// If that number is too high... things might break.
    /// </summary>
    int ConcurrentFilesProcessor { get; }

    /// <summary>
    /// The number of events we wand to do per processing events.
    /// Don't make that number too small as it will take forever to parse
    /// But also not too big as it blocks the database when/if there is work to do.
    /// </summary>
    int UpdatesPerFilesEvent { get; }
  }
}