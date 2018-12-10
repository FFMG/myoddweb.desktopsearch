namespace myoddweb.desktopsearch.interfaces.Persisters
{
  public interface ITransaction
  {
    /// <summary>
    /// Prepare for a transaction
    /// </summary>
    void Prepare(IPersister persister, IConnectionFactory factory );

    /// <summary>
    /// Complete a transaction
    /// </summary>
    /// <param name="success">If the transaction was successfull or not.</param>
    void Complete(bool success);
  }
}