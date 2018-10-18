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
using System.Collections.Generic;
using myoddweb.desktopsearch.interfaces.Enums;
using myoddweb.desktopsearch.interfaces.Logging;

namespace myoddweb.desktopsearch.service.Logger
{
  internal class Loggers : ILogger
  {
    /// <summary>
    /// All the loggers
    /// </summary>
    private readonly List<ILogger> _loggers;

    public Loggers(List<ILogger> loggers)
    {
      _loggers = loggers;
    }

    /// <summary>
    /// Write a message with the given color, to the screen
    /// </summary>
    /// <param name="logLevel"></param>
    /// <param name="message"></param>
    private void Log(LogLevel logLevel, string message)
    {
      foreach (var logger in _loggers)
      {
        switch (logLevel)
        {
          case LogLevel.Verbose:
            logger.Verbose(message);
            break;

          case LogLevel.Information:
            logger.Information(message);
            break;

          case LogLevel.Warning:
            logger.Warning(message);
            break;

          case LogLevel.Error:
            logger.Error(message);
            break;

          case LogLevel.None:
            break;

          case LogLevel.All:
            logger.Error(message); // All is an error I guess.
            break;

          default:
            throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null);
        }
      }
    }

    /// <inheritdoc />
    public void Exception(Exception ex)
    {
      if (ex is AggregateException ae )
      {
        ae.Handle( e => 
        {
          //  handle it
          Exception( e );

          // we handled it.
          return true;
        });
        return;
      }
      while (true)
      {
        Error(ex.ToString());
        if (ex.InnerException != null)
        {
          ex = ex.InnerException;
          continue;
        }
        break;
      }
    }

    /// <inheritdoc />
    public void Exception(string message, Exception ex)
    {
      Error(message);
      Exception(ex);
    }

    /// <inheritdoc />
    public void Error(string message)
    {
      Log(LogLevel.Error, message);
    }

    /// <inheritdoc />
    public void Warning(string message)
    {
      Log(LogLevel.Warning, message);
    }

    /// <inheritdoc />
    public void Information(string message)
    {
      Log(LogLevel.Information, message);
    }

    /// <inheritdoc />
    public void Verbose(string message)
    {
      Log(LogLevel.Verbose, message);
    }
  }
}
