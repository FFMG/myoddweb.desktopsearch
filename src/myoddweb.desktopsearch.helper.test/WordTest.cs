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
using myoddweb.desktopsearch.helper.IO;
using NUnit.Framework;

namespace myoddweb.desktopsearch.parser.test
{
  [TestFixture]
  internal class WordTest
  {
    [Test]
    public void ValueCannotBeNull()
    {
      Assert.Throws<ArgumentNullException>(() =>{ var _ = new Word(null); });
    }

    [Test]
    public void ValueCannotBeNullString()
    {
      const string value = null;
      Assert.Throws<ArgumentNullException>(() => { var _ = new Word(value); });
    }

    [TestCase("Mix", new [] {"M", "Mi", "Mix", "i", "ix", "x"})]
    [TestCase("mix", new[] { "m", "mi", "mix", "i", "ix", "x" })]
    [TestCase("Home", new[] { "H", "Ho", "Hom", "Home", "o", "om", "ome", "m", "me", "e" })]
    [TestCase("notes", new[]
    {
      "n", "no", "not", "note", "notes",
      "o", "ot", "ote", "otes",
      "t", "te", "tes",
      "e", "es",
      "s",
    })]
    public void SimpleParts( string value, string[] expected )
    {
      var w = new Word(value);
      Assert.That(w.Parts( 128 ), Is.EquivalentTo(expected));
    }

    [TestCase("ooo", new[] { "o", "ooo", "oo" })]
    [TestCase("Ooo", new[] { "O", "Oo", "Ooo", "o", "oo" })]
    public void RepeatLetters(string value, string[] expected)
    {
      var w = new Word(value);
      Assert.That(w.Parts(128), Is.EquivalentTo(expected));
    }

    [Test]
    public void PartsCannotBeZero()
    {
      const string value = "Hello";
      var w = new Word(value);
      Assert.Throws<ArgumentException>(() => { var _ = w.Parts(0); });
    }

    [Test]
    public void PartsCannotBeNegative()
    {
      const string value = "Hello";
      var w = new Word(value);
      Assert.Throws<ArgumentException>(() => { var _ = w.Parts(-1); });
      Assert.Throws<ArgumentException>(() => { var _ = w.Parts(-42); });
    }
  }
}
