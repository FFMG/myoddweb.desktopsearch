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

namespace myoddweb.desktopsearch.parser.test
{
  [TestFixture]
  internal class WordTest
  {
    private const int MaxNumCharactersPerParts = 255;

    [Test]
    public void ValueCannotBeNull()
    {
      Assert.Throws<ArgumentNullException>(() =>{ var _ = new Word(null, MaxNumCharactersPerParts); });
    }

    [Test]
    public void ValueCannotBeNullString()
    {
      const string value = null;
      Assert.Throws<ArgumentNullException>(() => { var _ = new Word(value, MaxNumCharactersPerParts); });
    }

    [TestCase("Mix", new [] {"Mix", "ix", "x"})]
    [TestCase("mix", new[] { "mix", "ix", "x" })]
    [TestCase("Home", new[] { "Home", "ome", "me", "e" })]
    [TestCase("notes", new[]
    {
      "notes", "otes", "tes", "es", "s"
    })]
    public void SimpleParts( string value, string[] expected )
    {
      var w = new Word(value, MaxNumCharactersPerParts);
      Assert.That(w.Parts, Is.EquivalentTo(expected));
    }

    [TestCase("ooo", new[] { "ooo", "o", "oo" })]
    [TestCase("Ooo", new[] { "Ooo", "oo", "o" })]
    public void RepeatLetters(string value, string[] expected)
    {
      var w = new Word(value, MaxNumCharactersPerParts);
      Assert.That(w.Parts, Is.EquivalentTo(expected));
    }

    [Test]
    public void PartsCannotBeZero()
    {
      const string value = "Hello";
      Assert.Throws<ArgumentException>(() => { var _ = new Word(value, 0); });
    }

    [TestCase(-1)]
    [TestCase(-42)]
    [TestCase(int.MinValue)]
    public void PartsCannotBeNegative( int number )
    {
      const string value = "Hello";
      Assert.Throws<ArgumentException>(() => { var _ = new Word(value, number); });
    }

    [Test]
    public void TheMaxPartLenIsUsedProperly()
    {
      const string value = "Hello";
      var w = new Word(value, 1);
      var p = w.Parts;
      Assert.AreEqual( new []{"H", "e", "l", "o" }, p.ToArray() );
    }

    [Test]
    public void IfThePartsizeIsTheSameAsTheWordLenItDoesNotMatter()
    {
      const string value = "Cat";
      var w = new Word(value, 3);
      var p = w.Parts;
      Assert.AreEqual(new[] { "Cat", "at", "t" }, p.ToArray());
    }

    [Test]
    public void TheMaxPartLeIsLongerThanTheWordItself()
    {
      const string value = "Hi";
      var w = new Word(value, MaxNumCharactersPerParts);
      var p = w.Parts;
      Assert.AreEqual(new [] { "Hi", "i" }, p.ToArray());
    }
  }
}
