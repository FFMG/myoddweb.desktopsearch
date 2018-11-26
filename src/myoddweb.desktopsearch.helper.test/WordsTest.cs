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
using myoddweb.desktopsearch.helper.IO;
using NUnit.Framework;

namespace myoddweb.desktopsearch.parser.test
{
  [TestFixture]
  internal class WordsTest
  {
    private const int MaxNumCharactersPerParts = 255;

    [Test]
    public void CreateEmptyList()
    {
      var words = new Words();
      Assert.That( words.Count == 0 );
    }

    [Test]
    public void CreateEmptyListAndAddNull()
    {
      var words = new Words();
      Assert.That(words.Count == 0);
      words.Add( (Word)null );
      Assert.That(words.Count == 0);
    }

    [Test]
    public void CreateEmptyListAndAddSingleWord()
    {
      var words = new Words();
      Assert.That(words.Count == 0);
      words.Add(new Word("Test", MaxNumCharactersPerParts));
      Assert.That(words.Count == 1);
      Assert.AreEqual("Test", words[0].Value);
    }

    [Test]
    public void CreateWithSingleWord()
    {
      var words = new Words( new Word("Test", MaxNumCharactersPerParts));
      Assert.That(words.Count == 1);
      Assert.AreEqual("Test", words[0].Value);
    }

    [Test]
    public void CreateWithSingleNullWord()
    {
      // word not really added.
      var words = new Words( (Word)null);
      Assert.That(words.Count == 0);
    }

    [Test]
    public void CreateWithArrayOfWord()
    {
      // word not really added.
      var words = new Words( new []{ new Word("a", MaxNumCharactersPerParts), new Word("b", MaxNumCharactersPerParts) });
      Assert.That(words.Count == 2);
      Assert.AreEqual("a", words[0].Value);
      Assert.AreEqual("b", words[1].Value);
    }

    [Test]
    public void CreateWithArrayOfNullWord()
    {
      var words0 = new Words(new[] { (Word)null, null });
      Assert.That(words0.Count == 0);

      var words1 = new Words(new[] { (Word)null });
      Assert.That(words1.Count == 0);
    }

    [Test]
    public void CreateWithEmptyArrayOfWord()
    {
      var words0 = new Words(new Word[]{});
      Assert.That(words0.Count == 0);
    }

    [Test]
    public void CreateWithNullArrayOfWord()
    {
      var words = new Words( (Word[])null);
      Assert.That(words.Count == 0);
    }

    [Test]
    public void CreateWithArrayOfWordWithSomeNulls()
    {
      // word not really added.
      var words = new Words(new[] { new Word("a", MaxNumCharactersPerParts), null, new Word("b", MaxNumCharactersPerParts), null });
      Assert.That(words.Count == 2);
      Assert.AreEqual("a", words[0].Value);
      Assert.AreEqual("b", words[1].Value);
    }

    [Test]
    public void CreateNullArrayOfWords()
    {
      var words = new Words( (Words[])null);
      Assert.That(words.Count == 0);
    }

    [Test]
    public void CreateArrayOfWords()
    {
      var words = new Words(
        new[]
        {
          new Words( new Word("a", MaxNumCharactersPerParts)),
          new Words( new Word("b", MaxNumCharactersPerParts)),
          new Words( new Word("c", MaxNumCharactersPerParts))
        }
      );
      Assert.That(words.Count == 3);
    }

    [Test]
    public void CreateArrayOfWordsWithSomeNull()
    {
      var words = new Words(
        new[]
        {
          new Words( new Word("a", MaxNumCharactersPerParts)),
          null,
          new Words( new Word("b", MaxNumCharactersPerParts)),
          null
        }
      );
      Assert.That(words.Count == 2);
      Assert.AreEqual("a", words[0].Value);
      Assert.AreEqual("b", words[1].Value);
    }

    [Test]
    public void CreateArrayOfWordsWithAllNull()
    {
      var words = new Words(
        new[]
        {
          (Words)null,
          null
        }
      );
      Assert.That(words.Count == 0);
    }

