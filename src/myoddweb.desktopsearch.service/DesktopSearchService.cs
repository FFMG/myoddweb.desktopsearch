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
using System.Collections.Specialized;
using System.Configuration.Install;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;
using myoddweb.desktopsearch.http;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.parser;
using myoddweb.desktopsearch.processor;
using myoddweb.desktopsearch.service.Configs;
using myoddweb.desktopsearch.service.Logger;
using myoddweb.desktopsearch.service.Persisters;
using Microsoft.Win32;
using Newtonsoft.Json;
using Directory = myoddweb.desktopsearch.service.IO.Directory;
using ILogger = myoddweb.desktopsearch.interfaces.Configs.ILogger;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.service
{
  internal class DesktopSearchService : ServiceBase
  {
    /// <summary>
    /// The name of the server.
    /// </summary>
    private const string DesktopSearchServiceName = "Myoddweb.DesktopSearch";

    /// <summary>
    /// This is the parser we are currently working with.
    /// </summary>
    private Parser _parser;

    /// <summary>
    /// The logger created.
    /// </summary>
    private interfaces.Logging.ILogger _logger;

    /// <summary>
    /// The files/folders processor.
    /// </summary>
    private Processor _processor;

    /// <summary>
    /// The cancellation source
    /// </summary>
    private CancellationTokenSource _cancellationTokenSource;

    /// <summary>
    /// The parsed arguments.
    /// </summary>
    private ArgumentsParser _arguments;

    /// <summary> 
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components;

    /// <summary>
    /// We use this to prevent a shutdown while we are still starting.
    /// </summary>
    private bool _startupThreadBusy;

    /// <summary>
    /// The http server.
    /// </summary>
    private HttpServer _http;

    public DesktopSearchService()
    {
      InitializeComponent();
    }

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
      if (disposing)
      {
        components?.Dispose();
      }
      base.Dispose(disposing);
    }

    /// <summary> 
    /// Required method for Designer support - do not modify 
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
      components = new System.ComponentModel.Container();
      ServiceName = $"{DesktopSearchServiceName} Service";
    }

    #region ServiceBase overrides
    protected override void OnStart(string[] args)
    {
      StartParserAndProcessor();
    }

    protected override void OnStop()
    {
      StopParser();
    }
    #endregion

    /// <summary>
    /// Create the config interface
    /// </summary>
    /// <returns></returns>
    private interfaces.Configs.IConfig CreateConfig()
    {
      var json = File.ReadAllText(_arguments["config"]);
      return JsonConvert.DeserializeObject<Config>(json);
    }

    /// <summary>
    /// load all the file parsers.
    /// </summary>
    /// <param name="paths"></param>
    /// <returns></returns>
    private static List<T> CreateFileParsers<T>(IEnumerable<string> paths)
    {
      var parsers = new List<T>();
      foreach (var path in paths)
      {
        var directory = new DirectoryInfo(path);
        if (!helper.File.CanReadDirectory(directory))
        {
          continue;
        }

        // get all the dlls
        var dlls = directory.EnumerateFiles("*.dll");
        foreach (var dll in dlls)
        {
          try
          {
            var assembly = Assembly.LoadFile(dll.FullName);
            var types = assembly.GetTypes();
            foreach (var type in types.Where( t => t.IsClass && t.IsPublic))
            {
              var interfaces = type.GetInterfaces();
              if (!interfaces.Contains(typeof(T)))
              {
                continue;
              }

              var obj = Activator.CreateInstance(type);
              var t = (T)obj;
              parsers.Add(t);
            }
          }
          catch (Exception)
          {
            //  ignore
          }
        }
      }
      return parsers;
    }

    /// <summary>
    /// Create the logger interface
    /// </summary>
    /// <returns></returns>
    private static interfaces.Logging.ILogger CreateLogger( IEnumerable<ILogger> configLoggers)
    {
      var loggers = new List<interfaces.Logging.ILogger>();
      foreach (var configLogger in configLoggers)
      {
        switch (configLogger)
        {
          case ConfigFileLogger dl:
          {
            var logger = new FileLogger( dl.BaseDirectoryInfo, dl.LogLevel);
            logger.Information("Started logger.");
            loggers.Add(logger);
          }
          break;

          case ConfigConsoleLogger cl:
          {
            var logger = new ConsoleLogger(cl.LogLevel);
            logger.Information("Running as a console.");
            loggers.Add( logger );
          }
          break;

          default:
            throw new ArgumentException( $"Unknown Config logger type: '{configLogger}'");
        }
      }
      return new Loggers(loggers);
    }

    /// <summary>
    /// Create the directories parser/handler.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    private static IDirectory CreateDirectory(interfaces.Logging.ILogger logger, interfaces.Configs.IConfig config )
    {
      var paths = config.Paths.IgnoredPaths;
      paths.AddRange(config.Database?.IgnoredPaths ?? new List<string>());
      return new Directory( logger, paths);
    }

    /// <summary>
    /// Create the persister
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    private static IPersister CreatePersister(interfaces.Logging.ILogger logger, interfaces.Configs.IConfig config )
    {
      if( config.Database is ConfigSqliteDatabase sqlData )
      {
        return new SqlitePersister(logger, sqlData, config.MaxNumCharacters );
      }

      throw new ArgumentException("Unknown Database type.");
    }

    /// <summary>
    /// Start the process as a service or as a console app.
    /// </summary>
    /// <returns></returns>
    private bool StartParserAndProcessor()
    {
      var errorDuringStartup = false;
      try
      {
        _startupThreadBusy = true;

        // create the config
        var config = CreateConfig();

        // and the logger
        _logger = CreateLogger(config.Loggers );

        // create the cancellation source
        _cancellationTokenSource = new CancellationTokenSource();

        var token = _cancellationTokenSource.Token;

        // the persister
        var persister = CreatePersister( _logger, config);

        // the directory parser
        var directory = CreateDirectory( _logger, config );

        // and we can now create and start the parser.
        _parser = new Parser( config, persister, _logger, directory );

        // we now need to create the files parsers
        var fileParsers = CreateFileParsers<IFileParser>( config.Paths.ComponentsPaths );

        // create the processor
        _processor = new Processor( fileParsers, config.Processors, persister, _logger, directory);

        // create the http server
        _http = new HttpServer( config.WebServer, persister, _logger);

        // we can now start everything 
        persister.Start(token);
        _http.Start(token);       //  the http server
        _parser.Start(token);     //  the parser
        _processor.Start(token);  //  the processor
      }
      catch (AggregateException ex)
      {
        errorDuringStartup = true;
        if (null != _logger)
        {
          _logger.Exception(ex);
        }
        else
        {
          Console.WriteLine( ex.Message );
        }
      }
      catch (Exception ex )
      {
        errorDuringStartup = true;
        if (null != _logger)
        {
          _logger.Exception(ex);
        }
        else
        {
          Console.WriteLine(ex.Message);
        }
      }
      finally
      {
        _startupThreadBusy = false;
      }

      // if there were any errors in startup stop everything.
      if (errorDuringStartup)
      {
        Stop();
      }

      // return if we had an error or not.
      return !errorDuringStartup;
    }

    /// <summary>
    /// Stopt the currently running parsers.
    /// </summary>
    private void StopParser()
    {
      // don't do the stop while the startup thread is still busy starting up
      SpinWait.SpinUntil( () => !_startupThreadBusy);

      _cancellationTokenSource?.Cancel();
      _parser?.Stop();
      _processor?.Stop();
      _http?.Stop();

      _cancellationTokenSource = null;
      _parser = null;
      _processor = null;
    }

    /// <summary>
    /// Invoke the actions directrly from the Main( string[] )
    /// </summary>
    /// <param name="args"></param>
    public void InvokeAction(string[] args)
    {
      _arguments = new ArgumentsParser(args, new Dictionary<string, ArgumentData>
      {
        { "config", new ArgumentData{ IsRequired = false, DefaultValue = "config.json"}},
        { "install", new ArgumentData{ IsRequired = false} },
        { "uninstall", new ArgumentData{ IsRequired = false} }
      });

      // we can now call with the parameters.
      InvokeAction();
    }

    /// <summary>
    /// Called by InvokeAction(string[] args) and assumes that ArgumentParser has been created.
    /// </summary>
    private void InvokeAction()
    {
      // save the arguments.
      if (null == _arguments)
      {
        // the arguments must be created @see InvokeAction(string[] args)
        throw new ArgumentNullException(nameof(_arguments));
      }

      // are we installing?
      if (_arguments.IsSet("install"))
      {
        InvokeActionInstall();
        return;
      }

      // uninstalling?
      if (_arguments.IsSet("uninstall"))
      {
        InvokeActionUnInstall();
        return;
      }

      // running as a console
      if (_arguments.IsSet("console"))
      {
        RunAsConsole();
        return;
      }

      // then we must be running as a service.
      RunAsService();
    }

    /// <summary>
    /// Install the service.
    /// </summary>
    private void InvokeActionInstall()
    {
      try
      {
        DoInstall();
      }
      catch (Exception e)
      {
        Console.WriteLine($"There was an insue installing the service {e}");
      }
    }

    /// <summary>
    /// Uninstall the servoce
    /// </summary>
    private void InvokeActionUnInstall()
    {
      try
      {
        DoUnInstall();
      }
      catch (Exception e)
      {
        Console.WriteLine($"There was an insue uninstalling the service {e}");
      }
    }

    /// <summary>
    /// Do the actual install.
    /// </summary>
    private void DoInstall()
    {
      using (var serviceProcessInstaller = new ServiceProcessInstaller { Account = ServiceAccount.LocalSystem })
      {
        // make sure we do not have the install listed.
        var clone = _arguments.Clone().Remove("install");

        // figure out the command line based on whether we have a custom service name or not
        var processLaunchCommand = $"\"{Assembly.GetEntryAssembly().Location}\" {clone}";

        // set this up as part of the install params - it won't work of course since the silly
        // installer is going to put quotes around the whole thing thus screwing our -sn ServiceName
        // paramater - we'll fix this after the intall step below with the custom registry fix
        string[] cmdline =
        {
          $"/assemblypath=\"{processLaunchCommand}\""
        };

        using (
          var serviceInstaller = new ServiceInstaller
          {
            Context = new InstallContext(null, cmdline),
            DisplayName = ServiceName,
            Description = $"{DesktopSearchServiceName} Service to parse folders/files.",
            ServiceName = ServiceName,
            StartType = ServiceStartMode.Automatic,
            Parent = serviceProcessInstaller
          })
        {
          serviceInstaller.Install(new ListDictionary());
        }

        // now fix up the command line - the braindead installer puts quotes around everything
        var serviceKeyName = $@"SYSTEM\CurrentControlSet\Services\{ServiceName}";
        using (var key = Registry.LocalMachine.OpenSubKey(serviceKeyName, true))
        {
          // check if key is null - in weird cases I guess the installer might have failed to populate this
          if (key == null)
          {
            var msg = $"Failed to locate service key {serviceKeyName}";
            throw new Exception(msg);
          }
          key.SetValue("ImagePath", processLaunchCommand);
        }
      }
      Console.WriteLine("The service has been installed!");
    }

    /// <summary>
    /// Do the actual un-install.
    /// </summary>
    private void DoUnInstall()
    {
      using (var serviceInstaller = new ServiceInstaller())
      {
        var context = new InstallContext();
        serviceInstaller.Context = context;
        serviceInstaller.ServiceName = ServiceName;

        // ReSharper disable once AssignNullToNotNullAttribute
        serviceInstaller.Uninstall(null);
      }
      Console.WriteLine("The service has been uninstalled!");
    }

    /// <summary>
    /// We selected to run as a service rather than a console.
    /// </summary>
    private void RunAsService()
    {
      Run(this);
    }

    /// <summary>
    /// We selected to run as a console rather than a service.
    /// </summary>
    private void RunAsConsole()
    {
      try
      {
        if (!StartParserAndProcessor())
        {
          return;
        }

        Console.WriteLine("Press Ctrl+C to stop the monitors.");

        // then wait for the user to press a key
        var exitEvent = new ManualResetEvent(false);
        Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs e)
        {
          e.Cancel = true;
          Console.WriteLine("Stop detected.");
          exitEvent.Set();
        };

        exitEvent.WaitOne();
      }
      catch (Exception ex)
      {
        Console.WriteLine( ex.Message );
        while (ex.InnerException != null)
        {
          ex = ex.InnerException;
          Console.WriteLine(ex.Message);
        }
      }

      // if we are here, we stopped
      // they might throw as well, but it is up to the 
      // stop monitor function to handle it.
      StopParser();
    }
  }
}
