using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration.Install;
using System.Reflection;
using System.ServiceProcess;
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
    }

    protected override void OnStop()
    {
    }
    #endregion

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
    }
  }
}
