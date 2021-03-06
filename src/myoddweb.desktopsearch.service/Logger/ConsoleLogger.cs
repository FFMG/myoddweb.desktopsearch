﻿//This file is part of Myoddweb.DesktopSearch.
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
using myoddweb.desktopsearch.interfaces.Enums;
using myoddweb.desktopsearch.interfaces.Logging;

namespace myoddweb.desktopsearch.service.Logger
{
  internal class ConsoleLogger : ILogger
  {
    private readonly object _lock = new object();

    /// <summary>
    /// The current log level.
    /// </summary>
    private readonly LogLevel _logLevel;

    public ConsoleLogger(LogLevel logLevel)
    {
      _logLevel = logLevel;
    }

    /// <summary>
    /// Check if we can log at a given level
    /// </summary>
    /// <param name="level"></param>
    /// <returns></returns>
    private bool CanLog(LogLevel level)
    {
      return (_logLevel & level) == level;
    }

    /// <summary>
    /// Write a message with the given color, to the screen
    /// </summary>
    /// <param name="logLevel"></param>
    /// <param name="color"></param>
    /// <param name="message"></param>
    private void WriteLine(LogLevel logLevel, ConsoleColor color, string message)
    {
      if (!CanLog(logLevel))
      {
        return;
      }

      lock (_lock)
      {
        var currentColor = Console.ForegroundColor;
        try
        {
          Console.ForegroundColor = color;
          Console.WriteLine(message);
        }
        finally
        {
          Console.ForegroundColor = currentColor;
        }
      }
    }

    /// <inheritdoc />
    public void Error(string message)
    {
      WriteLine(LogLevel.Error, ConsoleColor.Red, message);
    }

    /// <inheritdoc />
    public void Exception(Exception ex )
    {
      if (!CanLog(LogLevel.Error))
      {
        return;
      }
      if (ex is AggregateException ae)
      {
        ae.Handle(e =>
        {
          //  handle it
          Exception(e);

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
      if (!CanLog(LogLevel.Error))
      {
        return;
      }
      Error(message);
      Exception( ex );
    }

    /// <inheritdoc />
    public void Warning(string message)
    {
      WriteLine(LogLevel.Warning, ConsoleColor.Yellow, message);
    }

    /// <inheritdoc />
    public void Information(string message)
    {
      WriteLine(LogLevel.Information, ConsoleColor.Blue, message);
    }

    /// <inheritdoc />
    public void Verbose(string message)
    {
      WriteLine(LogLevel.Verbose, ConsoleColor.White, message);
    }
  }
}
