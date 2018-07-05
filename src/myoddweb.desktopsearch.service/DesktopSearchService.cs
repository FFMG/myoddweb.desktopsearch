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
using System.Reflection;
using System.ServiceProcess;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace myoddweb.desktopsearch.service
{
  internal class DesktopSearchService : ServiceBase
  {
    private const string DesktopSearchServiceName = "Myoddweb.DesktopSearch";

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
      StartMonitors(true);
    }

    protected override void OnStop()
    {
      StopMonitors();
    }
    #endregion

    /// <summary>
    /// Start the process as a service or as a console app.
    /// </summary>
    /// <param name="isService"></param>
    /// <returns></returns>
    private bool StartMonitors( bool isService )
    {
      var errorDuringStartup = false;
      try
      {
        _startupThreadBusy = true;
      }
      catch (AggregateException)
      {
        errorDuringStartup = true;
      }
      finally
      {
        _startupThreadBusy = false;
      }

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
    private void StopMonitors()
    {
      // don't do the stop while the startup thread is still busy starting up
      while (_startupThreadBusy)
      {
        Task.Yield();
      }
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
        Console.WriteLine("Running as a console.");
        if (!StartMonitors(false))
        {
          Console.WriteLine("There was a problem starting the processes.");
          return;
        }

        Console.WriteLine("Press Ctrl+C to stop the monitors.");

        // then wait for the user to press a key
        var keepRunning = true;
        Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs e)
        {
          Console.WriteLine("Stop detected.");
          e.Cancel = true;
          keepRunning = false;
        };

        while (keepRunning)
        {
          // wait a little.
          Task.Delay(1000).Wait();
        }
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
      StopMonitors();

    }
  }
}
