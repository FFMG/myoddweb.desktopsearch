using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;
using myoddweb.desktopsearch.interfaces.Logging;

namespace myoddweb.desktopsearch.helper
{
  public class File
  {
    private File()
    {
    }

    /// <summary>
    /// Safely create a File info
    /// </summary>
    /// <param name="path"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    public static FileInfo FileInfo(string path, ILogger logger )
    {
      try
      {
        return new FileInfo(path);
      }
      catch (Exception e)
      {
        logger?.Exception(e);
        return null;
      }
    }

    /// <summary>
    /// Safely create a Directory info
    /// </summary>
    /// <param name="path"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    public static DirectoryInfo DirectoryInfo(string path, ILogger logger)
    {
      try
      {
        return new DirectoryInfo(path);
      }
      catch (Exception e)
      {
        logger?.Exception(e);
        return null;
      }
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

    public static bool CanReadFile(FileInfo file)
    {
      try
      {
        if (file == null )
        {
          return false;
        }
        if (file.Exists)
        {
          return false;
        }
        var accessControlList = file.GetAccessControl();
        var accessRules = accessControlList?.GetAccessRules(true, true, typeof(SecurityIdentifier));
        return CanRead(accessRules);
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
      catch (Exception)
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
        if (null == directoryInfo)
        {
          return false;
        }

        if (!directoryInfo.Exists)
        {
          return false;
        }

        var accessControlList = directoryInfo.GetAccessControl();
        var accessRules = accessControlList?.GetAccessRules(true, true, typeof(SecurityIdentifier));
        return CanRead(accessRules);
      }
      catch (IOException)
      {
        return false;
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

    private static bool CanRead(AuthorizationRuleCollection accessRules)
    {
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
        var diParent = DirectoryInfo(parent, null);
        if (null == diParent)
        {
          continue;
        }
        var diChild = DirectoryInfo(parent, null);
        if (null == diChild)
        {
          continue;
        }
        if (IsSubDirectory( diParent, diChild ))
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

      // if the directory is a child of any of the parents 
      return parents.Any(parent => IsSubDirectory(parent, child));
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
      var diParent = DirectoryInfo(parent, null );
      if (null == diParent)
      {
        return false;
      }

      var diChild = DirectoryInfo(child, null );
      if (null == diChild)
      {
        return false;
      }

      return IsSubDirectory(diParent, diChild );
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
