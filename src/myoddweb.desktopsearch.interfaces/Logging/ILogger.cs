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

using System;

namespace myoddweb.desktopsearch.interfaces.Logging
{
  public interface ILogger
  {
    /// <summary>
    /// Log an error message.
    /// </summary>
    /// <param name="message"></param>
    void Error(string message);

    /// <summary>
    /// Log a warning
    /// </summary>
    /// <param name="message"></param>
    void Warning(string message);

    /// <summary>
    /// Log an information message
    /// </summary>
    /// <param name="message"></param>
    void Information(string message);

    /// <summary>
    /// Log a verbose message
    /// </summary>
    /// <param name="message"></param>
    void Verbose(string message);

    /// <summary>
    /// Log an Exception
    /// </summary>
    /// <param name="ex"></param>
    void Exception(Exception ex);

    /// <summary>
    /// Log an Exception with a message
    /// </summary>
    /// <param name="message">The message we want to add.</param>
    /// <param name="ex"></param>
    void Exception(string message, Exception ex);
  }
}