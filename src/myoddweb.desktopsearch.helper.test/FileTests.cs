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
using System.Linq;
using NUnit.Framework;
using File = myoddweb.desktopsearch.helper.File;

namespace myoddweb.desktopsearch.parser.test
{
  [TestFixture]
  internal class FileTests
  {
    [TestCase("c:/a/bb/c", "c:/a/b/cc")]
    [TestCase("cc:/a/b/c", "c:/a/bb/c")]        //  root is wrong
    [TestCase("c:/a/bbb/c.d", "c:/a/b.d/c.d")]  // dot is wrong (first one)
    public void SameLenghButNotTheSame(string parent, string path)
    {
      Assert.IsFalse(
        File.IsSubDirectory(parent, path)
      );
    }

    [TestCase("c:/parent", "c:/child")]
    [TestCase("c:/b", "c:/a")]    //  same length... but not same 
    [TestCase("c:/aa", "c:/a/")]  //  same length... but not same 
    [TestCase("c:/aa", "c://a")]  //  same length... but not same 
    public void ParentIsLongerThanChild(string parent, string path)
    {
      Assert.IsFalse(
        File.IsSubDirectory(parent, path)
      );
    }

    [TestCase("c:/", "d:/")]
    [TestCase("c:/parent", "d:/parent")]
    [TestCase("c:/parent", "D:/Parent")]
    [TestCase("c:/parent", "d:/parent/")]
    [TestCase("c:/parent", "d://parent//")]
    [TestCase("c:/parent", "d:/parent/child")]
    public void DifferentRootPaths(string parent, string path)
    {
      Assert.IsFalse(
        File.IsSubDirectory(parent, path)
      );
    }

    [TestCase("cc:/parent", "ddd:/parent")]
    public void DifferentRootPathsLongRootPath(string parent, string path)
    {
      Assert.IsFalse(
        File.IsSubDirectory(parent, path)
      );
    }

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
    public void ChildCannotBeNull()
    {
      Assert.Throws<ArgumentNullException>(() =>
        File.IsSubDirectory( new List<string>
        {
          "D:\\A\\B\\",
          "c:\\A\\B\\C\\"
        }, 
        null)
      );
    }

