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
using myoddweb.desktopsearch.interfaces.Configs;

namespace myoddweb.desktopsearch.helper.IO
{
  public class Paths
  {
    /// <summary>
    /// Get all the paths we want to ignore.
    /// </summary>
    /// <returns></returns>
    public static IReadOnlyCollection<DirectoryInfo> GetIgnorePaths( List<string> paths, interfaces.Logging.ILogger logger )
    {
      var ignorePaths = new List<DirectoryInfo>( paths.Count );

      // get all the paths we want to ignore.
      foreach (var path in paths)
      {
        try
        {
          var ePath = Environment.ExpandEnvironmentVariables(path);
          var fullPath =  Path.GetFullPath(ePath);
          ignorePaths.Add(new DirectoryInfo(fullPath));
        }
        catch (Exception e)
        {
          logger.Exception(e);
        }
      }
      return ignorePaths.Distinct( new DirectoryInfoComparer() ).ToList();
    }

    /// <summary>
    /// Get all the start paths so we can monitor them.
    /// As well as parse them all.
    /// </summary>
    /// <returns></returns>
    public static IReadOnlyCollection<DirectoryInfo> GetStartPaths(IPaths paths)
    {
      var drvs = DriveInfo.GetDrives();
      var startPaths = new List<DirectoryInfo>();
      foreach (var drv in drvs)
      {
        switch (drv.DriveType)
        {
          case DriveType.Fixed:
            if (paths.ParseFixedDrives)
            {
              startPaths.Add(new DirectoryInfo(drv.Name));
            }
            break;

          case DriveType.Removable:
            if (paths.ParseRemovableDrives)
            {
              startPaths.Add(new DirectoryInfo(drv.Name));
            }
            break;
        }
      }

      // then we try and add the folders as given by the user
      // but if the ones given by the user is a child of the ones 
      // we already have, then there is no point in adding it.
      foreach (var path in paths.Paths)
      {
        if (!File.IsSubDirectory(startPaths, new DirectoryInfo(path)))
        {
          startPaths.Add(new DirectoryInfo(path));
        }
      }

      // This is the list of all the paths we want to parse.
      return startPaths;
    }
  }
}
