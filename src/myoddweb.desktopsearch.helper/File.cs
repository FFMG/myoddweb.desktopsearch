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
using System.IO;
using System.Linq;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;
using myoddweb.desktopsearch.helper.IO;
using myoddweb.desktopsearch.interfaces.Logging;

namespace myoddweb.desktopsearch.helper
{
  public static class File
  {
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

    /// <summary>
    /// Check if a file can be read by this process
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    public static bool CanReadFile(FileInfo file)
    {
      try
      {
        if (file == null )
        {
          return false;
        }
        if (!file.Exists)
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

    /// <summary>
    /// Check if we are able to read an item given the access rules.
    /// </summary>
    /// <param name="accessRules"></param>
    /// <returns></returns>
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
      if (null == child)
      {
        throw new ArgumentNullException(nameof(child));
      }
      var diChild = DirectoryInfo(child, null);
      if (null == diChild)
      {
        throw new ArgumentNullException(nameof(child));
      }
      foreach (var parent in parents)
      {
        if (null == parent)
        {
          throw new ArgumentNullException(nameof(child));
        }
        var diParent = DirectoryInfo(parent, null);
        if (null == diParent)
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
      #region Sanity Check
      if (null == parent)
      {
        throw new ArgumentNullException(nameof(parent));
      }
      if (null == child)
      {
        throw new ArgumentNullException(nameof(child));
      }
      #endregion

      #region Fast Checks
      // "c:\foo\bar" is a child of "c:\foo"
      var pFullName = DirectoryInfoComparer.FullName(parent);
      var cFullName = DirectoryInfoComparer.FullName(child);

      // if the parent is longer than the child
      // then it cannot posibly be a child.
      if (pFullName.Length > cFullName.Length)
      {
        return false;
      }

      // are they equal?
      if (pFullName.Length == cFullName.Length)
      {
        return Equals(parent, child);
      }

      // if the root paths are not the same... then don't do it.
      if (string.Compare(Path.GetPathRoot(pFullName), Path.GetPathRoot(cFullName), StringComparison.OrdinalIgnoreCase) != 0)
      {
        return false;
      }
      #endregion

      //  is the child a substring of the parent...
      return cFullName.ToUpper().Contains(pFullName.ToUpper());
    }

    /// <summary>
    /// Check if 2 directories are equal
    /// </summary>
    /// <param name="diA"></param>
    /// <param name="diB"></param>
    /// <returns></returns>
    public static bool Equals<T>( T diA, T diB) where T : FileSystemInfo
    {
      var fsic = new FileSystemInfoComparer<T>();
      return fsic.Equals(diA, diB);
    }

    /// <summary>
    /// Get the union of A and B
    /// Basically A+B without dublicates.
    /// </summary>
    /// <param name="fisA"></param>
    /// <param name="fisB"></param>
    /// <returns></returns>
    public static List<FileInfo> Union(List<FileInfo> fisA, List<FileInfo> fisB)
    {
      // if they are both null then the union has to be an empty list.
      if (fisA == null && fisB == null )
      {
        return new List<FileInfo>();
      }

      // if A is null then B is not
      // the union of both has to be B
      if (fisA == null)
      {
        return fisB;
      }

      // if B is null then A is not
      // the union of both has to be A
      if (fisB == null)
      {
        return fisA;
      }

      // make a copy of it so we do not change fisA
      var fisUnion = fisA.Select( fi => fi ).ToList();
      foreach (var fiB in fisB)
      {
        if (fisUnion.Any(fi => fiB.FullName == fi.FullName))
        {
          continue;
        }
        fisUnion.Add(fiB);
      }

      // return the newly created list
      return fisUnion;
    }

    /// <summary>
    /// Get the intersection of A and B
    /// Those are the files that are in both B and A
    /// </summary>
    /// <param name="fisA"></param>
    /// <param name="fisB"></param>
    /// <returns></returns>
    public static List<FileInfo> Intersection(List<FileInfo> fisA, List<FileInfo> fisB)
    {
      // if either A or B are null then it does not matter what is in the 
      // other collection as the first one has to be empty.
      if (fisA == null || fisB == null)
      {
        return new List<FileInfo>();
      }

      // start with an empty list
      var fisIntersection = new List<FileInfo>();
      foreach (var fiB in fisB)
      {
        // if this file is never in B then there is no intersection.
        if (fisA.All(fi => fiB.FullName != fi.FullName))
        {
          continue;
        }
        fisIntersection.Add(fiB);
      }

      // return the newly created list
      return Distinct(fisIntersection);
    }

    /// <summary>
    /// Remove duplicates in file info
    /// </summary>
    /// <param name="fis"></param>
    /// <returns></returns>
    public static List<FileInfo> Distinct(List<FileInfo> fis)
    {
      return fis.GroupBy(fi => fi.FullName).Select(fi => fi.First()).ToList();
    }

    /// <summary>
    /// Get the relative complement
    /// Return the list of elements that are in B but not in A
    /// </summary>
    /// <param name="fisA"></param>
    /// <param name="fisB"></param>
    /// <returns></returns>
    public static List<FileInfo> RelativeComplement(List<FileInfo> fisA, List<FileInfo> fisB)
    {
      // if they are both empty then the complement is nothing.
      if (null == fisA && null == fisB)
      {
        return new List<FileInfo>();
      }

      // if we have a null A then everything in B is the relative
      // complement as they never intercet.
      if (null == fisA || !fisA.Any())
      {
        return Distinct(fisB ?? new List<FileInfo>());
      }

      // If B is empty then it will never intercet with A
      // so there is no complement values.
      if (null == fisB || !fisB.Any())
      {
        return new List<FileInfo>();
      }

      // we can now go around B and find all the ones that are _not_ in A
      // A = {2,3,4}
      // B = {3,4,5}
      // RC = {5}
      var fc = new FileInfoComparer();
      var fisRelativeComplement = new HashSet<FileInfo>( fc );

      // convert to a hashSet, it is a lot faster than a List<>.Contains( ... ) ...
      // even if all we are doing is converting it back to a lis.t
      var dicA = new HashSet<FileInfo>( fisA, fc );
      foreach (var fi in fisB)
      {
        if (dicA.Contains(fi))
        {
          continue;
        }
        fisRelativeComplement.Add(fi);
      }

      // convert it to a list
      // we know it is Distinct as it is a HashSet<>()
      return fisRelativeComplement.ToList();
    }

    /// <summary>
    /// Check if a given file has the one extension
    /// </summary>
    /// <param name="file"></param>
    /// <param name="ext"></param>
    /// <returns></returns>
    public static bool IsExtension(FileInfo file, string ext)
    {
      return IsExtension(file, new[] {ext});
    }

    /// <summary>
    /// Check if a file has an extension.
    /// </summary>
    /// <param name="file"></param>
    /// <param name="exts"></param>
    /// <returns></returns>
    public static bool IsExtension(FileInfo file, string[] exts)
    {
      // get the file name
      var name = file?.Name.ToLowerInvariant() ?? throw new ArgumentNullException( nameof(file));

      // then compare all the extensions.
      foreach (var ext in exts)
      {
        if (ext.Length == 0 )
        {
          continue;
        }

        var e = ext.ToLowerInvariant();
        if (e[0] != '.')
        {
          // in the case when name is 'c:\\bin' and the ext is 'bin'
          if (e == name)
          {
            continue;
          }
          e = $".{e}";
        }

        // in the case when name is 'c:\\.bin' and the ext is '.bin'
        if (e == name)
        {
          continue;
        }

        if (name.EndsWith(e))
        {
          return true;
        }
      }

      // if we made it here, it is not one of those extensions
      return false;
    }
  }
}