    [Test]
    public void IsNotChildOfMultipleParents()
    {
      Assert.IsFalse(
        File.IsSubDirectory(new List<string>
          {
            "D:\\A\\B\\",
            "c:\\A\\B\\C\\"
          },
          "c:\\A\\B\\")
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

    [Test]
    public void SimpleUnion()
    {
      var fisA = new List<FileInfo>
      {
        new FileInfo("z:\\a.txt")
      };
      var fisB = new List<FileInfo>
      {
        new FileInfo("z:\\b.txt")
      };

      var fisU = File.Union(fisA, fisB);
      Assert.AreEqual( 2, fisU.Count);
      Assert.IsTrue( fisU.Any( f => f.FullName == "z:\\a.txt"));
      Assert.IsTrue(fisU.Any(f => f.FullName == "z:\\b.txt"));

      // nothing has changed
      Assert.AreEqual(1, fisA.Count);
      Assert.IsTrue(fisA.Any(f => f.FullName == "z:\\a.txt"));

      Assert.AreEqual(1, fisB.Count);
      Assert.IsTrue(fisB.Any(f => f.FullName == "z:\\b.txt"));
    }

    [Test]
    public void RemoveDuplicateUnion()
    {
      var fisA = new List<FileInfo>
      {
        new FileInfo("z:\\a.txt")
      };
      var fisB = new List<FileInfo>
      {
        new FileInfo("z:\\a.txt"),
        new FileInfo("z:\\b.txt")
      };

      var fisU = File.Union(fisA, fisB);
      Assert.AreEqual(2, fisU.Count);
      Assert.IsTrue(fisU.Any(f => f.FullName == "z:\\a.txt"));
      Assert.IsTrue(fisU.Any(f => f.FullName == "z:\\b.txt"));

      // nothing has changed
      Assert.AreEqual(1, fisA.Count);
      Assert.IsTrue(fisA.Any(f => f.FullName == "z:\\a.txt"));

      Assert.AreEqual(2, fisB.Count);
      Assert.IsTrue(fisB.Any(f => f.FullName == "z:\\a.txt"));
      Assert.IsTrue(fisB.Any(f => f.FullName == "z:\\b.txt"));
    }


    [Test]
    public void UnionFirstListIsEmptyList()
    {
      var fisA = new List<FileInfo>();
      var fisB = new List<FileInfo>
      {
        new FileInfo("z:\\a.txt"),
        new FileInfo("z:\\b.txt")
      };

      var fisU = File.Union(fisA, fisB);
      Assert.AreEqual(2, fisU.Count);
      Assert.IsTrue(fisU.Any(f => f.FullName == "z:\\a.txt"));
      Assert.IsTrue(fisU.Any(f => f.FullName == "z:\\b.txt"));

      // nothing has changed
      Assert.AreEqual(0, fisA.Count);

      Assert.AreEqual(2, fisB.Count);
      Assert.IsTrue(fisB.Any(f => f.FullName == "z:\\a.txt"));
      Assert.IsTrue(fisB.Any(f => f.FullName == "z:\\b.txt"));
    }

    [Test]
    public void UnionFirstListIsNullList()
    {
      var fis = new List<FileInfo>
      {
        new FileInfo("z:\\a.txt"),
        new FileInfo("z:\\b.txt")
      };

      var fisU = File.Union(null, fis);
      Assert.AreEqual(2, fisU.Count);
      Assert.IsTrue(fisU.Any(f => f.FullName == "z:\\a.txt"));
      Assert.IsTrue(fisU.Any(f => f.FullName == "z:\\b.txt"));

      Assert.AreEqual(2, fis.Count);
      Assert.IsTrue(fis.Any(f => f.FullName == "z:\\a.txt"));
      Assert.IsTrue(fis.Any(f => f.FullName == "z:\\b.txt"));
    }

    [Test]
    public void UnionSecondListIsEmpty()
    {
      var fisA = new List<FileInfo>
      {
        new FileInfo("z:\\a.txt"),
        new FileInfo("z:\\b.txt")
      };
      var fisB = new List<FileInfo>();

      var fisU = File.Union(fisA, fisB);
      Assert.AreEqual(2, fisU.Count);
      Assert.IsTrue(fisU.Any(f => f.FullName == "z:\\a.txt"));
      Assert.IsTrue(fisU.Any(f => f.FullName == "z:\\b.txt"));

      // nothing has changed
      Assert.AreEqual(2, fisA.Count);
      Assert.IsTrue(fisA.Any(f => f.FullName == "z:\\a.txt"));
      Assert.IsTrue(fisA.Any(f => f.FullName == "z:\\b.txt"));

      Assert.AreEqual(0, fisB.Count);
    }

    [Test]
    public void UnionSecondListIsNullList()
    {
      var fis = new List<FileInfo>
      {
        new FileInfo("z:\\a.txt"),
        new FileInfo("z:\\b.txt")
      };

      var fisU = File.Union(fis, null);
      Assert.AreEqual(2, fisU.Count);
      Assert.IsTrue(fisU.Any(f => f.FullName == "z:\\a.txt"));
      Assert.IsTrue(fisU.Any(f => f.FullName == "z:\\b.txt"));

      Assert.AreEqual(2, fis.Count);
      Assert.IsTrue(fis.Any(f => f.FullName == "z:\\a.txt"));
      Assert.IsTrue(fis.Any(f => f.FullName == "z:\\b.txt"));
    }

    [Test]
    public void UnionBothNull()
    {
      var fisU = File.Union(null, null);
      Assert.IsInstanceOf<List<FileInfo>>(fisU);
      Assert.AreEqual(0, fisU.Count);
    }

    [Test]
    public void IntersectionBothNull()
    {
      var fisI = File.Intersection(null, null);
      Assert.IsInstanceOf<List<FileInfo>>(fisI);
      Assert.AreEqual(0, fisI.Count);
    }

    [Test]
    public void IntersectionFirstListIsNullList()
    {
      var fis = new List<FileInfo>
      {
        new FileInfo("z:\\a.txt"),
        new FileInfo("z:\\b.txt")
      };

      var fisI = File.Intersection(null, fis);
      Assert.IsInstanceOf<List<FileInfo>>(fisI);
      Assert.AreEqual(0, fisI.Count);

      Assert.AreEqual(2, fis.Count);
      Assert.IsTrue(fis.Any(f => f.FullName == "z:\\a.txt"));
      Assert.IsTrue(fis.Any(f => f.FullName == "z:\\b.txt"));
    }

    [Test]
    public void IntersectionSecondListIsNullList()
    {
      var fis = new List<FileInfo>
      {
        new FileInfo("z:\\a.txt"),
        new FileInfo("z:\\b.txt")
      };

      var fisI = File.Intersection(fis, null);
      Assert.IsInstanceOf<List<FileInfo>>(fisI);
      Assert.AreEqual(0, fisI.Count);

      Assert.AreEqual(2, fis.Count);
      Assert.IsTrue(fis.Any(f => f.FullName == "z:\\a.txt"));
      Assert.IsTrue(fis.Any(f => f.FullName == "z:\\b.txt"));
    }

    [Test]
    public void SimpleIntersection()
    {
      var fisA = new List<FileInfo>
      {
        new FileInfo("z:\\a.txt"),
        new FileInfo("z:\\b.txt")
      };
      var fisB = new List<FileInfo>
      {
        new FileInfo("z:\\b.txt"),
        new FileInfo("z:\\c.txt")
      };

      var fisI = File.Intersection(fisA, fisB);
      Assert.AreEqual(1, fisI.Count);
      Assert.IsTrue(fisI.Any(f => f.FullName == "z:\\b.txt"));

      // nothing has changed
      Assert.AreEqual(2, fisA.Count);
      Assert.IsTrue(fisA.Any(f => f.FullName == "z:\\a.txt"));
      Assert.IsTrue(fisA.Any(f => f.FullName == "z:\\b.txt"));

      Assert.AreEqual(2, fisB.Count);
      Assert.IsTrue(fisB.Any(f => f.FullName == "z:\\b.txt"));
      Assert.IsTrue(fisB.Any(f => f.FullName == "z:\\c.txt"));
    }

    [Test]
    public void SimpleIntersectionWithDuplicates()
    {
      var fisA = new List<FileInfo>
      {
        new FileInfo("z:\\a.txt"),
        new FileInfo("z:\\b.txt"),
        new FileInfo("z:\\b.txt")
      };
      var fisB = new List<FileInfo>
      {
        new FileInfo("z:\\b.txt"),
        new FileInfo("z:\\b.txt"),
        new FileInfo("z:\\c.txt")
      };

      var fisI = File.Intersection(fisA, fisB);
      Assert.AreEqual(1, fisI.Count);
      Assert.IsTrue(fisI.Any(f => f.FullName == "z:\\b.txt"));

      // nothing has changed
      Assert.AreEqual(3, fisA.Count);
      Assert.IsTrue(fisA.Any(f => f.FullName == "z:\\a.txt"));
      Assert.AreEqual(2, fisA.Count(f => f.FullName == "z:\\b.txt"));

      Assert.AreEqual(3, fisB.Count);
      Assert.IsTrue(fisB.Any(f => f.FullName == "z:\\b.txt"));
      Assert.AreEqual(2, fisB.Count(f => f.FullName == "z:\\b.txt"));
    }

    [Test]
    public void ExactIntersection()
    {
      var fisA = new List<FileInfo>
      {
        new FileInfo("z:\\a.txt")
      };
      var fisB = new List<FileInfo>
      {
        new FileInfo("z:\\a.txt")
      };

      var fisI = File.Intersection(fisA, fisB);
      Assert.AreEqual(1, fisI.Count);
      Assert.IsTrue(fisI.Any(f => f.FullName == "z:\\a.txt"));

      // nothing has changed
      Assert.AreEqual(1, fisA.Count);
      Assert.IsTrue(fisA.Any(f => f.FullName == "z:\\a.txt"));

      Assert.AreEqual(1, fisB.Count);
      Assert.IsTrue(fisB.Any(f => f.FullName == "z:\\a.txt"));
    }

    [Test]
    public void NoIntersectionAtAll()
    {
      var fisA = new List<FileInfo>
      {
        new FileInfo("z:\\a.txt")
      };
      var fisB = new List<FileInfo>
      {
        new FileInfo("z:\\b.txt")
      };

      var fisI = File.Intersection(fisA, fisB);
      Assert.AreEqual(0, fisI.Count);
      Assert.IsInstanceOf<List<FileInfo>>(fisI);

      // nothing has changed
      Assert.AreEqual(1, fisA.Count);
      Assert.IsTrue(fisA.Any(f => f.FullName == "z:\\a.txt"));

      Assert.AreEqual(1, fisB.Count);
      Assert.IsTrue(fisB.Any(f => f.FullName == "z:\\b.txt"));
    }

    [Test]
    public void RemoveDuplicates()
    {
      var fis = new List<FileInfo>
      {
        new FileInfo("z:\\a.txt"),
        new FileInfo("z:\\a.txt"),
        new FileInfo("z:\\a.txt"),
        new FileInfo("z:\\a.txt")
      };

      var fisD = File.Distinct(fis);
      Assert.AreEqual(1, fisD.Count);
      Assert.IsTrue(fisD.Any(f => f.FullName == "z:\\a.txt"));

      // nothing has changed
      Assert.AreEqual(4, fis.Count(f => f.FullName == "z:\\a.txt"));
    }

    [Test]
    public void RemoveDuplicatesSimple()
    {
      var fis = new List<FileInfo>
      {
        new FileInfo("z:\\a.txt"),
        new FileInfo("z:\\b.txt"),
        new FileInfo("z:\\a.txt"),
        new FileInfo("z:\\a.txt")
      };

      var fisD = File.Distinct(fis);
      Assert.AreEqual(2, fisD.Count);
      Assert.IsTrue(fisD.Any(f => f.FullName == "z:\\a.txt"));
      Assert.IsTrue(fisD.Any(f => f.FullName == "z:\\b.txt"));

      // nothing has changed
      Assert.AreEqual(3, fis.Count(f => f.FullName == "z:\\a.txt"));
      Assert.AreEqual(1, fis.Count(f => f.FullName == "z:\\b.txt"));
    }

    [Test]
    public void ComplementWithNullB()
    {
      var fis = new List<FileInfo>
      {
        new FileInfo("z:\\a.txt"),
        new FileInfo("z:\\b.txt"),
      };
      var fisC = File.RelativeComplement(fis, null );

      Assert.AreEqual(0, fisC.Count);
      Assert.IsInstanceOf<List<FileInfo>>(fisC);
    }

    [Test]
    public void ComplementWithNullA()
    {
      var fis = new List<FileInfo>
      {
        new FileInfo("z:\\a.txt"),
        new FileInfo("z:\\b.txt"),
      };
      var fisC = File.RelativeComplement(null, fis);

      Assert.AreEqual(2, fisC.Count);
      Assert.AreEqual(1, fisC.Count(f => f.FullName == "z:\\a.txt"));
      Assert.AreEqual(1, fisC.Count(f => f.FullName == "z:\\b.txt"));
    }

    [Test]
    public void ComplementWithDumplicates()
    {
      var fisA = new List<FileInfo>
      {
        new FileInfo("z:\\c.txt"),
      };

      var fisB = new List<FileInfo>
      {
        new FileInfo("z:\\a.txt"),
        new FileInfo("z:\\b.txt"),
        new FileInfo("z:\\a.txt")
      };
      var fisC = File.RelativeComplement(fisA, fisB);

      Assert.AreEqual(2, fisC.Count);
      Assert.AreEqual(1, fisC.Count(f => f.FullName == "z:\\a.txt"));
      Assert.AreEqual(1, fisC.Count(f => f.FullName == "z:\\b.txt"));

      Assert.AreEqual(3, fisB.Count);
      Assert.AreEqual(2, fisB.Count(f => f.FullName == "z:\\a.txt"));
      Assert.AreEqual(1, fisB.Count(f => f.FullName == "z:\\b.txt"));
    }

    [Test]
    public void ComplementWithDumplicatesAndNullA()
    {
      var fis = new List<FileInfo>
      {
        new FileInfo("z:\\a.txt"),
        new FileInfo("z:\\b.txt"),
        new FileInfo("z:\\a.txt")
      };
      var fisC = File.RelativeComplement(null, fis);

      Assert.AreEqual(2, fisC.Count);
      Assert.AreEqual(1, fisC.Count(f => f.FullName == "z:\\a.txt"));
      Assert.AreEqual(1, fisC.Count(f => f.FullName == "z:\\b.txt"));

      Assert.AreEqual(3, fis.Count);
      Assert.AreEqual(2, fis.Count(f => f.FullName == "z:\\a.txt"));
      Assert.AreEqual(1, fis.Count(f => f.FullName == "z:\\b.txt"));
    }

    [Test]
    public void SimpleComplement()
    {
      var fisA = new List<FileInfo>
      {
        new FileInfo("z:\\c.txt"),
      };

      var fisB = new List<FileInfo>
      {
        new FileInfo("z:\\a.txt"),
        new FileInfo("z:\\b.txt"),
        new FileInfo("z:\\c.txt")
      };
      var fisC = File.RelativeComplement(fisA, fisB);

      Assert.AreEqual(2, fisC.Count);
      Assert.AreEqual(1, fisC.Count(f => f.FullName == "z:\\a.txt"));
      Assert.AreEqual(1, fisC.Count(f => f.FullName == "z:\\b.txt"));

      Assert.AreEqual(1, fisA.Count);
      Assert.AreEqual(1, fisA.Count(f => f.FullName == "z:\\c.txt"));

      Assert.AreEqual(3, fisB.Count);
      Assert.AreEqual(1, fisB.Count(f => f.FullName == "z:\\a.txt"));
      Assert.AreEqual(1, fisB.Count(f => f.FullName == "z:\\b.txt"));
      Assert.AreEqual(1, fisB.Count(f => f.FullName == "z:\\c.txt"));
    }

    [Test]
    public void BothNullDirectoryInfoCompare()
    {
      Assert.IsTrue( File.Equals(null, null) );
    }

    [Test]
    public void NullLhsDirectoryInfoCompare()
    {
      Assert.IsFalse( File.Equals(null, new DirectoryInfo("c:/test/")));
    }

    [Test]
    public void NullRhsDirectoryInfoCompare()
    {
      Assert.IsFalse( File.Equals( new DirectoryInfo("c:/test/"), null ));
    }

    [TestCase("c:/", "c:/")]
    [TestCase("c:\\", "c:\\")]
    [TestCase("c:\\Test", "c://Test")]
    [TestCase("c:\\Test\\", "c://Test")]
    [TestCase("c://Test//", "c://Test")]
    [TestCase("c://Test//Test2", "c://Test//Test2//")]
    [TestCase("c://TEST//", "c://TEST")]
    [TestCase("c://root//", "c://ROOT")]
    public void DirectoriesAreEqual( string lhs, string rhs)
    {
      Assert.IsTrue( 
        File.Equals(new DirectoryInfo(lhs), new DirectoryInfo(rhs)));
    }

    [TestCase("c:\\hello.TxT", "txt")]
    [TestCase("c:\\hello.TxT", ".txt")]
    [TestCase("c:\\hello.txt", "txt")]
    [TestCase("c:\\hello.txt", "TXT")]
    [TestCase("c:\\hello.txt", ".txt")]
    [TestCase("c:\\hello.txt", ".TXT")]
    [TestCase("c:\\hello.txt", "Txt")]
    [TestCase("c:\\hello.txt", ".TxT")]
    [TestCase("c:\\hello.ext1.ext2", ".ext2")]
    [TestCase("c:\\hello.ext1.ext2", "ext2")]
    [TestCase("c:\\hello.ext1.ext2", ".ext1.ext2")]
    [TestCase("c:\\hello.ext1.ext2", "ext1.ext2")]
    public void SingleValidExt(string file, string ext )
    {
      Assert.IsTrue(
        File.IsExtension(new FileInfo(file), ext )
        );
    }

    [TestCase("c:\\hello.txt", new []{"bad", "txt"})]
    [TestCase("c:\\hello.txt", new[] {"bad1", "txt", "bad2" })]
    [TestCase("c:\\hello.txt", new[] {"txt" })]
    [TestCase("c:\\hello.txt", new[] { ".bad", ".txt" })]
    [TestCase("c:\\hello.txt", new[] { ".bad1", ".txt", ".bad2" })]
    [TestCase("c:\\hello.txt", new[] { ".txt" })]
    public void MultipleValidExt(string file, string[] ext)
    {
      Assert.IsTrue(
        File.IsExtension(new FileInfo(file), ext)
      );
    }

    [TestCase("c:\\hello.txt", "t")]
    [TestCase("c:\\hello.txt", ".t")]
    [TestCase("c:\\.bin", ".bin")]
    [TestCase("c:\\.bin", "bin")]
    [TestCase("c:\\bin", "bin")]
    [TestCase("c:\\bin", ".bin")]
    public void SingleInvalidExt(string file, string ext)
    {
      Assert.IsFalse(
        File.IsExtension(new FileInfo(file), ext)
      );
    }

    [Test]
    public void LargeRelativeComplementWithNoMatch()
    {
      const int count = 10000;
      var fisA = new List<FileInfo>(count);
      for( var i = 0; i < count; ++i )
      {
        fisA.Add( new FileInfo( $"c:\\{Guid.NewGuid()}.txt"));
      }

      // B has everything in A
      var fisB = new List<FileInfo>(fisA);
      var fisC = File.RelativeComplement(fisA, fisB);

      // everything in A is in B ... so nothing is in C
      Assert.AreEqual(0, fisC.Count);
    }

    [Test]
    public void BothNullWillReturnAnEmptyList()
    {
      // 
      var fi = File.RelativeComplement(null, null);
      Assert.IsInstanceOf< List<FileInfo>>( fi );
      Assert.AreEqual(0, fi.Count);
    }

    [Test]
    public void LargeRelativeComplementWithOneMatch()
    {
      const int count = 10000;
      var fisA = new List<FileInfo>(count);
      for (var i = 0; i < count; ++i)
      {
        fisA.Add(new FileInfo($"c:\\{Guid.NewGuid()}.txt"));
      }

      // B has everything in A and one extra
      var extra = new FileInfo($"c:\\{Guid.NewGuid()}.txt");
      var fisB = new List<FileInfo>(fisA)
      {
        extra
      };

      // 
      var fisC = File.RelativeComplement(fisA, fisB);

      // Return the list of elements that are in B but not in A
      // in other words ... just 'extra'
      Assert.AreEqual(1, fisC.Count);
      Assert.IsTrue(File.Equals(fisC[0], extra));
    }

    [Test]
    public void LargeRelativeComplemenAllMatch()
    {
      const int count = 10000;
      // A has nothing in it at all.
      var fisA = new List<FileInfo>();

      var fisB = new List<FileInfo>(count);
      for (var i = 0; i < count; ++i)
      {
        fisB.Add(new FileInfo($"c:\\{Guid.NewGuid()}.txt"));
      }

      // Return the list of elements that are in B but not in A
      // in other words ... everything.
      var fisC = File.RelativeComplement(fisA, fisB);

      // everything in A is in B ... so nothing is in C
      Assert.AreEqual(count, fisC.Count);
    }
  }
}
