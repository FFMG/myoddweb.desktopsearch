namespace myoddweb.desktopsearch.service
{
  internal class Program
  {
    static void Main(string[] args)
    {
      using (var service = new DesktopSearchService())
      {
        service.InvokeAction(args);
      }
    }
  }
}
