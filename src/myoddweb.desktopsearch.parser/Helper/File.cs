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

    /// <summary>
    /// If the given path is the child of any given parent directories.
    /// </summary>
    /// <param name="parents"></param>
    /// <param name="child"></param>
    /// <returns></returns>
    public static bool IsSubDirectory(List<string> parents, string child)
    {
      if (null == parents)
      {
        throw new ArgumentNullException(nameof(parents));
      }
      foreach (var parent in parents)
      {
        if (IsSubDirectory( new DirectoryInfo(parent), new DirectoryInfo(child) ))
        {
          return true;
        }
      }
      return false;
    }

    /// <summary>
    /// If the given path is the child of any given parent directories.
    /// </summary>
    /// <param name="parents"></param>
    /// <param name="child"></param>
    /// <returns></returns>
    public static bool IsSubDirectory(IEnumerable<DirectoryInfo> parents, DirectoryInfo child)
    {
      if (null == parents)
      {
        throw new ArgumentNullException(nameof(parents));
      }
      foreach (var parent in parents)
      {
        if (IsSubDirectory( parent, child))
        {
          return true;
        }
      }
      return false;
    }

    /// <summary>
    /// Check if a directory is a child of the parent
    /// </summary>
    /// <param name="parent"></param>
    /// <param name="child"></param>
    /// <returns></returns>
    public static bool IsSubDirectory(string parent, string child)
    {
      if (null == parent)
      {
        throw new ArgumentNullException( nameof(parent));
      }
      if (null == child)
      {
        throw new ArgumentNullException(nameof(child));
      }
      var parentDirectory = new DirectoryInfo(parent);
      var childDirectory = new DirectoryInfo(child);
      return IsSubDirectory(parentDirectory, childDirectory);
    }

    /// <summary>
    /// Check if a directory is a child of the parent
    /// </summary>
    /// <param name="parent"></param>
    /// <param name="child"></param>
    /// <returns></returns>
    public static bool IsSubDirectory(DirectoryInfo parent, DirectoryInfo child)
    {
      if (null == parent)
      {
        throw new ArgumentNullException(nameof(parent));
      }
      if (null == child)
      {
        throw new ArgumentNullException(nameof(child));
      }
      var pFullName = parent.FullName.TrimEnd('\\', '/');
      var cFullName = child.FullName.TrimEnd('\\', '/');
      if (string.Equals(pFullName, cFullName, StringComparison.OrdinalIgnoreCase))
      {
        return true;
      }

      if ( child.Parent != null && IsSubDirectory(parent, child.Parent))
      {
        return true;
      }

      // if we made it this far, it is not the same.
      return false;
    }
  }
}
