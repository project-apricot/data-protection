namespace ApricotFramework.DataProtection;

/// <summary>
/// Reads and writes the rows the key ring is stored in.
/// </summary>
/// <remarks>
/// Synchronous throughout, because the repository the framework asks for is synchronous. An async
/// contract here would buy nothing and force a blocking call at every implementation.
/// </remarks>
public interface IProtectionKeyStore
{
    /// <summary>
    /// Gets every stored row.
    /// </summary>
    /// <returns>The rows, in no guaranteed order.</returns>
    IReadOnlyList<ProtectionKeyRecord> GetAll();

    /// <summary>
    /// Stores one element.
    /// </summary>
    /// <param name="friendlyName">The name to store it under.</param>
    /// <param name="xml">The serialized element.</param>
    void Add(string friendlyName, string? xml);

    /// <summary>
    /// Deletes rows by identifier.
    /// </summary>
    /// <param name="orderedIds">The identifiers, in the order they must be deleted.</param>
    /// <returns>True if every row was deleted.</returns>
    /// <remarks>
    /// Order is a correctness requirement, not a preference: the caller chooses it so that a
    /// partial failure never leaves the key ring in a state it cannot recover from. Stop at the
    /// first failure rather than continuing.
    /// </remarks>
    bool Delete(IReadOnlyList<int> orderedIds);
}
