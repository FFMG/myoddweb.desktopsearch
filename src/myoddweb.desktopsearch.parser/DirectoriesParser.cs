using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.interfaces.IO;
using myoddweb.desktopsearch.interfaces.Logging;

namespace myoddweb.desktopsearch.parser
{
  internal class DirectoriesParser
  {
    /// <summary>
    /// The directory parser we will be using.
    /// </summary>
    private readonly IDirectory _directory;

    /// <summary>
    /// The logger that we will be using to log messages.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The folder we will be parsing
    /// </summary>
    private readonly string StartFolder;

    public List<DirectoryInfo> Directories { get; private set; }

    public DirectoriesParser( string startFolder, ILogger logger, IDirectory directory)
    {
      // save the start folde.r
      StartFolder = startFolder ?? throw new ArgumentNullException(nameof(startFolder));

      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      // save the directory parser
      _directory = directory ?? throw new ArgumentNullException(nameof(directory));

      Directories = new List<DirectoryInfo>();
    }

    /// <summary>
    /// Check if we are able to process this directlry.
    /// </summary>
    /// <param name="directoryInfo"></param>
    /// <returns></returns>
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
          if ((FileSystemRights.Read & rule.FileSystemRights) != FileSystemRights.Read)
          {
            continue;
          }

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

    private Task<bool> ParseDirectory(DirectoryInfo directory)
    {
      if (!CanReadDirectory(directory))
      {
        _logger.Warning($"Cannot Parse Directory: {directory.FullName}");
        return Task.FromResult(false);
      }

      // add this directory to our list.
      Directories.Add(directory);

      // we will be parsing it.
      return Task.FromResult(true);
    }

    public async Task<bool> SearchAsync(CancellationToken token)
    {
      // parse the directory
      if (!await _directory.ParseDirectoriesAsync(StartFolder, ParseDirectory, token).ConfigureAwait(false))
      {
        _logger.Warning("The parsing was cancelled");
        return false;
      }

      return true;
    }
  }
}
