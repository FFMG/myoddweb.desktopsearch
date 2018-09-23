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
using myoddweb.desktopsearch.interfaces.Configs;

namespace myoddweb.desktopsearch.service.IO
{
  internal class IgnoreFile : IIgnoreFile
  {
    /// <inheritdoc/>
    public string Pattern { get; }

    /// <inheritdoc/>
    public long MaxSizeMegabytes { get; }

    /// The converted megabyte size if Bytes
    public long MaxSizeBytes { get; }

    public IgnoreFile(string pattern, long maxSizeMegabytes)
    {
      Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
      if (maxSizeMegabytes < 0)
      {
        throw new ArgumentNullException( $"The max size in megabytes, ({maxSizeMegabytes}), cannot less than zero.");
      }
      MaxSizeMegabytes = maxSizeMegabytes;
      MaxSizeBytes = ConvertMegabytesToBytes(maxSizeMegabytes);
    }

    /// <inheritdoc/>
    public bool Match(FileInfo file)
    {
      try
      {
        // Check the required size first
        // this helps preventing to open file to get the length
        if (MaxSizeBytes > 0)
        {
          if (file.Length < MaxSizeBytes )
          {
            return false;
          }
        }
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
        // if we get an exception then the file might no longer exit.
        // in that case ... it is not a match.
        return false;
      }

      // check the pattern
      return helper.File.NameMatch(file, Pattern);
    }

    /// <summary>
    /// Convert megabytes to bytes.
    /// </summary>
    /// <param name="megabytes"></param>
    /// <returns></returns>
    private static long ConvertMegabytesToBytes(long megabytes )
    {
      return megabytes * (1024 * 1024);
    }
  }
}
