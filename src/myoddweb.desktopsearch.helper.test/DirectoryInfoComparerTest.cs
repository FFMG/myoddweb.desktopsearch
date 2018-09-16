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
using myoddweb.desktopsearch.helper.IO;
using NUnit.Framework;

namespace myoddweb.desktopsearch.parser.test
{
  internal class DirectoryInfoComparerTest
  {
    [TestCase("c:\\dir", "c:\\dir\\")]
    [TestCase("c:\\//dir", "c:\\dir\\")]
    [TestCase("c:/dir", "c:\\dir\\")]
    [TestCase("c:\\dir\\", "c:\\dir\\")]
    [TestCase("c:\\dir/", "c:\\dir\\")]
    [TestCase("c:\\dir//", "c:\\dir\\")]
    [TestCase("c:\\dir\\", "c:\\dir\\")]
    public void FullNameIsProperlyChanged(string lhs, string rhs)
    {
      Assert.AreEqual(
        DirectoryInfoComparer.FullName(new DirectoryInfo(lhs)), rhs);
    }

    [TestCase("c:\\dir", "c:\\dir\\")]
    [TestCase("c:\\//dir", "c:\\dir\\")]
    [TestCase("c:/dir", "c:\\dir\\")]
    [TestCase("c:\\dir\\", "c:\\dir\\")]
    [TestCase("c:\\dir/", "c:\\dir\\")]
    [TestCase("c:\\dir//", "c:\\dir\\")]
    [TestCase("c:\\dir\\", "c:\\dir\\")]
    public void CheckAreEqual(string lhs, string rhs)
    {
      var fic = new DirectoryInfoComparer();
      Assert.IsTrue(
        fic.Equals(new DirectoryInfo(lhs), new DirectoryInfo(rhs)));
    }

    [Test]
    public void CheckNullAreEqual()
    {
      var fic = new DirectoryInfoComparer();
      Assert.IsTrue(
        fic.Equals(null, null));
    }

    [Test]
    public void CheckNonNullAreEqual()
    {
      var fic = new DirectoryInfoComparer();
      Assert.IsFalse(
        fic.Equals(null, new DirectoryInfo("c:\\dir" )));
    }

    [Test]
    public void DictionaryKeys()
    {
      const string name = "c:\\test";

      var dic = new DirectoryInfoComparer();
      var dict = new Dictionary<DirectoryInfo, object>(dic);
      var info = new DirectoryInfo(name);
      dict[info] = null;
      Assert.IsTrue(dict.ContainsKey(info));
      Assert.IsTrue(dict.ContainsKey(new DirectoryInfo(name)));
    }

    [Test]
    public void DictionaryKeysUpperCase()
    {
      const string name = "c:\\test";
      var nameU = name.ToUpper();

      var dic = new DirectoryInfoComparer();
      var dict = new Dictionary<DirectoryInfo, object>(dic);
      var info = new DirectoryInfo(name);
      dict[info] = null;
      Assert.IsTrue(dict.ContainsKey(info));
      Assert.IsTrue(dict.ContainsKey(new DirectoryInfo(name)));
      Assert.IsTrue(dict.ContainsKey(new DirectoryInfo(nameU)));
    }

    [TestCase("C:\\\\tESt")]
    [TestCase("c:\\\\test")]
    [TestCase("c:\\/test")]
    [TestCase("c://test")]
    [TestCase("c:/test")]
    [TestCase("C:\\\\tESt/")]
    [TestCase("c:\\\\test/")]
    [TestCase("c:\\/test/")]
    [TestCase("c://test/")]
    [TestCase("c:/test/")]
    [TestCase("C:\\\\tESt\\")]
    [TestCase("c:\\\\test\\")]
    [TestCase("c:\\/test\\")]
    [TestCase("c://test\\")]
    [TestCase("c:/test\\")]
    public void DictionaryKeysVariousCases(string given)
    {
      const string name = "c:\\test";

      var dic = new DirectoryInfoComparer();
      var dict = new Dictionary<DirectoryInfo, object>(dic);
      var info = new DirectoryInfo(name);
      dict[info] = null;
      Assert.IsTrue(dict.ContainsKey(info));
      Assert.IsTrue(dict.ContainsKey(new DirectoryInfo(given)));
    }

    [Test]
    public void FulleNameCannotBeNull()
    {
      Assert.Throws<ArgumentNullException>(() => DirectoryInfoComparer.FullName(null));
    }

    [TestCase("\\\\blah\\test", "\\\\blah\\test\\")]  //  network
    [TestCase("\\\\blah\\test\\", "\\\\blah\\test\\")]  //  network
    [TestCase("\\\\blah\\test/", "\\\\blah\\test\\")]  //  network
    [TestCase("c:\\test", "c:\\test\\")]
    [TestCase("c:/test", "c:\\test\\")]
    [TestCase("c:\\\\\\test\\", "c:\\test\\")]
    [TestCase("c:/\\/test", "c:\\test\\")]
    public void FullName(string given, string expected)
    {
      Assert.AreEqual(expected, DirectoryInfoComparer.FullName(new DirectoryInfo(given)));
    }

    [TestCase("c:\\test\\blah", "c:\\test\\blah\\")]
    [TestCase("c:\\TEST\\BLAH", "c:\\TEST\\BLAH\\")]
    [TestCase("c:\\tESt\\", "c:\\tESt\\")]
    public void FullNameCaseAreKept(string given, string expected)
    {
      Assert.AreEqual(expected, DirectoryInfoComparer.FullName(new DirectoryInfo(given)));
    }

    [Test]
    public void HashCodeCaseInsensitive()
    {
      const string name = "c:\\hello\\world\\blah\\";
      var dic = new DirectoryInfoComparer();
      var expected = dic.GetHashCode(new DirectoryInfo(name));
      Assert.AreEqual(expected, dic.GetHashCode(new DirectoryInfo(name.ToUpper())));
    }
  }
}
