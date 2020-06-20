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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.commandlineparser;
using myoddweb.desktopsearch.http;
using myoddweb.desktopsearch.interfaces.Configs;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.parser;
using myoddweb.desktopsearch.processor;
using myoddweb.desktopsearch.service.Configs;
using myoddweb.desktopsearch.service.Logger;
using myoddweb.desktopsearch.service.Persisters;
using Microsoft.Win32;
using myoddweb.commandlineparser.Rules;
using Newtonsoft.Json;
using Directory = myoddweb.desktopsearch.service.IO.Directory;
using ILogger = myoddweb.desktopsearch.interfaces.Configs.ILogger;
using myoddweb.desktopsearch.interfaces.Persisters;
using File = System.IO.File;
using IConfig = myoddweb.desktopsearch.interfaces.Configs.IConfig;

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
    private IParser _parser;

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
    private CommandlineParser _commandlineParsers;

    /// <summary> 
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components;

    /// <summary>
    /// The http server.
    /// </summary>
    private HttpServer _http;

    /// <summary>
    /// The persister.
    /// </summary>
    private IPersister _persister;

    /// <summary>
    /// Create the service event log.
    /// </summary>
    private readonly EventLog _eventLog;

    /// <summary>
    /// The currently running task.
    /// </summary>
    private Task _startupTask;

    public DesktopSearchService()
    {
      InitializeComponent();

      // create the cancellation source
      _cancellationTokenSource = new CancellationTokenSource();

      _eventLog = new EventLog();
      if (!EventLog.SourceExists(DesktopSearchServiceName))
      {
        EventLog.CreateEventSource(
          DesktopSearchServiceName, "Service Log");
      }
      _eventLog.Source = DesktopSearchServiceName;
      _eventLog.Log = "Service Log";
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
#if DEBUG
      // if we start the service ... and it is debug
      // offer to atach the debugger.
      Debugger.Launch();
#endif
      _eventLog.WriteEntry("Starting service");

      // start the parsers task
      _startupTask = Task.Run( () => StartParserAndProcessor(), _cancellationTokenSource.Token );
    }

    protected override void OnStop()
    {
      _eventLog.WriteEntry("Stopping service");
      StopParser();
    }
    #endregion

    /// <summary>
    /// Create the config interface
    /// </summary>
    /// <returns></returns>
    private IConfig CreateConfig()
    {
      var config = _commandlineParsers.Get<string>("config");
      config = !string.IsNullOrEmpty(config) ?  Environment.ExpandEnvironmentVariables(config) : "";
      _eventLog.WriteEntry($"Config location: {config}.");
      var json = File.ReadAllText(config);
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
    private static IDirectory CreateDirectory(interfaces.Logging.ILogger logger, IConfig config )
    {
      var paths = new List<string>( config.Paths.IgnoredPaths );
      paths.AddRange( config.Database?.IgnoredPaths ?? new List<string>() );
      return new Directory( logger, paths);
    }

    /// <summary>
    /// Create the persister
    /// </summary>
    /// <param name="parsers"></param>
    /// <param name="logger"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    private static IPersister CreatePersister(IList<IFileParser> parsers, interfaces.Logging.ILogger logger, IConfig config )
    {
      if( config.Database is ConfigSqliteDatabase sqlData )
      {
        return new SqlitePersister(
          config.Performance, 
          parsers, 
          logger, 
          sqlData, 
          config.MaxNumCharactersPerWords, 
          config.MaxNumCharactersPerParts
          );
      }

      throw new ArgumentException("Unknown Database type.");
    }

    /// <summary>
    /// Delete the performance category if we need to.
    /// </summary>
    /// <param name="performance"></param>
    private static void CreatePerformance( IPerformance performance )
    {
      if (!performance.DeleteStartUp)
      {
        return;
      }
      if (PerformanceCounterCategory.Exists(performance.CategoryName))
      {
        PerformanceCounterCategory.Delete(performance.CategoryName);
      }
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
        System.IO.Directory.SetCurrentDirectory(
          Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? throw new InvalidOperationException()
        );

        // create the config
        var config = CreateConfig();

        // (re)create the performance counters
        CreatePerformance(config.Performance);

        // and the logger
        _logger = CreateLogger(config.Loggers );

        // we now need to create the files parsers
        var fileParsers = CreateFileParsers<IFileParser>(config.Paths.ComponentsPaths);

        // the persister
        _persister = CreatePersister( fileParsers, _logger, config);

        // the directory parser
        var directory = CreateDirectory( _logger, config );

        // and we can now create and start the parser.
        _parser = new Parser( config, _persister, _logger, directory );

        // create the processor
        _processor = new Processor( fileParsers, config.Processors, config.Timers, config.Maintenance, _parser, _persister, _logger, directory, config.Performance );

        // create the http server
        _http = new HttpServer( config.WebServer, _persister, _logger);

        // create the cancellation source
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        // we can now start everything 
        _persister.Start(token);
        _http.Start(token);       //  the http server
        _parser.Start(token);     //  the parser
        _processor.Start(token);  //  the processor

        LogStartupComplete(config);
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

      // if there were any errors in startup stop everything.
      if (errorDuringStartup)
      {
        Stop();
      }

      // return if we had an error or not.
      return !errorDuringStartup;
    }

    /// <summary>
    /// Log informational message giving a sumarry of what is going on.
    /// </summary>
    private void LogStartupComplete( IConfig config )
    {
      var sb = new StringBuilder();
      sb.AppendLine( "Startup complete.");
      sb.AppendLine($"  ProcessID                       : {Process.GetCurrentProcess().Id}");
      sb.AppendLine($"  Platform                        : {(Environment.Is64BitProcess ? "64-bit (x64)" : "32-bit (x86))")}");
      sb.AppendLine($"  Runtime                         : {Environment.Version}");
      sb.AppendLine();

      sb.AppendLine( "Webserver");
      sb.AppendLine($"  Port                            : {config.WebServer.Port}");
      sb.AppendLine();

      sb.AppendLine( "Processors" );
      sb.AppendLine($"  Update files per events         : {config.Processors.UpdatesFilesPerEvent}");
      sb.AppendLine($"  Update folders per events       : {config.Processors.UpdatesFolderPerEvent}");
      sb.AppendLine($"  Events Processor Ms             : {config.Processors.EventsProcessorMs} Ms");
      sb.AppendLine($"  Maintenance Processor Minutes   : {config.Processors.MaintenanceProcessorMinutes} Minutes");
      sb.AppendLine($"  Parser Processor Minutes        : {config.Processors.ParserProcessorMinutes} Minutes"); 
      sb.AppendLine( "  Ignore Files");
      foreach (var ignorefile in config.Processors.IgnoreFiles)
      {
        sb.AppendLine($"    Pattern                       : {ignorefile.Pattern}");
        sb.AppendLine($"    Max size in Mb                : {ignorefile.MaxSizeMegabytes}");
      }
      sb.AppendLine();

      sb.AppendLine("Paths");
      sb.AppendLine($"  Parse Fixed Drives              : {(config.Paths.ParseFixedDrives?"true":"false")}");
      sb.AppendLine($"  Parse Removable Drives          : {(config.Paths.ParseRemovableDrives ? "true" : "false")}");
      sb.AppendLine( "  Included Paths");
      foreach (var path in config.Paths.Paths)
      {
        sb.AppendLine($"    Folder                        : {path}");
      }
      sb.AppendLine( "  Ignored Paths");
      foreach (var ignoredPath in config.Paths.IgnoredPaths)
      {
        sb.AppendLine($"    Folder                        : {ignoredPath}");
      }
      sb.AppendLine();

      sb.AppendLine("Misc");
      sb.AppendLine($"  Max Number Characters Per Words : {config.MaxNumCharactersPerWords}");
      sb.AppendLine($"  Max Number Characters Per Parts : {config.MaxNumCharactersPerParts}");
      sb.AppendLine( "  Maintenance");
      sb.AppendLine($"    From                          : {config.Maintenance.Active.From}:00");
      sb.AppendLine($"    To                            : {config.Maintenance.Active.To}:00");
      sb.AppendLine($"    UTC                           : {(config.Maintenance.Active.Utc?"True":"False")}");

      _logger.Information( sb.ToString() );
    }

    /// <summary>
    /// Stopt the currently running parsers.
    /// </summary>
    private void StopParser()
    {
      _cancellationTokenSource?.Cancel();

      _parser?.Stop();
      _processor?.Stop();
      _http?.Stop();
      _persister?.Stop();

      // wait for the service task to complete.
      _startupTask?.Wait();

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
      _commandlineParsers = new CommandlineParser(args,
        new CommandlineArgumentRules
        {
          new OptionalCommandlineArgumentRule( "config", "config.json"),
          new OptionalCommandlineArgumentRule( "install", "false", "Install the service" ),
          new OptionalCommandlineArgumentRule( "uninstall", "false", "Uninstall the service" ),
          new OptionalCommandlineArgumentRule( "console", "false", "Run in console mode" )
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
      if (null == _commandlineParsers)
      {
        // the arguments must be created @see InvokeAction(string[] args)
        throw new ArgumentNullException(nameof(_commandlineParsers));
      }

      // are we installing?
      if (_commandlineParsers.IsSet("install"))
      {
        InvokeActionInstall();
        return;
      }

      // uninstalling?
      if (_commandlineParsers.IsSet("uninstall"))
      {
        InvokeActionUnInstall();
        return;
      }

      // running as a console
      if (_commandlineParsers.IsSet("console"))
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
        var clone = _commandlineParsers.Clone().Remove("install");

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
        // start the parsers task
        _startupTask = Task.Run(StartParserAndProcessor, _cancellationTokenSource.Token);

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