    [Test]
    public void CreateEmptyArrayOfWords()
    {
      var words = new Words(new Words[] {});
      Assert.That(words.Count == 0);
    }

    [Test]
    public void CreateNullReadOnlyCollection()
    {
      var words = new Words( (IReadOnlyCollection<string>)null, MaxNumCharactersPerParts);
      Assert.That(words.Count == 0);
    }

    [Test]
    public void CreateCollectionOfStrings()
    {
      var words = new Words( new List<string>
      {
        "a", "b"
      }, MaxNumCharactersPerParts);
      Assert.That(words.Count == 2);
      Assert.AreEqual("a", words[0].Value);
      Assert.AreEqual("b", words[1].Value);
    }

    [Test]
    public void CreateCollectionOfStringsWithSomeNull()
    {
      var words = new Words(new List<string>
      {
        "a", null, "b", null
      }, MaxNumCharactersPerParts);
      Assert.That(words.Count == 2);
      Assert.AreEqual("a", words[0].Value);
      Assert.AreEqual("b", words[1].Value);
    }

    [Test]
    public void CreateCollectionOfStringsAllNulls()
    {
      var words = new Words(new List<string>
      {
        null, null
      }, MaxNumCharactersPerParts);
      Assert.That(words.Count == 0);
    }

    [Test]
    public void CreateEmptyCollectionOfStrings()
    {
      var words = new Words(new List<string>(), MaxNumCharactersPerParts);
      Assert.That(words.Count == 0);
    }

    [Test]
    public void AddANullString()
    {
      #pragma warning disable IDE0028 // Simplify collection initialization
      // ReSharper disable once UseObjectOrCollectionInitializer
      var words = new Words();
      #pragma warning restore IDE0028 // Simplify collection initialization
      words.Add( (string) null, MaxNumCharactersPerParts);
      Assert.That(words.Count == 0);
    }

    [Test]
    public void AddAString()
    {
#pragma warning disable IDE0028 // Simplify collection initialization
      // ReSharper disable once UseObjectOrCollectionInitializer
      var words = new Words();
#pragma warning restore IDE0028 // Simplify collection initialization
      words.Add("a", MaxNumCharactersPerParts);
      Assert.That(words.Count == 1);
      Assert.AreEqual("a", words[0].Value);

      words.Add("b", MaxNumCharactersPerParts);
      Assert.That(words.Count == 2);
      Assert.AreEqual("b", words[1].Value);
    }

    [Test]
    public void AddNullArrayOfWord()
    {
      Assert.DoesNotThrow(() =>
      {
#pragma warning disable IDE0028 // Simplify collection initialization
        // ReSharper disable once UseObjectOrCollectionInitializer
        var words = new Words();
#pragma warning restore IDE0028 // Simplify collection initialization
        words.Add( (Word[]) null );
        Assert.That(words.Count == 0);
      });
    }

    [Test]
    public void AddArrayOfWordWithSomeNulls()
    {
#pragma warning disable IDE0028 // Simplify collection initialization
      // ReSharper disable once UseObjectOrCollectionInitializer
      var words = new Words();
#pragma warning restore IDE0028 // Simplify collection initialization
      words.Add(new[] { new Word("a", MaxNumCharactersPerParts), null, new Word("b", MaxNumCharactersPerParts), null });
      Assert.That(words.Count == 2);
      Assert.AreEqual("a", words[0].Value);
      Assert.AreEqual("b", words[1].Value);
    }

    [Test]
    public void AddArrayOfWord()
    {
#pragma warning disable IDE0028 // Simplify collection initialization
      // ReSharper disable once UseObjectOrCollectionInitializer
      var words = new Words();
#pragma warning restore IDE0028 // Simplify collection initialization
      words.Add(new[] { new Word("a", MaxNumCharactersPerParts), new Word("b", MaxNumCharactersPerParts) });
      Assert.That(words.Count == 2);
      Assert.AreEqual("a", words[0].Value);
      Assert.AreEqual("b", words[1].Value);
    }

