using System;
using System.IO;
using System.Security;
using System.Security.AccessControl;
using myoddweb.desktopsearch.interfaces.Logging;

namespace myoddweb.desktopsearch.parser
{
  internal class FileHelper
  {
    /// <summary>
    /// The logger that we will be using to log messages.
    /// </summary>
    private readonly ILogger _logger;

    public FileHelper(ILogger logger)
    {
      // save the logger
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Check if a given file system is a file or a directory.
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    protected static bool IsDirectory(FileSystemInfo file)
    {
      try
      {
        // get the file attributes for file or directory
        if (Directory.Exists(file.FullName))
        {
          return true;
        }

        return !File.Exists(file.FullName);
      }
      catch (SecurityException)
      {
        return false;
      }
      catch (UnauthorizedAccessException)
      {
        return false;
      }
      catch (FileNotFoundException)
      {
        return false;
      }
    }

    protected bool CanReadFile(FileSystemInfo file)
    {
      try
      {
        File.Open(file.FullName, FileMode.Open, FileAccess.Read).Dispose();
        return true;
      }
      catch (IOException)
      {
        return false;
      }
      catch (SecurityException)
      {
        return false;
      }
      catch (UnauthorizedAccessException)
      {
        return false;
      }
    }

    /// <summary>
    /// Check if we are able to process this directlry.
    /// </summary>
    /// <param name="directoryInfo"></param>
    /// <returns></returns>
    protected bool CanReadDirectory(DirectoryInfo directoryInfo)
    {
      try
      {
        if (!directoryInfo.Exists)
        {
          return false;
        }
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
  }
}
