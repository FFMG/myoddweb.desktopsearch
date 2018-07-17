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
using NUnit.Framework;
using File = myoddweb.desktopsearch.helper.File;

namespace myoddweb.desktopsearch.parser.test.Helper
{
  [TestFixture]
  internal class FileTests
  {
    [TestCase("c:/", "d:/")]
    [TestCase("c:\\", "d:\\")]
    [TestCase("c:\\Test", "d:\\Test")]
    [TestCase("c:\\Test\\", "d:\\Test\\")]
    [TestCase("c:/", "d:/")]
    [TestCase("c:/Test", "d:/Test")]
    [TestCase("c:/Test/", "d:/Test/")]
    public void CheckRootIsNotSame( string parent, string path )
    {
      Assert.IsFalse(
        File.IsSubDirectory(parent, path )
      );
    }

    [TestCase("c:/", "c:/")]
    [TestCase("D:/", "D:/")]
    [TestCase("c:/", "C:/")]
    [TestCase("C:/", "c:/")]
    [TestCase("c:\\", "c:\\")]
    public void CheckIsSubDirectorySameRootDirectory(string parent, string path)
    {
      Assert.IsTrue(
        File.IsSubDirectory(parent, path)
      );
    }

    [TestCase("c:\\PARENT\\", "c:\\Parent\\CHILD")]
    [TestCase("c:\\Parent\\", "c:\\PARent\\CHILD")]
    [TestCase("c:\\Parent\\", "c:\\Parent\\Child")]
    [TestCase("c:\\Parent\\", "c:\\Parent\\Child1\\Child2")]
    [TestCase("c:\\Parent\\", "c:\\Parent\\Child\\")]
    [TestCase("c:\\Parent\\", "c:\\Parent\\Child1\\Child2\\")]
    [TestCase("c:/Parent/", "c:\\Parent\\Child\\")]
    [TestCase("c:\\Parent\\", "c:/Parent/Child1/Child2/")]
    public void CheckIsChildOfParent(string parent, string path)
    {
      Assert.IsTrue(
        File.IsSubDirectory(parent, path)
      );
    }

    [TestCase("c:\\Parent\\", "c:\\Parent\\Child\\..\\")]
    public void CheckSpecialCases(string parent, string path)
    {
      Assert.IsTrue(
        File.IsSubDirectory(parent, path)
      );
    }

    [TestCase("c:\\Parent\\", "c:\\Parent\\\\Child\\\\..\\\\")]
    [TestCase("c:\\Parent\\", "c:\\Parent\\Child\\..\\")]
    [TestCase("c:\\Parent\\", "c:\\Bad\\..\\Parent\\Child\\..\\")]
    public void CheckSpecialCasesIsChild(string parent, string path)
    {
      Assert.IsTrue(
        File.IsSubDirectory(parent, path)
      );
    }

    [TestCase("c:\\Parent\\", "c:\\Parent\\Child\\..\\..\\")]
    [TestCase("c:\\Parent\\", "c:\\Parent\\Child\\..\\..\\Bad")]

    public void CheckSpecialCasesNotChild(string parent, string path)
    {
      Assert.IsFalse(
        File.IsSubDirectory(parent, path)
      );
    }

    [Test]
    public void ParentStringCannotBeNull()
    {
      Assert.Throws<ArgumentNullException>( () => File.IsSubDirectory( (string)null, "c:\\"));
    }

    [Test]
    public void ParentDirectoryInfoCannotBeNull()
    {
      Assert.Throws<ArgumentNullException>(() => File.IsSubDirectory((DirectoryInfo)null, new DirectoryInfo("c:\\")));
    }

    [Test]
    public void ChildStringCannotBeNull()
    {
      Assert.Throws<ArgumentNullException>(() => File.IsSubDirectory("c:\\", null));
    }

    [Test]
    public void ChildDirectoryInfoCannotBeNull()
    {
      Assert.Throws<ArgumentNullException>(() => File.IsSubDirectory(new DirectoryInfo("c:\\"), null));
    }

    [Test]
    public void IsChildOfMultipleParentsWithEmptyList()
    {
      Assert.IsFalse(
        File.IsSubDirectory(new List<string>(), "c:\\")
      );
    }

    [Test]
    public void IsChildOfMultipleParentsWithNullList()
    {
      Assert.Throws<ArgumentNullException>(() => 
        File.IsSubDirectory((List<string>)(null), "c:\\")
      );
    }

    [Test]
    public void IsChildOfMultipleParentsWithNullItemInList()
    {
      Assert.Throws<ArgumentNullException>(() =>
        File.IsSubDirectory(new List<string> {"d:\\", null}, "c:\\"));
    }

    [Test]
    public void IsNotChildOfMultipleParents()
    {
      Assert.IsFalse(
        File.IsSubDirectory( new List<string>
        {
          "D:\\A\\B\\",
          "c:\\A\\B\\C\\"
        }, "c:\\A\\B\\")
      );
    }

    [Test]
    public void IsChildOfMultipleParents()
    {
      Assert.IsTrue(
        File.IsSubDirectory(new List<string>
        {
          "c:\\A\\",
          "c:\\A\\B\\C\\"
        }, "c:\\A\\B\\")
      );
    }
  }
}
