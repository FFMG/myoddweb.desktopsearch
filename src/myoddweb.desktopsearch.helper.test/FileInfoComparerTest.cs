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

using System.Collections.Generic;
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

    [Test]
    public void CheckNullAreEqual()
    {
      var fic = new FileInfoComparer();
      Assert.IsTrue(
        fic.Equals(null, null));
    }

    [Test]
    public void CheckNonNullAreEqual()
    {
      var fic = new FileInfoComparer();
      Assert.IsFalse(
        fic.Equals(null, new FileInfo("c:\\dir\\file.txt")));
    }

    [Test]
    public void DictionaryKeys()
    {
      const string name = "c:\\test.txt";

      var fic = new FileInfoComparer();
      var dict = new Dictionary<FileInfo, object>(fic);
      var info = new FileInfo(name);
      dict[info] = null;
      Assert.IsTrue(dict.ContainsKey(info));
      Assert.IsTrue(dict.ContainsKey(new FileInfo(name)));
    }

    [Test]
    public void DictionaryKeysUpperCase()
    {
      const string name = "c:\\test.txt";
      var nameU = name.ToUpper();

      var fic = new FileInfoComparer();
      var dict = new Dictionary<FileInfo, object>( fic );
      var info = new FileInfo( name );
      dict[info] = null;
      Assert.IsTrue(dict.ContainsKey(info));
      Assert.IsTrue(dict.ContainsKey(new FileInfo(name)));
      Assert.IsTrue(dict.ContainsKey(new FileInfo(nameU)));
    }

    [TestCase("C:\\\\tESt.txt")]
    [TestCase("c:\\\\test.TXT")]
    [TestCase("c:\\/test.txt")]
    [TestCase("c://test.txt")]
    [TestCase("c:/test.txt")]
    public void DictionaryKeysVariousCases( string given)
    {
      const string name = "c:\\test.txt";

      var fic = new FileInfoComparer();
      var dict = new Dictionary<FileInfo, object>(fic);
      var info = new FileInfo(name);
      dict[info] = null;
      Assert.IsTrue(dict.ContainsKey(info));
      Assert.IsTrue(dict.ContainsKey(new FileInfo(given)));
    }
  }
}