    [Test]
    public void AddArrayOfStringDoesNotAllowDuplicates()
    {
#pragma warning disable IDE0028 // Simplify collection initialization
      // ReSharper disable once UseObjectOrCollectionInitializer
      var words = new Words();
#pragma warning restore IDE0028 // Simplify collection initialization
      words.Add(new[] { "a", "b", "a" }, MaxNumCharactersPerParts);
      Assert.That(words.Count == 2);
      Assert.AreEqual("a", words[0].Value);
      Assert.AreEqual("b", words[1].Value);
    }

    [Test]
    public void AddArrayOfStringWithSomeNulls()
    {
#pragma warning disable IDE0028 // Simplify collection initialization
      // ReSharper disable once UseObjectOrCollectionInitializer
      var words = new Words();
#pragma warning restore IDE0028 // Simplify collection initialization
      words.Add(new[] { "a", "b", null }, MaxNumCharactersPerParts);
      Assert.That(words.Count == 2);
      Assert.AreEqual("a", words[0].Value);
      Assert.AreEqual("b", words[1].Value);
    }

    [Test]
    public void CreateArrayOfStringDoesNotAllowDuplicates()
    {
      var words = new Words( new[] { "a", "b", "a" }, MaxNumCharactersPerParts);
      Assert.That(words.Count == 2);
      Assert.AreEqual("a", words[0].Value);
      Assert.AreEqual("b", words[1].Value);
    }

    [Test]
    public void CreateArrayOfStringAndTryToAddDuplicates()
    {
      var words = new Words(new[] { "a", "b", "a" }, MaxNumCharactersPerParts);
      Assert.That(words.Count == 2);
      Assert.AreEqual("a", words[0].Value);
      Assert.AreEqual("b", words[1].Value);

      // add more words
      words.Add(new[] { "a", "b", null }, MaxNumCharactersPerParts);
      Assert.That(words.Count == 2);
      Assert.AreEqual("a", words[0].Value);
      Assert.AreEqual("b", words[1].Value);
    }

    [Test]
    public void CreateArrayOfWordsWithDuplicates()
    {
      var words = new Words(
        new[]
        {
          new Words( new Word("a", MaxNumCharactersPerParts)),
          new Words( new Word("a", MaxNumCharactersPerParts)),
          new Words( new Word("b", MaxNumCharactersPerParts)),
          new Words( new Word("a", MaxNumCharactersPerParts))
        }
      );
      Assert.That(words.Count == 2);
      Assert.AreEqual("a", words[0].Value);
      Assert.AreEqual("b", words[1].Value);
    }

    [Test]
    public void AddASingleWord()
    {
#pragma warning disable IDE0028 // Simplify collection initialization
      // ReSharper disable once UseObjectOrCollectionInitializer
      var words = new Words();
#pragma warning restore IDE0028 // Simplify collection initialization
      words.Add( new Word("a", MaxNumCharactersPerParts));
      Assert.That(words.Count == 1);
    }

    [Test]
    public void AddASingleWordMultipleTimes()
    {
#pragma warning disable IDE0028 // Simplify collection initialization
      // ReSharper disable once UseObjectOrCollectionInitializer
      var words = new Words( );
#pragma warning restore IDE0028 // Simplify collection initialization
      words.Add(new Word("a", MaxNumCharactersPerParts));
      words.Add(new Word("a", MaxNumCharactersPerParts));
      words.Add(new Word("b", MaxNumCharactersPerParts));
      words.Add(new Word("a", MaxNumCharactersPerParts));
      Assert.That(words.Count == 2);
      Assert.AreEqual("a", words[0].Value);
      Assert.AreEqual("b", words[1].Value);
    }

    [Test]
    public void TryToGetAnItemOutOfRangeThrowsAnError()
    {
#pragma warning disable IDE0028 // Simplify collection initialization
      // ReSharper disable once UseObjectOrCollectionInitializer
      var words = new Words();
#pragma warning restore IDE0028 // Simplify collection initialization
      words.Add(new[] { new Word("a", MaxNumCharactersPerParts), new Word("b", MaxNumCharactersPerParts) });
      Assert.That(words.Count == 2);

      Assert.Throws<IndexOutOfRangeException>( () =>
      {
        var _ = words[3].Value;
      } );
    }
  }
}
