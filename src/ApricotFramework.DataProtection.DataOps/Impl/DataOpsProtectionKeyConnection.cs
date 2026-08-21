using System.Data;
using ApricotFramework.DataOps;

namespace ApricotFramework.DataProtection.DataOps.Impl;

/// <summary>
/// A lease over a connection reference obtained from the data operations registry.
/// </summary>
/// <remarks>
/// Disposing this disposes the reference, which is what the registry documents as the way to
/// release a connection it created. Disposing the connection directly would work by accident
/// today and is not the contract.
/// </remarks>
public sealed class DataOpsProtectionKeyConnection : IProtectionKeyConnection
{
    /// <summary>
    /// The reference this lease releases.
    /// </summary>
    private readonly IConnectionReference reference;

    /// <summary>
    /// Creates a lease over a connection reference.
    /// </summary>
    /// <param name="reference">The reference to release when the lease is returned.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is null.</exception>
    public DataOpsProtectionKeyConnection(IConnectionReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        this.reference = reference;
    }

    /// <inheritdoc />
    public IDbConnection GetConnection()
    {
        var connection = this.reference.Connection;

        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        return connection;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.reference.Dispose();
    }
}
