namespace myoddweb.desktopsearch.interfaces.Persisters
{
  public interface ITransaction
  {
    /// <summary>
    /// The current connection factory.
    /// </summary>
    IConnectionFactory Factory { get; set; }

    /// <summary>
    /// Prepare for a transaction
    /// </summary>
    void Prepare(IPersister persister, IConnectionFactory factory );

    /// <summary>
    /// Complete a transaction
    /// </summary>
    /// <param name="factory">The factory that we just completed.</param>
    /// <param name="success">If the transaction was successfull or not.</param>
    void Complete(IConnectionFactory factory, bool success);
  }
}