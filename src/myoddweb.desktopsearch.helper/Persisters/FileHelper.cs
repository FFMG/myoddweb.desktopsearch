using System;
using myoddweb.desktopsearch.interfaces.Persisters;

namespace myoddweb.desktopsearch.helper.Persisters
{
  internal class FileHelper : IFileHelper
  {
    /// <inheritdoc />
    public long Id { get; }

    /// <inheritdoc />
    public string Name { get; }

    public FileHelper(long id, string name)
    {
      Id = id;
      Name = name ?? throw new ArgumentNullException( nameof(name) );
    }
  }
}
