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
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace myoddweb.desktopsearch.Helpers
{
  internal static class Shell
  {
    [DllImport("shell32.dll", EntryPoint = "SHOpenWithDialog", CharSet = CharSet.Unicode)]
    private static extern int SHOpenWithDialog(IntPtr hWndParent, ref TagOpenasinfo oOai);

#pragma warning disable 414
    // http://msdn.microsoft.com/en-us/library/windows/desktop/bb773363(v=vs.85).aspx 
    private struct TagOpenasinfo
    {
      [MarshalAs(UnmanagedType.LPWStr)]
      public string CszFile;

      [MarshalAs(UnmanagedType.LPWStr)]
      public string CszClass;

      [MarshalAs(UnmanagedType.I4)]
      public TagOpenAsInfoFlags OaifInFlags;
    }
#pragma warning restore 414

    [Flags]
    private enum TagOpenAsInfoFlags
    {
      OaifAllowRegistration = 0x00000001,   // Show "Always" checkbox
      //      OaifRegisterExt = 0x00000002,   // Perform registration when user hits OK
      OaifExec = 0x00000004,   // Exec file after registering
      //      OaifForceRegistration = 0x00000008,   // Force the checkbox to be registration
      //      OaifHideRegistration = 0x00000020,   // Vista+: Hide the "always use this file" checkbox
      //      OaifUrlProtocol = 0x00000040,   // Vista+: cszFile is actually a URI scheme; show handlers for that scheme
      //      OaifFileIsUri = 0x00000080    // Win8+: The location pointed to by the pcszFile parameter is given as a URI
    }

    public static void OpenWith( string sFilename )
    {
      var hwndParent = Process.GetCurrentProcess().MainWindowHandle;
      var oOai = new TagOpenasinfo
      {
        CszFile = sFilename,
        CszClass = string.Empty,
        OaifInFlags = TagOpenAsInfoFlags.OaifAllowRegistration | TagOpenAsInfoFlags.OaifExec
      };
      SHOpenWithDialog(hwndParent, ref oOai);
    }
  }
}
