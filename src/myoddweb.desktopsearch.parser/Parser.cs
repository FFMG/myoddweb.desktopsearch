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
using System.IO;
using System.Security;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Logging;

namespace myoddweb.desktopsearch.parser
{
  public class Parser
  {
    /// <summary>
    /// The logger that we will be using to log messages.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The directory parser we will be using.
    /// </summary>
    private readonly IDirectory _directory;

    public Parser(ILogger logger, IDirectory directory)
    {
      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      // save the directory parser
      _directory = directory ?? throw new ArgumentNullException(nameof(directory));
    }

    /// <summary>
    /// Start parsing.
    /// </summary>
    public void Start( CancellationToken token )
    {
      var thread = new Thread(async () => await Work(token).ConfigureAwait(false));
      thread.Start();
      _logger.Information("Parser started");
    }

    public async Task<bool> Work(CancellationToken token)
    {
      // parse the directory
      return await _directory.ParseAsync("c:\\", ProcessFile, ParseDirectory, token).ConfigureAwait( false );
    }

    public bool CanReadDirectory(DirectoryInfo directoryInfo)
    {
      try
      {
        var accessControlList = directoryInfo.GetAccessControl();

        var accessRules =
          accessControlList?.GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier));
        if (accessRules == null)
        {
          return false;
        }

        var readAllow = false;
        var readDeny = false;
        foreach (FileSystemAccessRule rule in accessRules)
        {
          if ((FileSystemRights.Read & rule.FileSystemRights) != FileSystemRights.Read){ continue;}

          switch (rule.AccessControlType)
          {
            case AccessControlType.Allow:
              readAllow = true;
              break;

            case AccessControlType.Deny:
              readDeny = true;
              break;
          }
        }

        return readAllow && !readDeny;
      }
      catch (UnauthorizedAccessException)
      {
        return false;
      }
      catch (SecurityException)
      {
        return false;
      }
      catch (Exception e)
      {
        _logger.Error(e.Message);
        return false;
      }
    }

    /// <summary>
    /// Check if we want to parse this directory or not.
    /// </summary>
    /// <param name="directoryInfo"></param>
    /// <returns></returns>
    private bool ParseDirectory(DirectoryInfo directoryInfo )
    {
      if (!CanReadDirectory(directoryInfo))
      {
        _logger.Warning($"Cannot Parse Directory: {directoryInfo.Name}");
        return false;
      }

      // we can parse it.
      _logger.Verbose($"Parsing Directory: {directoryInfo.Name}");

      // we always parse sub directories
      return true;
    }

    /// <summary>
    /// Process a file that has been found.
    /// </summary>
    /// <param name="fileInfo"></param>
    private void ProcessFile(FileInfo fileInfo )
    {
      _logger.Verbose( $"Processing File: {fileInfo.Name}");
    }

    /// <summary>
    /// Stop parsing
    /// </summary>
    public void Stop()
    {
    }
  }
}
