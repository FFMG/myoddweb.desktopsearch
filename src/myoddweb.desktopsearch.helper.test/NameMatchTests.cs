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
using NUnit.Framework;
using File = myoddweb.desktopsearch.helper.File;

namespace myoddweb.desktopsearch.parser.test
{
  [TestFixture]
  internal class NameMatchTests
  {
    [Test]
    public void FileCannotBeNull()
    {
      Assert.Throws<ArgumentNullException>(() => File.NameMatch(null, "*.*"));
    }

    [Test]
    public void NameCannotBeNull()
    {
      Assert.Throws<ArgumentNullException>(() => File.NameMatch( new FileInfo( "c:\\text.txt"), null ));
    }

    [Test]
    public void NameCannExactMatchPatternSimple()
    {
      var f = new FileInfo( $"c:\\{Guid.NewGuid().ToString()}.{Guid.NewGuid().ToString()}");
      Assert.IsTrue( File.NameMatch(f, "*"));
    }


    [Test]
    public void NameCannExactMatchPatternDoubleStar()
    {
      var name = $"{Guid.NewGuid().ToString()}.{Guid.NewGuid().ToString()}";
      var f = new FileInfo($"c:\\directory\\{name}");
      Assert.IsTrue(File.NameMatch(f, "*.*"));
    }

    [Test]
    public void ExactMatchIgnoreCase()
    {
      const string name = "FiLeNamE.tXT";
      var f = new FileInfo( $"c:\\directory\\{name}");
      Assert.IsTrue(File.NameMatch(f, name ));
      Assert.IsTrue(File.NameMatch(f, name.ToLower()));
      Assert.IsTrue(File.NameMatch(f, name.ToUpper()));
    }

    [Test]
    public void NotEqual()
    {
      const string name1 = "FiLeNamE.tXT";
      const string name2 = "FiLeNamF.tXT";
      var f = new FileInfo($"c:\\directory\\{name1}");
      Assert.IsFalse(File.NameMatch(f, name2));
    }

    [Test]
    public void NotEqualDifferentLenght()
    {
      const string name1 = "FiLeNamEE.tXT";
      const string name2 = "FiLeNamE.tXT";
      var f = new FileInfo($"c:\\directory\\{name1}");
      Assert.IsFalse(File.NameMatch(f, name2));
    }

    [Test]
    public void EqualWithQuestionMark()
    {
      const string name1 = "FiLeNamEE.tXT";
      const string name2 = "FiLeNamE?.tXT";
      var f = new FileInfo($"c:\\directory\\{name1}");
      Assert.IsTrue(File.NameMatch(f, name2));
      Assert.IsTrue(File.NameMatch(f, name2.ToLower()));
      Assert.IsTrue(File.NameMatch(f, name2.ToUpper()));
    }

    [TestCase("file.txt", "file.**")]
    [TestCase("file.txt", "**.txt")]
    [TestCase("file.txt", "*.txt")]
    [TestCase("file.txt", "*.tx?")]
    [TestCase("file.txt", "????.txt")]
    [TestCase("file.txt", "f???.txt")]
    [TestCase("file.txt", "F???.txt")]
    [TestCase("FILE.txt", "f???.txt")]
    [TestCase("file.txt", "*.??t")]
    [TestCase("abc.doc", "*.?oc")]
    [TestCase("abc.doc", "*?.?oc")]
    public void CompareExtensions( string name, string pattern)
    {
      var f = new FileInfo($"c:\\directory\\{name}");
      Assert.IsTrue(File.NameMatch(f, pattern));
    }

    [TestCase("file.txt", "*.txtt")]
    [TestCase("file.txt", "???.txt")]
    [TestCase("file.txt", "???f.*")]
    public void NoMatch(string name, string pattern)
    {
      var f = new FileInfo($"c:\\directory\\{name}");
      Assert.IsFalse(File.NameMatch(f, pattern));
    }

    [Test]
    public void EmptyPaternNeverMatches()
    {
      var f = new FileInfo("c:\\directory\\blah.txt");
      Assert.IsFalse(File.NameMatch(f, ""));
    }

    [Test]
    public void SpacesAreNotIgnoredWithStar()
    {
      var f = new FileInfo("c:\\directory\\blah blah.txt");
      Assert.IsTrue(File.NameMatch(f, "* blah.txt"));
    }

    [Test]
    public void SpacesAreNotIgnored()
    {
      var f = new FileInfo("c:\\directory\\blah blah.txt");
      Assert.IsTrue(File.NameMatch(f, "blah blah.txt"));
    }

    [Test]
    public void SpacesAreNotIgnoredWithQuestionMark()
    {
      var f = new FileInfo("c:\\directory\\blah blah.txt");
      Assert.IsTrue(File.NameMatch(f, "blah?blah.txt"));
    }
  }
}
