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

namespace myoddweb.desktopsearch.service
{
  internal class Program
  {
    private static void Main(string[] args)
    {
      // Add the event handler for handling non-UI thread exceptions to the event. 
      AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
      using (var service = new DesktopSearchService())
      {
        service.InvokeAction(args);
      }
    }

    // Handle the UI exceptions by showing a dialog box, and asking the user whether
    // or not they wish to abort execution.
    // NOTE: This exception cannot be kept from terminating the application - it can only 
    // log the event, and inform the user about it. 
    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
      var ex = (Exception)e.ExceptionObject;
      const string errorMsg = "An application error occurred. Please contact the adminstrator " +
                              "with the following information:\n\n";

      // Since we can't prevent the app from terminating, log this to the event log.
      if (!EventLog.SourceExists("ThreadException"))
      {
        EventLog.CreateEventSource("ThreadException", "Application");
      }

      // Create an EventLog instance and assign its source.
      var myLog = new EventLog
      {
        Source = "ThreadException"
      };
      myLog.WriteEntry(errorMsg + ex.Message + "\n\nStack Trace:\n" + ex.StackTrace);
    }
  }
}
