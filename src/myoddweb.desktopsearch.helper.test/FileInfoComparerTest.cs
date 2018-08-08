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
using System.IO;
using myoddweb.desktopsearch.helper.IO;
using NUnit.Framework;

namespace myoddweb.desktopsearch.parser.test
{
  internal class FileInfoComparerTest
  {
    [TestCase("c:\\dir\\file.txt", "c:\\dir\\file.txt")]
    [TestCase("c:\\//dir\\\\file.txt", "c:\\dir\\file.txt")]
    [TestCase("c:/dir\\\\\\file.txt", "c:\\dir\\file.txt")]
    [TestCase("c:\\dir\\file.txt", "c:\\dir\\file.txt")]
    [TestCase("c:\\dir/\\file.txt", "c:\\dir\\file.txt")]
    [TestCase("c:\\dir//file.txt", "c:\\dir\\file.txt")]
    [TestCase("c:\\dir\\file.txt", "c:\\dir\\file.txt")]
    public void FullNameIsProperlyChanged(string lhs, string rhs)
    {
      Assert.AreEqual(
        FileInfoComparer.FullName(new FileInfo(lhs)), rhs);
    }

    [TestCase("c:\\dir\\file.txt", "c:\\dir\\file.txt")]
    [TestCase("c:\\//dir\\file.txt", "c:\\dir\\file.txt")]
    [TestCase("c:/dir\\file.txt", "c:\\dir\\file.txt")]
    [TestCase("c:\\dir\\file.txt", "c:\\dir\\file.txt")]
    [TestCase("c:\\dir/file.txt", "c:\\dir\\file.txt")]
    [TestCase("c:\\dir//file.txt", "c:\\dir\\file.txt")]
    [TestCase("c:\\dir\\file.txt", "c:\\dir\\file.txt")]
    public void CheckAreEqual(string lhs, string rhs)
    {
      var fic = new FileInfoComparer();
      Assert.IsTrue(
        fic.Equals(new FileInfo(lhs), new FileInfo(rhs)));
    }

    public void CheckNullAreEqual()
    {
      var fic = new FileInfoComparer();
      Assert.IsTrue(
        fic.Equals(null, null));
    }

    public void CheckNonNullAreEqual()
    {
      var fic = new FileInfoComparer();
      Assert.IsFalse(
        fic.Equals(null, new FileInfo("c:\\dir\\file.txt")));
    }
  }
}
