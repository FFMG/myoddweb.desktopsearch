using System;
using System.Runtime.CompilerServices;
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
      var gotLock = false;
      var l = new Lock();
      using (await l.TryAsync().ConfigureAwait(false))
      {
        gotLock = true;
      }
      Assert.True(gotLock);
    }

    [Test]
    public void TestSingleEntry()
    {
      var gotLock = false;
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
      var gotLock = false;
      var wasDisposed = false;
      var l = new Lock();
      var key = await l.TryAsync().ConfigureAwait(false);
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
      var gotLock = false;
      var wasDisposed = false;
      var l = new Lock();
      var key = l.TryAsync();
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
      using (await l.TryAsync().ConfigureAwait(false))
      {
        ++gotLock;
      }
      using (await l.TryAsync().ConfigureAwait(false))
      {
        ++gotLock;
      }
      Assert.AreEqual(2, gotLock);
    }

    private async Task<bool> LockingTest( Lock l )
    {
      using (await l.TryAsync().ConfigureAwait(false))
      {
        return true;
      }
    }

    private async Task<bool> Blah(Lock l)
    {
      var context = Guid.NewGuid().ToString();
      var valueToDisplay = Thread.CurrentThread.ManagedThreadId;
TestContext.WriteLine($"{context}: {valueToDisplay}");
      await Task.Yield();
      using (await l.TryAsync().ConfigureAwait(false))
      {
        await Task.Yield();
        await Task.Delay(500, CancellationToken.None).ConfigureAwait(false);
TestContext.WriteLine($"{context}: {valueToDisplay}");
        if (await LockingTest(l))
        {
          await Task.Yield();
TestContext.WriteLine($"{context}: {valueToDisplay}");
          return true;
        }
      }
      return false;
    }

    [Test]
    public async Task ReEntryTest()
    {
      var canceler = new CancellationTokenSource();
      var gotLock = false;
      var l = new Lock();
      var worker1 = Task.Factory.StartNew(async () => await Blah(l), canceler.Token );
      var worker2 = Task.Factory.StartNew(async () => await Blah(l), canceler.Token);
      var worker3 = Task.Factory.StartNew(async () => await Blah(l), canceler.Token);

      Task.Delay(2000, CancellationToken.None).Wait(CancellationToken.None);
      canceler.Cancel();
      await Task.WhenAll( new []{worker1,worker2, worker3} ).ConfigureAwait(false);
      Assert.True( gotLock );
    }
  }
}
