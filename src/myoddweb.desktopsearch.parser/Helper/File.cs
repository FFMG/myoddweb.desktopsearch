using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Security.AccessControl;

namespace myoddweb.desktopsearch.parser.Helper
{
  internal class File
  {
    private File()
    {
    }

    /// <summary>
    /// Check if a given file system is a file or a directory.
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    public static bool IsDirectory(FileSystemInfo file)
    {
      try
      {
        // get the file attributes for file or directory
        if (Directory.Exists(file.FullName))
        {
          return true;
        }

        return !System.IO.File.Exists(file.FullName);
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

    public static bool CanReadFile(FileSystemInfo file)
    {
      try
      {
        System.IO.File.Open(file.FullName, FileMode.Open, FileAccess.Read).Dispose();
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
    public static bool CanReadDirectory(DirectoryInfo directoryInfo)
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
      catch (Exception)
      {
        return false;
      }
    }

    public static bool IsSubDirectory(List<string> parentPaths, string path)
    {
      foreach (var parentPath in parentPaths)
      {
        if (IsSubDirectory(parentPath, path))
        {
          return true;
        }
      }
      return false;
    }

    public static bool IsSubDirectory(string parentPath, string path)
    { 
      var parentDirectory = new DirectoryInfo(Path.GetDirectoryName(parentPath) ?? throw new InvalidOperationException());
      var childDirectory = new DirectoryInfo(Path.GetDirectoryName(path) ?? throw new InvalidOperationException());

      
      return false;
    }
  }
}
