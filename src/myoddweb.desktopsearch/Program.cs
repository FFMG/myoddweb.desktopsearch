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
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using myoddweb.desktopsearch.helper;
using myoddweb.desktopsearch.Interfaces;
using Newtonsoft.Json;

namespace myoddweb.desktopsearch
{
  internal static class Program
  {
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    private static void Main( string[] args )
    {
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);

#if DEBUG
      // if we start the service ... and it is debug
      // offer to atach the debugger.
      Debugger.Launch();
#endif

      var arguments = new ArgumentsParser(args, new Dictionary<string, ArgumentData>
      {
        { "config", new ArgumentData{ IsRequired = false, DefaultValue = "desktop.json"}},
        { "query", new ArgumentData{ IsRequired = false, DefaultValue = ""}}
      });

      Application.Run(new Search( Config( arguments), arguments["query"]));
    }

    private static IConfig Config(ArgumentsParser arguments )
    {
      var json = System.IO.File.ReadAllText( ConfigFile(arguments["config"]));
      return JsonConvert.DeserializeObject<Config.Config>(json);
    }

    private static string ConfigFile( string config )
    {
      var file = Environment.ExpandEnvironmentVariables(config);
      var path = Path.GetDirectoryName(file);
      if ( !string.IsNullOrEmpty(path) && !Directory.Exists(path)) 
      {
        Directory.CreateDirectory(path);
      }
      return file;
    }
  }
}
