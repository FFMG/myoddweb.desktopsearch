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
using System.Linq;
using myoddweb.desktopsearch.helper.IO;
using NUnit.Framework;
// ReSharper disable AssignmentIsFullyDiscarded

namespace myoddweb.desktopsearch.parser.test
{
  [TestFixture]
  internal class PartsTests
  {
    [Test]
    public void WordCannotBeNull()
    {
      Assert.Throws<ArgumentNullException>(() => _ = new Parts(null));
    }

    [Test]
    public void TheMaxPartSizeCannotBeZero()
    {
      Assert.Throws<ArgumentException>(() => _ = new Parts("Hello", 0 ));
    }

    [Test]
    public void TheMaxPartSizeCannotBeLessThanMinusOne()
    {
      Assert.Throws<ArgumentException>(() => _ = new Parts("Hello", -20 ));
    }

    [Test]
    public void TheMaxPartSizeCanBeMinusOne()
    {
      Assert.DoesNotThrow(() => _ = new Parts("Hello", -1));
    }

    [TestCase("", 0 )]
    [TestCase("A", 1)]
    [TestCase("AB", 3)]
    [TestCase("ABCDEFG", 28)]
    public void TheNumberOfItemsIsValid( string word, int expected )
    {
      var p = new Parts(word);
      Assert.AreEqual( expected, p.Count );
    }

    [TestCase("", 0)]
    [TestCase("AA", 2)]
    [TestCase("AAA", 3)]
    [TestCase("AAAAAAA", 7)]
    public void CountWithDuplicates(string word, int expected)
    {
      var p = new Parts(word);
      Assert.AreEqual(expected, p.Count);
    }

    [TestCase("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyz", -1, 1953)]
    [TestCase("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyz", 10, 575)]
    public void LargeParts(string word, int length, int expected)
    {
      var p = new Parts(word, length);
      Assert.AreEqual(expected, p.Count);
    }

    [TestCase("", new string[]{})]
    [TestCase("A", new [] {"A"})]
    [TestCase("AB", new[] { "A", "AB", "B" })]
    [TestCase("ABCDEFG", new []
    {
      "A", "AB", "ABC", "ABCD", "ABCDE", "ABCDEF", "ABCDEFG",
      "B", "BC", "BCD", "BCDE", "BCDEF", "BCDEFG",
      "C", "CD", "CDE", "CDEF", "CDEFG",
      "D", "DE", "DEF", "DEFG",
      "E", "EF", "EFG",
      "F", "FG",
      "G"
    })]
    public void CheckTheValues(string word, string[] expected)
    {
      var parts = new Parts(word);
      Assert.AreEqual(expected.Length, parts.Count);
      foreach (var p in parts)
      {
        Assert.IsNotNull(p);
        Assert.IsTrue( expected.Contains(p) );
      }
    }

    [TestCase("", 1, new string[] { })]
    [TestCase("A", 1, new[] { "A" })]
    [TestCase("AB", 1, new[] { "A", "B" })]
    [TestCase("ABCDEFG", -1, new[]
    {
      "A", "AB", "ABC", "ABCD", "ABCDE", "ABCDEF", "ABCDEFG",
      "B", "BC", "BCD", "BCDE", "BCDEF", "BCDEFG",
      "C", "CD", "CDE", "CDEF", "CDEFG",
      "D", "DE", "DEF", "DEFG",
      "E", "EF", "EFG",
      "F", "FG",
      "G"
    })]
    [TestCase("ABCDEFG", 4, new[]
    {
      "A", "AB", "ABC", "ABCD",
      "B", "BC", "BCD", "BCDE",
      "C", "CD", "CDE", "CDEF",
      "D", "DE", "DEF", "DEFG",
      "E", "EF", "EFG",
      "F", "FG",
      "G"
    })]
    [TestCase("ABCDEFG", 3, new[]
    {
      "A", "AB", "ABC",
      "B", "BC", "BCD",
      "C", "CD", "CDE",
      "D", "DE", "DEF",
      "E", "EF", "EFG",
      "F", "FG",
      "G"
    })]
    public void CheckTheValuesWithAMaxLength(string word, int length, string[] expected)
    {
      var parts = new Parts(word, length );
      Assert.AreEqual(expected.Length, parts.Count);
      foreach (var p in parts)
      {
        Assert.IsNotNull(p);
        Assert.IsTrue(expected.Contains(p));
      }
    }
  }
}
