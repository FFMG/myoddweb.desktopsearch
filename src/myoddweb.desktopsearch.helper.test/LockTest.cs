using System.Threading;
using System.Threading.Tasks;
using myoddweb.desktopsearch.helper.Lock;
using NUnit.Framework;

namespace myoddweb.desktopsearch.parser.test
{
  [TestFixture]
  internal class LockTest
  {
    [Test]
    public async Task TestSingleEntryAsync()
    {
      bool gotLock;
      var l = new Lock();
      using (await l.TryAsync( CancellationToken.None).ConfigureAwait(false))
      {
        gotLock = true;
      }
      Assert.True(gotLock);
    }

    [Test]
    public void TestSingleEntry()
    {
      bool gotLock;
      var l = new Lock();
      using (l.Try())
      {
        gotLock = true;
      }
      Assert.True(gotLock);
    }

    [Test]
    public async Task ManualReleaseLockAsync()
    {
      bool gotLock;
      bool wasDisposed;
      var l = new Lock();
      var key = await l.TryAsync(CancellationToken.None).ConfigureAwait(false);
      try
      {
        gotLock = true;
      }
      finally 
      {
        key.Dispose();
        wasDisposed = true;
      }
      Assert.True(gotLock);
      Assert.True(wasDisposed);
    }

    [Test]
    public void ManualReleaseLock()
    {
      bool gotLock;
      bool wasDisposed;
      var l = new Lock();
      var key = l.TryAsync(CancellationToken.None);
      try
      {
        gotLock = true;
      }
      finally
      {
        key.Dispose();
        wasDisposed = true;
      }
      Assert.True(gotLock);
      Assert.True(wasDisposed);
    }

    [Test]
    public async Task MultipleEntries()
    {
      var gotLock = 0;
      var l = new Lock();
      using (await l.TryAsync(CancellationToken.None).ConfigureAwait(false))
      {
        ++gotLock;
      }
      using (await l.TryAsync(CancellationToken.None).ConfigureAwait(false))
      {
        ++gotLock;
      }
      Assert.AreEqual(2, gotLock);
    }
  }
}
