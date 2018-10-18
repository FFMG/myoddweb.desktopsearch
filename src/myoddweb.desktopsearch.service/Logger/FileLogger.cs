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
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.Enums;
using myoddweb.desktopsearch.interfaces.Logging;

namespace myoddweb.desktopsearch.service.Logger
{
  internal class FileLogger : ILogger
  {
    private readonly object _lock = new object();

    /// <summary>
    /// The current log level.
    /// </summary>
    private readonly LogLevel _logLevel;

    /// <summary>
    /// The full path name
    /// </summary>
    private readonly string _fileName;

    public FileLogger(DirectoryInfo baseDirectory, LogLevel logLevel)
    {
      _logLevel = logLevel;

      var now = DateTime.UtcNow;
      var timestamp = now.ToString("yyyy-MM-dd");
      _fileName = Path.Combine( baseDirectory.FullName, $"Log.{timestamp}.log");
      if (!File.Exists(_fileName))
      {
        File.Create(_fileName);
      }
      if (!File.Exists(_fileName))
      {
        throw new ArgumentException($"I was unable to create the log: {_fileName}");
      }
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
    /// <param name="message"></param>
    private void WriteLine(LogLevel logLevel, string message)
    {
      if (!CanLog(logLevel))
      {
        return;
      }

      // use the time before the lock...
      var now = DateTime.UtcNow;
      var timestamp = now.ToString("yyyy/MM/dd HH:mm:ss");

      // the log level message
      var logMessage = LogMessage(logLevel);

      const int timeOut = 100;
      var stopwatch = new Stopwatch();
      stopwatch.Start();
      while (true)
      {
        try
        {
          // try and get the lock
          lock (_lock)
          {
            File.AppendAllText(_fileName, $"[{timestamp}] : [{logMessage}] {message}{Environment.NewLine}");
          }
          break;
        }
        catch
        {
          //File not available, conflict with other class instances or application
        }

        if (stopwatch.ElapsedMilliseconds > timeOut)
        {
          //Give up.
          break;
        }
        //Wait and Retry
        Task.Delay(5).Wait();
      }
      stopwatch.Stop();
    }

    private static string LogMessage(LogLevel logLevel)
    {
      switch (logLevel)
      {
        case LogLevel.None:
          return "None";
        case LogLevel.Verbose:
          return "Verbose";
        case LogLevel.Information:
          return "Information";
        case LogLevel.Warning:
          return "Warning";
        case LogLevel.Error:
          return "Error";
        case LogLevel.All:
          return "All";

        default:
          throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null);
      }
    }

    /// <inheritdoc />
    public void Error(string message)
    {
      WriteLine(LogLevel.Error, message);
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
      Exception(ex);
    }

    /// <inheritdoc />
    public void Warning(string message)
    {
      WriteLine(LogLevel.Warning, message);
    }

    /// <inheritdoc />
    public void Information(string message)
    {
      WriteLine(LogLevel.Information, message);
    }

    /// <inheritdoc />
    public void Verbose(string message)
    {
      WriteLine(LogLevel.Verbose, message);
    }
  }
}
